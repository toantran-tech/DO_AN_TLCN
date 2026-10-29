param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Development fixtures only: clones one Shipper account; modifies only fixture
# orders/profile; finally removes the cloned account and restores reserved slots.
$sessions = @{}
$orders = @()
$jobs = @()
$fixtureUser = [Guid]::NewGuid()
$fixtureShipper = [Guid]::NewGuid()
$fixtureEmail = "assignment-smoke-$($fixtureUser.ToString('N'))@example.com"
$complete = $false
$merchantId = $null
try {
    foreach ($role in @('Admin', 'Merchant', 'Customer', 'Shipper')) {
        $sessions[$role] = (Call-Api Post '/auth/login' @{
            email = "$($role.ToLower())@washgo.vn"; password = "${role}@123"
        } $null).data
    }
    $admin = $sessions.Admin.accessToken
    $merchant = $sessions.Merchant.accessToken
    $customer = $sessions.Customer.accessToken
    $merchantId = [Guid]$sessions.Merchant.user.id
    $seedUser = [Guid]$sessions.Shipper.user.id
    Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users, to_jsonb(u) || jsonb_build_object(
    'id', '$fixtureUser', 'email', '$fixtureEmail', 'full_name', 'Assignment Smoke Shipper',
    'auth_provider', 1, 'provider_id', NULL, 'is_active', true))).*
FROM washgo.users u WHERE id = '$seedUser';
INSERT INTO washgo.shippers
SELECT (jsonb_populate_record(NULL::washgo.shippers, to_jsonb(s) || jsonb_build_object(
    'id', '$fixtureShipper', 'user_id', '$fixtureUser', 'status', 1, 'is_active', true))).*
