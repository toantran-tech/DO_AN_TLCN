param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db',
    [switch]$HelpersOnly
)
$ErrorActionPreference = 'Stop'

# Local development only: creates one identifiable order and temporarily changes
# ownership on that order to exercise access checks. Restores ownership in finally.
function Call-Api($Method, $Path, $Body, $Token) {
    $request = @{ Method = $Method; Uri = "$BaseUrl$Path"; TimeoutSec = 30 }
    if ($Token) { $request.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 15))
    }
    Invoke-RestMethod @request
}

function Assert-Status($Expected, $Method, $Path, $Body, $Token) {
    try { Call-Api $Method $Path $Body $Token | Out-Null }
    catch {
        if ([int]$_.Exception.Response.StatusCode -eq $Expected) { return }
        throw
    }
    throw "Expected HTTP $Expected for $Method $Path"
}

function Sql($Statement) {
    $result = $Statement | docker exec -i $PostgresContainer psql -U $PostgresUser -d $Database -v ON_ERROR_STOP=1 -At
    if ($LASTEXITCODE -ne 0) { throw 'Development fixture SQL failed.' }
    $result
}

function Assert-Private($Order) {
    if ($Order.customerName -ne '***' -or $Order.customerPhoneNumber -ne '***' -or
        $Order.depositPinCode -or $Order.pickupPinCode -or $Order.qrCodeString) {
        throw 'Staff response exposed Customer data or locker credentials.'
    }
    foreach ($entry in $Order.timeline) {
        if ($entry.actorRole -eq 'Customer' -and ($entry.actorId -or $entry.note -or
            $entry.actorName -eq $sessions.Customer.user.fullName)) {
            throw 'Staff timeline exposed Customer identity.'
        }
    }
}

if ($HelpersOnly) { return }

