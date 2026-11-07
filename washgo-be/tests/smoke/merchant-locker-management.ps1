param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Merchant locker list images, locker/box summary, usage history scope and validation on shared lockers.
$sessions=@{}; $teamId=$null; $lockers=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$NoAutoAssign=$true
function New-Locker($Name,$MerchantIds,$Boxes,$Images){
    $l=(Call-Api Post '/locker/' @{merchantIds=$MerchantIds;name="$Name $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=$Boxes;imageUrls=$Images} $admin).data
    $script:lockers+=[Guid]$l.id; $l
}
function UsageOf($Token,$Id,$Query){ Call-Api Get "/locker/$Id/usage-history?$Query" $null $Token }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedId="$($sessions.Merchant.user.id)"
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId="$($sessions.Team.user.id)"
    $img1=Upload-TestImage $admin; $img2=Upload-TestImage $admin
    $l1=New-Locker 'Img' @($teamId) 3 @($img1,$img2)
    $l2=New-Locker 'Maint' @($teamId) 2 @()
    $shared=New-Locker 'Shared' @($teamId,$seedId) 2 @()
    Sql "UPDATE washgo.lockers SET status=2 WHERE id='$($l2.id)'; UPDATE washgo.locker_slots SET status=4 WHERE id=(SELECT id FROM washgo.locker_slots WHERE locker_id='$($l1.id)' ORDER BY slot_number LIMIT 1);" | Out-Null

    Write-Output '1. List returns images (ordered) and a thumbnail'
    $list=(Call-Api Post '/locker/filter' @{page=1;take=10;keyword=$run} $team).list
    $row=@($list | Where-Object { $_.id -eq $l1.id })[0]
    if((@($row.imageUrls) -join ',') -ne "$img1,$img2" -or $row.thumbnailUrl -ne $img1){throw 'List images wrong.'}
    if(@(@($list | Where-Object { $_.id -eq $l2.id })[0].imageUrls).Count -ne 0 -or $null -ne @($list | Where-Object { $_.id -eq $l2.id })[0].thumbnailUrl){throw 'Locker without images wrong.'}

    Write-Output '2. Summary of all assigned lockers and their boxes'
    $s=(Call-Api Get '/locker/summary' $null $team).data
    if($s.totalLockers -ne 3 -or $s.activeLockers -ne 2 -or $s.maintenanceLockers -ne 1 -or $s.inactiveLockers -ne 0 -or $s.totalBoxes -ne 7 -or $s.availableBoxes -ne 6 -or $s.maintenanceBoxes -ne 1 -or !$s.generatedAt){throw "Summary wrong: $($s | ConvertTo-Json -Compress)"}
    if((Call-Api Get "/locker/summary?merchantId=$teamId" $null $admin).data.totalLockers -ne 3){throw 'Admin summary by merchant wrong.'}
    $own=(Call-Api Get '/locker/summary' $null $merchant).data
    $asked=(Call-Api Get "/locker/summary?merchantId=$teamId" $null $merchant).data
    if($asked.totalLockers -ne $own.totalLockers -or $asked.totalBoxes -ne $own.totalBoxes){throw 'Merchant read another scope.'}

    Write-Output '3. Shared locker history: each Merchant sees only its own orders'
    $orders=@()
    foreach($m in @($teamId,$seedId)){
        $box=(Call-Api Get "/customer/lockers/$($shared.id)" $null $customer).data.availableSlots[0].id
        $o=(Call-Api Post '/wash-order/' @{lockerId="$($shared.id)";boxId=$box;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $o.id deposit $customer | Out-Null
        Assign-Order $o.id $m $admin | Out-Null
        $orders+=$o
    }
    $th=UsageOf $team $shared.id 'page=1&take=10'
    $sh=UsageOf $merchant $shared.id 'page=1&take=10'
    $ah=UsageOf $admin $shared.id 'page=1&take=10'
    if($th.total -ne 1 -or $th.list[0].orderId -ne $orders[0].id -or $sh.total -ne 1 -or $sh.list[0].orderId -ne $orders[1].id -or $ah.total -ne 2){throw "Scope wrong: team=$($th.total) seed=$($sh.total) admin=$($ah.total)"}
    $u=$th.list[0]
    if($u.usageId -ne "$($orders[0].id):1" -or !$u.boxId -or !$u.boxNumber){throw 'usageId/boxId missing.'}
    if((UsageOf $team $shared.id "boxId=$($u.boxId)").total -ne 1){throw 'boxId filter wrong.'}

    Write-Output '4. Validation: foreign box, reversed or invalid dates'
    $foreignBox=(Call-Api Get "/locker/$($l1.id)" $null $team).data.boxes[1].id
    Assert-Status 400 Get "/locker/$($shared.id)/usage-history?boxId=$foreignBox" $null $team
    Assert-Status 400 Get "/locker/$($shared.id)/usage-history?fromDate=200&toDate=100" $null $team
    Assert-Status 400 Get "/locker/$($shared.id)/usage-history?fromDate=-1" $null $team
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    if((UsageOf $admin $shared.id "fromDate=$($now-3600)&toDate=$($now+60)").total -ne 2){throw 'Valid range wrong.'}
    if(($ah.list | ForEach-Object { $_.orderCode }) -join ',' -ne (($ah.list | Sort-Object @{e={[datetime]$_.putInAt};Descending=$true},@{e={$_.orderCode};Descending=$false} | ForEach-Object { $_.orderCode }) -join ',')){throw 'History order not by time.'}

    Write-Output 'PASS: locker list returns stored images in order plus thumbnail ([] / null without images); summary counts assigned lockers by status and non-deleted boxes by status in one snapshot, shared lockers once, Admin by merchantId, Merchant scope by JWT; usage history on a shared locker shows each Merchant only its own orders (Admin all) with usageId/boxId, boxId filter by id; foreign box, reversed and negative dates rejected with 400.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $lockers){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$id' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$id';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$id';
COMMIT;
"@ | Out-Null
    }
    Remove-FixtureMerchant $teamId
}