FROM washgo.shippers s WHERE user_id = '$seedUser';
"@ | Out-Null
    $sessions.Fixture = (Call-Api Post '/auth/login' @{ email = $fixtureEmail; password = 'Shipper@123' } $null).data
    $shipper = $sessions.Fixture.accessToken
    $lockers = (Call-Api Get '/customer/lockers?page=1&take=100' $null $customer).list
    $lockerSummary = $lockers | Where-Object { $_.merchantId -eq "$merchantId" -and $_.availableSlotCount -ge 2 } | Select-Object -First 1
    if (!$lockerSummary) { throw 'Need two available slots for the assignment concurrency smoke test.' }
    $locker = (Call-Api Get "/customer/lockers/$($lockerSummary.id)" $null $customer).data
    $service = (Call-Api Get "/customer/services?merchantId=$merchantId&page=1&take=100" $null $customer).list | Select-Object -First 1
    foreach ($slot in @($locker.availableSlots | Select-Object -First 2)) {
        $item = @{ serviceId = $service.id; quantity = 1 }
        if ($service.unit -eq 'kg') { $item.weightKg = 1 }
        $order = (Call-Api Post '/wash-order/' @{
            lockerId = $locker.id; boxId = $slot.id; paymentMethod = 2
            customerNote = 'Shipper assignment smoke'; items = @($item)
        } $customer).data
        $orders += [Guid]$order.id
        Assert-Status 400 Put '/wash-order/assign-shipper' @{ orderId = $order.id; shipperId = "$fixtureShipper" } $merchant
        Call-Api Put '/wash-order/status' @{ orderId = $order.id; newStatus = 2 } $admin | Out-Null
    }
    $first = $orders[0]
    $assignment = @{ orderId = "$first"; shipperId = "$fixtureShipper"; shipperName = 'Forged name' }
    Assert-Status 403 Put '/wash-order/assign-shipper' $assignment $customer
    Assert-Status 403 Get "/wash-order/$first/available-shippers" $null $shipper
    Assert-Status 400 Put '/wash-order/assign-shipper' @{ orderId = "$first"; shipperId = [Guid]::NewGuid().ToString() } $merchant

    Sql "UPDATE washgo.orders SET merchant_id = NULL WHERE id = '$first';" | Out-Null
    Assert-Status 404 Get "/wash-order/$first/available-shippers" $null $merchant
    Assert-Status 404 Put '/wash-order/assign-shipper' $assignment $merchant
    Assert-Status 400 Put '/wash-order/assign-shipper' $assignment $admin
    Sql "UPDATE washgo.orders SET merchant_id = '$merchantId' WHERE id = '$first';" | Out-Null

    foreach ($state in @('is_active = false', 'status = 2', 'status = 3')) {
        Sql "UPDATE washgo.shippers SET is_active = true, status = 1 WHERE id = '$fixtureShipper'; UPDATE washgo.shippers SET $state WHERE id = '$fixtureShipper';" | Out-Null
        Assert-Status 400 Put '/wash-order/assign-shipper' $assignment $merchant
        $available = (Call-Api Get "/wash-order/$first/available-shippers" $null $merchant).data
        if (@($available | Where-Object { $_.id -eq "$fixtureShipper" }).Count) { throw 'Unavailable Shipper was listed.' }
    }
    Sql "UPDATE washgo.shippers SET is_active = true, status = 1 WHERE id = '$fixtureShipper'; UPDATE washgo.users SET is_active = false WHERE id = '$fixtureUser';" | Out-Null
    Assert-Status 400 Put '/wash-order/assign-shipper' $assignment $merchant
    Sql "UPDATE washgo.users SET is_active = true WHERE id = '$fixtureUser';" | Out-Null

    # Competing orders must not reserve the same available Shipper twice.
    foreach ($id in $orders) {
        $jobs += Start-Job -ScriptBlock {
            param($Url, $Token, $OrderId, $ShipperId)
            try {
                Invoke-RestMethod -Method Put -Uri "$Url/wash-order/assign-shipper" -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body (@{orderId=$OrderId;shipperId=$ShipperId}|ConvertTo-Json) | Out-Null
                200
            }
            catch { [int]$_.Exception.Response.StatusCode }
        } -ArgumentList $BaseUrl, $merchant, "$id", "$fixtureShipper"
    }
    $codes = @($jobs | Receive-Job -Wait -AutoRemoveJob)
    $jobs = @()
    if (($codes | Sort-Object) -join ',' -ne '200,400') { throw "Concurrent assignment returned $codes instead of 200/400." }

    $winner = $null
    $loser = $null
    foreach ($id in $orders) {
        $view = (Call-Api Get "/wash-order/$id" $null $merchant).data
        if ($view.shipperId -eq "$fixtureShipper") { $winner = $id } else { $loser = $id }
    }
    $before = (Call-Api Get "/wash-order/$winner" $null $merchant).data
    $repeated = (Call-Api Put '/wash-order/assign-shipper' @{orderId="$winner";shipperId="$fixtureShipper";shipperName='Forged name'} $merchant).data
    Assert-Private $repeated
    if ($repeated.shipperName -ne 'Assignment Smoke Shipper' -or $repeated.assignedTaskType -ne 1 -or
        !$repeated.shipperAssignedAt -or $before.timeline.Count -ne $repeated.timeline.Count) { throw 'Assignment identity, task, or replay check failed.' }
    $available = (Call-Api Get "/wash-order/$loser/available-shippers" $null $merchant).data
    if (@($available | Where-Object { $_.id -eq "$fixtureShipper" }).Count) { throw 'Busy Shipper was listed for another order.' }
    Call-Api Put '/wash-order/status' @{orderId="$winner";newStatus=3} $shipper | Out-Null
    $available = (Call-Api Get "/wash-order/$loser/available-shippers" $null $merchant).data
    if (!@($available | Where-Object { $_.id -eq "$fixtureShipper" }).Count) { throw 'Shipper did not become available after collection.' }
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$loser";shipperId="$fixtureShipper"} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$loser";newStatus=3} $shipper | Out-Null

    foreach ($id in $orders) {
        $quote=(Call-Api Get "/wash-order/$id" $null $merchant).data
        if ($quote.includedWeightKg -gt 0) {
            Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=1} $merchant | Out-Null
        }
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=4} $merchant | Out-Null
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=8} $merchant | Out-Null
        $payment = (Call-Api Post '/payment/create-url' @{orderId="$id";paymentMethod=2} $customer).data
        if ($payment.paymentUrl -notmatch '/payment/mock-checkout') { throw 'Local mock payment provider required.' }
        Call-Api Post '/payment/callback' @{orderId="$id";transactionReference=$payment.transactionReference;success=$true} $admin | Out-Null
        Assert-Status 400 Put '/wash-order/status' @{orderId="$id";newStatus=10} $shipper
        $returnAssignment = (Call-Api Put '/wash-order/assign-shipper' @{orderId="$id";shipperId="$fixtureShipper"} $merchant).data
        if ($returnAssignment.assignedTaskType -ne 2) { throw 'Return task not recorded.' }
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=10} $shipper | Out-Null
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=11} $admin | Out-Null
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=12} $admin | Out-Null
    }
    $complete = $true
    Write-Output "PASS: Merchant assignment, role/ownership/state/availability checks, server name, replay, concurrent 200/400, separate collect/return tasks. Completed smoke orders: $($orders -join ', ')"
}
finally {
    foreach ($job in $jobs) { Stop-Job $job -ErrorAction SilentlyContinue; Remove-Job $job -Force -ErrorAction SilentlyContinue }
    if ($orders.Count -gt 0) {
        $ids = ($orders | ForEach-Object { "'$_'" }) -join ','
        Sql "UPDATE washgo.orders SET merchant_id = '$merchantId' WHERE id IN ($ids);" | Out-Null
        if (!$complete) {
            Sql @"
BEGIN;
WITH slots AS (
    SELECT locker_id, locker_slot_id AS id FROM washgo.orders WHERE id IN ($ids) AND status IN (1,2)
    UNION
    SELECT COALESCE(return_locker_id,locker_id), return_box_id FROM washgo.orders WHERE id IN ($ids) AND status = 10
), freed AS (
    UPDATE washgo.locker_slots b SET status = 1 FROM slots s
    WHERE b.id = s.id AND b.locker_id = s.locker_id AND b.status IN (2,3) RETURNING b.locker_id
)
UPDATE washgo.lockers l SET available_slots = available_slots + f.count
FROM (SELECT locker_id, COUNT(*)::int AS count FROM freed GROUP BY locker_id) f WHERE l.id = f.locker_id;
UPDATE washgo.orders SET status = 99, cancellation_reason = 'Assignment smoke fixture cleanup' WHERE id IN ($ids) AND status <> 12;
COMMIT;
"@ | Out-Null
        }
    }
    foreach ($session in $sessions.Values) {
        if ($session.accessToken) {
            try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null }
            catch { Write-Warning 'Could not revoke a smoke session.' }
        }
    }
    Sql "DELETE FROM washgo.refresh_tokens WHERE user_id = '$fixtureUser'; DELETE FROM washgo.shippers WHERE id = '$fixtureShipper'; DELETE FROM washgo.users WHERE id = '$fixtureUser';" | Out-Null
}