$sessions = @{}
$orderId = $null
$merchantId = $null
$shipperId = $null
$completed = $false
try {
    foreach ($role in @('Admin', 'Merchant', 'Shipper', 'Customer')) {
        $sessions[$role] = (Call-Api Post '/auth/login' @{
            email = "$($role.ToLower())@washgo.vn"; password = "${role}@123"
        } $null).data
    }
    $admin = $sessions.Admin.accessToken
    $merchant = $sessions.Merchant.accessToken
    $shipper = $sessions.Shipper.accessToken
    $customer = $sessions.Customer.accessToken
    $merchantId = [Guid]$sessions.Merchant.user.id
    $shipperUserId = [Guid]$sessions.Shipper.user.id
    $shipperId = [Guid](Sql "SELECT id FROM washgo.shippers WHERE user_id = '$shipperUserId' AND is_active = true LIMIT 1;")

    $lockers = (Call-Api Get '/customer/lockers?page=1&take=100' $null $customer).list
    $locker = $lockers | Where-Object { $_.merchantId -eq "$merchantId" } | Select-Object -First 1
    if (!$locker) { throw 'Seed Merchant needs an available locker for this smoke test.' }
    $locker = (Call-Api Get "/customer/lockers/$($locker.id)" $null $customer).data
    $services = (Call-Api Get "/customer/services?merchantId=$merchantId&page=1&take=100" $null $customer).list
    $service = $services | Select-Object -First 1
    if (!$service) { throw 'No approved service for the test locker.' }
    $item = @{ serviceId = $service.id; quantity = 1 }
    if ($service.unit -eq 'kg') { $item.weightKg = 1 }
    $created = (Call-Api Post '/wash-order/' @{
        lockerId = $locker.id; boxId = $locker.availableSlots[0].id
        paymentMethod = 2; customerNote = 'Order security smoke test'; items = @($item)
    } $customer).data
    $orderId = [Guid]$created.id
    if (!$orderId) { throw 'Order creation failed.' }

    foreach ($token in @($admin, $merchant, $shipper)) {
        foreach ($status in @(9, 99)) {
            Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = $status } $token
        }
    }
    Assert-Private (Call-Api Get "/wash-order/$orderId" $null $merchant).data
    $visible = Call-Api Post '/wash-order/filter' @{ keyword = $created.orderCode; page = 1; take = 10 } $merchant
    if ($visible.total -ne 1) { throw 'Merchant cannot see its own order.' }
    Assert-Private $visible.list[0]
    $customerView = (Call-Api Get "/wash-order/$orderId" $null $customer).data
    if ($customerView.customerName -ne $sessions.Customer.user.fullName -or !$customerView.depositPinCode) {
        throw 'Customer lost access to its own data.'
    }
    Assert-Status 400 Post '/wash-order/column-distinct-values?field=customername' @{} $merchant

    # Only the new smoke order is changed; no existing order is modified.
    Sql "UPDATE washgo.orders SET merchant_id = NULL WHERE id = '$orderId';" | Out-Null
    Assert-Status 404 Get "/wash-order/$orderId" $null $merchant
    Assert-Status 404 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 4 } $merchant
    $hidden = Call-Api Post '/wash-order/filter' @{ keyword = $created.orderCode; merchantId = "$merchantId" } $merchant
    if ($hidden.total -ne 0) { throw 'Merchant filter bypassed order scope.' }
    Sql "UPDATE washgo.orders SET merchant_id = '$merchantId' WHERE id = '$orderId';" | Out-Null

    Assert-Status 404 Get "/wash-order/$orderId" $null $shipper
    Assert-Status 400 Put '/wash-order/assign-shipper' @{ orderId = "$orderId"; shipperId = "$shipperId" } $merchant
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 2 } $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{ orderId = "$orderId"; shipperId = "$shipperId"; shipperName = 'Security smoke Shipper' } $merchant | Out-Null
    Assert-Private (Call-Api Get "/wash-order/$orderId" $null $shipper).data
    Sql "UPDATE washgo.orders SET shipper_id = NULL WHERE id = '$orderId';" | Out-Null
    Assert-Status 404 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 3 } $shipper
    $hidden = Call-Api Post '/wash-order/filter' @{ keyword = $created.orderCode; shipperId = "$shipperId" } $shipper
    if ($hidden.total -ne 0) { throw 'Shipper filter bypassed order scope.' }
    Sql "UPDATE washgo.orders SET shipper_id = '$shipperId' WHERE id = '$orderId';" | Out-Null

    $collected = (Call-Api Put '/wash-order/status' @{
        orderId = "$orderId"; newStatus = 3; actorId = "$merchantId"; actorName = 'Spoofed'; actorRole = 'Admin'
    } $shipper).data
    Assert-Private $collected
    $event = $collected.timeline | Where-Object { $_.status -eq 3 } | Select-Object -Last 1
    if ($event.actorId -ne "$shipperUserId" -or $event.actorRole -ne 'Shipper' -or $event.actorName -eq 'Spoofed') {
        throw 'Timeline trusted actor identity from request body.'
    }
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 4 } $shipper
    if ($created.includedWeightKg -gt 0) {
        Call-Api Put "/wash-order/$orderId/actual-weight" @{weightKg=1} $merchant | Out-Null
    }
    Assert-Private (Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 4 } $merchant).data
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 8 } $merchant | Out-Null

    $payment = (Call-Api Post '/payment/create-url' @{ orderId = "$orderId"; paymentMethod = 2 } $customer).data
    if ($payment.paymentUrl -notmatch '/payment/mock-checkout') { throw 'This smoke test requires the local mock provider.' }
    $callback = @{ orderId = "$orderId"; transactionReference = $payment.transactionReference; success = $true }
    Assert-Status 403 Post '/payment/callback' $callback $merchant
    $paid = Call-Api Post '/payment/callback' $callback $admin
    if ($paid.data.paymentStatus -ne 2) { throw 'Internal payment confirmation failed.' }
    Call-Api Post '/payment/callback' $callback $admin | Out-Null
    $paidOrder = (Call-Api Get "/wash-order/$orderId" $null $customer).data
    if (@($paidOrder.timeline | Where-Object { $_.status -eq 9 }).Count -ne 1) { throw 'Duplicate payment timeline.' }
    $badIpn = Call-Api Get '/payment/vnpay-ipn?vnp_SecureHash=invalid' $null $null
    if ($badIpn.rspCode -ne '97') { throw 'Invalid VNPay signature was accepted.' }

    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 10 } $shipper
    Call-Api Put '/wash-order/assign-shipper' @{ orderId = "$orderId"; shipperId = "$shipperId" } $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 10 } $shipper | Out-Null
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 11 } $admin | Out-Null
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 12 } $admin | Out-Null
    $completed = $true
    Write-Output "PASS: public Paid/Cancelled blocked; Merchant/Shipper scope and privacy; server actor identity; mock payment and replay; invalid VNPay IPN. Retained completed test order: $orderId"
}
finally {
    if ($orderId -and $merchantId) {
        $assignment = if ($shipperId) { "'$shipperId'" } else { 'NULL' }
        Sql "UPDATE washgo.orders SET merchant_id = '$merchantId', shipper_id = $assignment WHERE id = '$orderId';" | Out-Null
        if (!$completed) {
            try {
                $remaining = (Call-Api Get "/wash-order/$orderId" $null $sessions.Customer.accessToken).data
                if ($remaining.status -eq 1) {
                    Call-Api Post "/wash-order/$orderId/cancel" @{ reason = 'Cleanup failed security smoke test' } $sessions.Customer.accessToken | Out-Null
                }
                else { Write-Warning "Incomplete smoke order retained for inspection: $orderId" }
            }
            catch { Write-Warning "Could not clean up smoke order: $orderId" }
        }
    }
    foreach ($session in $sessions.Values) {
        if ($session.accessToken) {
            try { Call-Api Post '/auth/logout' @{ refreshToken = $session.refreshToken } $session.accessToken | Out-Null }
            catch { Write-Warning 'Could not revoke a smoke-test session.' }
        }
    }
}
