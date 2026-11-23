param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Disposable development/CI fixtures only; never alters existing lockers or users.
$sessions = @{}; $lockerId = $null
try {
    foreach ($role in @('Admin', 'Customer', 'Merchant')) {
        $sessions[$role] = (Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin = $sessions.Admin.accessToken; $customer = $sessions.Customer.accessToken
    $created = (Call-Api Post '/locker/' @{
        merchantId=$sessions.Merchant.user.id;name='Customer slots fixture'
        code="SLOTS_$([Guid]::NewGuid().ToString('N'))";address='Fixture';district='Test';city='Test'
        latitude=10.77;longitude=106.69
        boxes=@(1..5 | ForEach-Object { @{boxNumber="$_";size=2} })
    } $admin).data
    $lockerId = [Guid]$created.id
    Sql "UPDATE washgo.locker_slots SET status=LEAST(slot_number,4),is_deleted=(slot_number=5) WHERE locker_id='$lockerId'; UPDATE washgo.lockers SET available_slots=1 WHERE id='$lockerId';" | Out-Null
    $view = (Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    if ($view.id -ne "$lockerId" -or @($view.slots).Count -ne 4 -or
        ($view.slots.slotNumber -join ',') -ne '1,2,3,4' -or
        ($view.slots.status -join ',') -ne '1,2,3,4' -or
        $view.availableSlotCount -ne 1 -or @($view.availableSlots).Count -ne 1 -or
        $view.availableSlots[0].id -ne $view.slots[0].id) { throw 'Slot scope, ordering, deleted exclusion, status or available count mismatch.' }
    foreach ($slot in $view.slots) {
        $fields = @($slot.PSObject.Properties.Name)
        if (@($fields | Where-Object { $_ -notin @('id','slotNumber','size','status') }).Count) {
            throw 'Customer slot DTO exposes more than public metadata.'
        }
    }
    if ($view.availableSlots[0].PSObject.Properties.Name -contains 'status') { throw 'Legacy availableSlots shape changed.' }
    Sql "UPDATE washgo.locker_slots SET status=4 WHERE locker_id='$lockerId'; UPDATE washgo.lockers SET available_slots=0 WHERE id='$lockerId';" | Out-Null
    $full = (Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    if ($full.id -ne "$lockerId" -or @($full.slots).Count -ne 4 -or $full.availableSlotCount -ne 0 -or @($full.availableSlots).Count -ne 0) {
        throw 'A visible locker without free slots should return its detail, not 404.'
    }
    $guest = (Call-Api Get "/customer/lockers/$lockerId" $null $null).data
    $staff = (Call-Api Get "/customer/lockers/$lockerId" $null $admin).data
    if ($guest.id -ne "$lockerId" -or @($guest.slots).Count -ne 4 -or $staff.id -ne "$lockerId") { throw 'Public locker access failed.' }
    Assert-Status 401 Get '/customer/me' $null $null
    $guestList=Call-Api Get '/customer/lockers?page=1&take=2' $null $null
    if (!$guestList.succeeded -or @($guestList.list).Count -gt 2) { throw 'Guest locker list/pagination failed.' }
    foreach ($path in @('/customer/lockers?latitude=10','/customer/lockers?latitude=91&longitude=106',('/customer/lockers?keyword='+('x'*201)))) {
        Assert-Status 400 Get $path $null $null
    }
    Assert-Status 404 Get "/customer/lockers/$([Guid]::NewGuid())" $null $customer
    Sql "UPDATE washgo.lockers SET status=2 WHERE id='$lockerId';" | Out-Null
    Assert-Status 404 Get "/customer/lockers/$lockerId" $null $customer
    Sql "UPDATE washgo.lockers SET status=1,is_deleted=true WHERE id='$lockerId';" | Out-Null
    Assert-Status 404 Get "/customer/lockers/$lockerId" $null $customer
    Write-Output 'PASS: one locker only, four slot states, metadata allowlist, ordering/deleted exclusion, compatible availableSlots, full locker 200 and access guards.'
}
finally {
    if ($lockerId) {
        Sql "UPDATE washgo.lockers SET is_deleted=true,status=2 WHERE id='$lockerId'; UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$lockerId';" | Out-Null
    }
    foreach ($session in $sessions.Values) {
        if ($session.accessToken) {
            try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null }
            catch { Write-Warning 'Could not revoke a fixture session.' }
        }
    }
}
