param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

$sessions = @{}
$orders = @()
$fixtureUser = [Guid]::NewGuid()
$fixtureShipper = [Guid]::NewGuid()
$fixtureEmail = "qr-pin-smoke-$($fixtureUser.ToString('N'))@example.com"
$completed = $false
$lockerId = $null

try {
    Write-Host "=== 1. Dang nhap cac vai tro ===" -ForegroundColor Cyan
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

    Write-Host "=== 2. Tao Shipper fixture de test ===" -ForegroundColor Cyan
    Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users, to_jsonb(u) || jsonb_build_object(
    'id', '$fixtureUser', 'email', '$fixtureEmail', 'full_name', 'QR Pin Smoke Shipper',
    'auth_provider', 1, 'provider_id', NULL, 'is_active', true))).*
FROM washgo.users u WHERE id = '$seedUser';
INSERT INTO washgo.shippers
SELECT (jsonb_populate_record(NULL::washgo.shippers, to_jsonb(s) || jsonb_build_object(
    'id', '$fixtureShipper', 'user_id', '$fixtureUser', 'status', 1, 'is_active', true))).*
FROM washgo.shippers s WHERE user_id = '$seedUser';
"@ | Out-Null

    $sessions.Fixture = (Call-Api Post '/auth/login' @{ email = $fixtureEmail; password = 'Shipper@123' } $null).data
    $shipper = $sessions.Fixture.accessToken

    Write-Host "=== 3. Tim Locker va Service cua Merchant ===" -ForegroundColor Cyan
    $locker = (Call-Api Post '/locker/' @{
        merchantId="$merchantId"; code="QR_$([Guid]::NewGuid().ToString('N'))"; name='QR PIN Smoke Locker'
        address='Development fixture'; district='Test'; city='Test'; latitude=10.77; longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=3})
    } $admin).data
    $lockerId = [Guid]$locker.id
    $locker = (Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $slot = $locker.availableSlots | Select-Object -First 1
    $service = (Call-Api Get "/customer/services?merchantId=$merchantId&page=1&take=100" $null $customer).list | Select-Object -First 1

    Write-Host "=== 4. Customer tao don hang moi ===" -ForegroundColor Cyan
    $item = @{ serviceId = $service.id; quantity = 1 }
    if ($service.unit -eq 'kg') { $item.weightKg = 3 }
    $order = (Call-Api Post '/wash-order/' @{
        lockerId = $locker.id; boxId = $slot.id; paymentMethod = 2
        customerNote = 'QR PIN smoke test order'; items = @($item)
    } $customer).data
    $orderId = [Guid]$order.id
    $orders += $orderId

    if ($order.status -ne 1) { throw "Don hang moi phai o trang thai Pending (1), nhan duoc: $($order.status)" }
    $depositPin = $order.depositPinCode
    $qrString = $order.qrCodeString
    if ([string]::IsNullOrWhiteSpace($depositPin)) { throw "Thieu DepositPinCode trong don moi tao" }

    Write-Host "=== 5. Kiem tra CHAN DUONG TAT qua PUT /wash-order/status ===" -ForegroundColor Cyan
    # Khong cho phep chuyen truc tiep sang Deposited (2), Collected (3), ReadyToReturn (10), Returned (11), Completed (12)
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 2 } $admin
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 3 } $admin
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 10 } $admin
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 11 } $admin
    Assert-Status 400 Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 12 } $admin
    Write-Host "  -> Tat ca cac duong tat da bi chan thanh cong!" -ForegroundColor Green

    Write-Host "=== 6. Test Brute-Force lockout gui do ===" -ForegroundColor Cyan
    $jobs=@()
    try {
        1..5 | ForEach-Object {
            $jobs+=Start-Job -ScriptBlock {
                param($Url,$Token)
                try {
                    Invoke-RestMethod $Url -Method Post -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body '{"pinCode":"000000"}' | Out-Null
                    200
                } catch {
                    if(!$_.Exception.Response){throw}
                    [int]$_.Exception.Response.StatusCode
                }
            } -ArgumentList "$BaseUrl/wash-order/$orderId/deposit",$customer
        }
        $results=@($jobs | Wait-Job | Receive-Job)
        if($results.Count -ne 5 -or @($results | Where-Object {$_ -ne 400}).Count){throw 'Concurrent PIN checks failed.'}
        $attempts=Sql "SELECT customer_deposit_pin_attempt_count FROM washgo.orders WHERE id='$orderId';"
        if([int]$attempts -ne 5){throw 'Concurrent attempts were lost.'}
    } finally {
        $jobs | Stop-Job -ErrorAction SilentlyContinue
        $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    }
    # Lan thu 6 phai bao bi tam khoa 5 phut (400)
    Assert-Status 400 Post "/wash-order/$orderId/deposit" @{ pinCode = $depositPin } $customer
    Write-Host "  -> Khoa thanh cong sau 5 lan nhap sai!" -ForegroundColor Green

    # Expire lockout rather than deleting attempts; the next bad request starts at one.
    Sql "UPDATE washgo.orders SET customer_deposit_lockout_until = NOW() - INTERVAL '1 minute' WHERE id = '$orderId';" | Out-Null
    Assert-Status 400 Post "/wash-order/$orderId/deposit" @{ pinCode = '000000' } $customer
    $attempts = Sql "SELECT customer_deposit_pin_attempt_count FROM washgo.orders WHERE id = '$orderId';"
    if ([int]$attempts -ne 1) { throw 'Expired lockout did not reset attempts.' }
    # Reset fixture counter for the remaining checks.
    Sql "UPDATE washgo.orders SET customer_deposit_lockout_until = NULL, customer_deposit_pin_attempt_count = 0 WHERE id = '$orderId';" | Out-Null

    Write-Host "=== 7. Customer gui do vao locker (POST /deposit) ===" -ForegroundColor Cyan
    $depositedOrder = (Call-Api Post "/wash-order/$orderId/deposit" @{ pinCode = $depositPin } $customer).data
    if ($depositedOrder.status -ne 2) { throw "Don hang phai o trang thai Deposited (2) sau khi gui do, nhan duoc: $($depositedOrder.status)" }
    Write-Host "  -> Gui do thanh cong! Don sang Deposited." -ForegroundColor Green

    # Idempotent call
    $repeatDeposit = (Call-Api Post "/wash-order/$orderId/deposit" @{ pinCode = $depositPin } $customer).data
    if ($repeatDeposit.status -ne 2) { throw "Idempotent call deposit that bai" }
    Write-Host "  -> Idempotency gui do thanh cong!" -ForegroundColor Green

    Write-Host "=== 8. Merchant phan cong Shipper lay do ===" -ForegroundColor Cyan
    $assignedOrder = (Call-Api Put '/wash-order/assign-shipper' @{ orderId = "$orderId"; shipperId = "$fixtureShipper" } $merchant).data
    
    # Kiem tra RedactStaffData: Customer xem don KHONG thay ShipperTaskPinCode
    $customerView = (Call-Api Get "/wash-order/$orderId" $null $customer).data
    if ($customerView.shipperTaskPinCode -or $customerView.shipperTaskQrString) {
        throw "Loi bao mat: Customer nhin thay ma nhiem vu cua Shipper!"
    }
    # Shipper duoc phan cong nhin thay ma nhiem vu
    $shipperView = (Call-Api Get "/wash-order/$orderId" $null $shipper).data
    $taskPin = $shipperView.shipperTaskPinCode
    $taskQr = $shipperView.shipperTaskQrString
    if ([string]::IsNullOrWhiteSpace($taskPin)) {
        throw "Shipper khong nhan duoc ShipperTaskPinCode sau khi phan cong!"
    }
    Write-Host "  -> Phan cong Shipper thanh cong; task credentials are private." -ForegroundColor Green

    Write-Host "=== 9. Shipper lay do ve tiem (POST /shipper-collect) ===" -ForegroundColor Cyan
    # Thu nhap sai ma task
    Assert-Status 400 Post "/wash-order/$orderId/shipper-collect" @{ pinCode = "000000" } $shipper
    # Nhap dung ma task
    $collectedOrder = (Call-Api Post "/wash-order/$orderId/shipper-collect" @{ pinCode = $taskPin } $shipper).data
    if ($collectedOrder.status -ne 3) { throw "Don hang phai o trang thai Collected (3), nhan duoc: $($collectedOrder.status)" }
    Write-Host "  -> Shipper lay do thanh cong! Don sang Collected, o gui ban dau duoc giai phong." -ForegroundColor Green

    # Idempotent call
    $repeatCollect = (Call-Api Post "/wash-order/$orderId/shipper-collect" @{ pinCode = $taskPin } $shipper).data
    if ($repeatCollect.status -ne 3) { throw "Idempotent call collect that bai" }

    Write-Host "=== 10. Merchant can do, giat va chuyen trang thai ===" -ForegroundColor Cyan
    if ($service.unit -eq 'kg') {
        $weightedOrder = (Call-Api Put "/wash-order/$orderId/actual-weight" @{ weightKg = 3.5 } $merchant).data
        Write-Host "  -> Merchant chot can nang 3.5 kg thanh cong! Gia cuoi: $($weightedOrder.finalAmount)" -ForegroundColor Green
    }

    # Merchant cap nhat cac buoc giat
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 4 } $merchant | Out-Null # Washing
    Call-Api Put '/wash-order/status' @{ orderId = "$orderId"; newStatus = 8 } $merchant | Out-Null # AwaitingPayment

    Write-Host "=== 11. Customer thanh toan don hang ===" -ForegroundColor Cyan
    $payment = (Call-Api Post '/payment/create-url' @{ orderId = "$orderId"; paymentMethod = 2 } $customer).data
    Call-Api Post '/payment/callback' @{ orderId = "$orderId"; transactionReference = $payment.transactionReference; success = $true } $admin | Out-Null
    $paidOrder = (Call-Api Get "/wash-order/$orderId" $null $customer).data
    if ($paidOrder.status -ne 9) { throw "Don hang phai o trang thai Paid (9) sau khi thanh toan, nhan duoc: $($paidOrder.status)" }
    Write-Host "  -> Thanh toan thanh cong! Don sang Paid (9)." -ForegroundColor Green

    Write-Host "=== 12. Merchant phan cong Shipper tra do ===" -ForegroundColor Cyan
    Call-Api Put '/wash-order/assign-shipper' @{ orderId = "$orderId"; shipperId = "$fixtureShipper" } $merchant | Out-Null
    $shipperReturnView = (Call-Api Get "/wash-order/$orderId" $null $shipper).data
    $returnTaskPin = $shipperReturnView.shipperTaskPinCode
    if ([string]::IsNullOrWhiteSpace($returnTaskPin)) { throw "Shipper khong co task PIN tra do" }

    Write-Host "=== 13. Shipper tra do vao locker (POST /shipper-return) ===" -ForegroundColor Cyan
    $returnedOrder = (Call-Api Post "/wash-order/$orderId/shipper-return" @{ pinCode = $returnTaskPin } $shipper).data
    if ($returnedOrder.status -ne 10) { throw "Don hang phai sang ReadyToReturn (10), nhan duoc: $($returnedOrder.status)" }
    Write-Host "  -> Shipper tra do thanh cong! Don sang ReadyToReturn (10), da chiem o tra $($returnedOrder.returnBoxNumber)." -ForegroundColor Green

    # Idempotent call
    $repeatReturn = (Call-Api Post "/wash-order/$orderId/shipper-return" @{ pinCode = $returnTaskPin } $shipper).data
    if ($repeatReturn.status -ne 10) { throw "Idempotent call shipper-return that bai" }

    Write-Host "=== 14. Customer nhan do tai Locker (2 buoc mo phong) ===" -ForegroundColor Cyan
    $customerPickupView = (Call-Api Get "/wash-order/$orderId" $null $customer).data
    $pickupPin = $customerPickupView.pickupPinCode

    # Buoc 1: Mo tu nhan do (POST /open-pickup-box)
    # Thu ma sai
    Assert-Status 400 Post "/wash-order/$orderId/open-pickup-box" @{ pinCode = "123123" } $customer
    # Nhap dung ma
    Assert-Status 400 Post "/wash-order/$orderId/open-pickup-box" @{ qrCode = $qrString } $customer
    Assert-Status 400 Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken = 'not-opened' } $customer
    Assert-Status 404 Post "/wash-order/$orderId/shipper-return" @{ pinCode = $returnTaskPin } $sessions.Shipper.accessToken
    $beforeOpen = Sql "SELECT available_slots FROM washgo.lockers WHERE id = '$lockerId';"
    $openBoxResult = (Call-Api Post "/wash-order/$orderId/open-pickup-box" @{ pinCode = $pickupPin } $customer).data
    $sessionToken = $openBoxResult.pickupSessionToken
    if ([string]::IsNullOrWhiteSpace($sessionToken)) { throw "Khong nhan duoc pickupSessionToken o Buoc 1" }
    Write-Host "  -> Buoc 1 mo tu thanh cong! Cua o $($openBoxResult.returnBoxNumber) da mo, nhan session token." -ForegroundColor Green

    $afterOpen = Sql "SELECT available_slots FROM washgo.lockers WHERE id = '$lockerId';"
    if ($beforeOpen -ne $afterOpen) { throw 'Open-box released the slot early.' }
    Sql "UPDATE washgo.orders SET pickup_session_expires_at=NOW()-INTERVAL '1 minute' WHERE id='$orderId';" | Out-Null
    Assert-Status 400 Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken=$sessionToken } $customer
    $openBoxResult = (Call-Api Post "/wash-order/$orderId/open-pickup-box" @{ qrCode=$customerPickupView.pickupQrCodeString } $customer).data
    $sessionToken=$openBoxResult.pickupSessionToken
    Sql "UPDATE washgo.orders SET payment_status=1 WHERE id='$orderId';" | Out-Null
    Assert-Status 400 Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken=$sessionToken } $customer
    Sql "UPDATE washgo.orders SET payment_status=2 WHERE id='$orderId';" | Out-Null
    # Buoc 2: Xac nhan da lay do va dong tu (POST /confirm-pickup)
    # Thu gui sai token
    Assert-Status 400 Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken = "wrong-session-token" } $customer
    # Gui dung token
    $finalOrder = (Call-Api Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken = $sessionToken } $customer).data
    if ($finalOrder.status -ne 12) { throw "Don hang phai chuyen sang Completed (12) sau khi xac nhan, nhan duoc: $($finalOrder.status)" }
    Write-Host "  -> Buoc 2 xac nhan thanh cong! O tra duoc giai phong, don hang hoan tat Completed (12)!" -ForegroundColor Green

    # Idempotent call
    $repeatConfirm = (Call-Api Post "/wash-order/$orderId/confirm-pickup" @{ pickupSessionToken = $sessionToken } $customer).data
    if ($repeatConfirm.status -ne 12) { throw "Idempotent call confirm-pickup that bai" }
    Write-Host "  -> Idempotency confirm-pickup thanh cong!" -ForegroundColor Green

    Write-Host '=== 15. Warehouse pickup and fee payment consistency ===' -ForegroundColor Cyan
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $stored=(Call-Api Post '/wash-order/' @{
        lockerId="$lockerId";boxId=$locker.availableSlots[0].id;paymentMethod=2;items=@($item)
    } $customer).data
    $storageId=[Guid]$stored.id; $orders+=$storageId
    Call-Api Post "/wash-order/$storageId/deposit" @{qrCode=$stored.qrCodeString} $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$storageId";shipperId="$fixtureShipper"} $merchant | Out-Null
    $task=(Call-Api Get "/wash-order/$storageId" $null $shipper).data
    Call-Api Post "/wash-order/$storageId/shipper-collect" @{qrCode=$task.shipperTaskQrString} $shipper | Out-Null
    if($stored.includedWeightKg -gt 0){Call-Api Put "/wash-order/$storageId/actual-weight" @{weightKg=3} $merchant | Out-Null}
    Call-Api Put '/wash-order/status' @{orderId="$storageId";newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$storageId";newStatus=8} $merchant | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId="$storageId";paymentMethod=2} $customer).data
    if($pay.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Local sandbox required.'}
    Call-Api Post '/payment/callback' @{orderId="$storageId";transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$storageId";shipperId="$fixtureShipper"} $merchant | Out-Null
    $task=(Call-Api Get "/wash-order/$storageId" $null $shipper).data
    Call-Api Post "/wash-order/$storageId/shipper-return" @{qrCode=$task.shipperTaskQrString} $shipper | Out-Null
    Sql "UPDATE washgo.orders SET pickup_deadline=NOW()-INTERVAL '73 hours' WHERE id='$storageId';" | Out-Null
    $moved=(Call-Api Post '/wash-order/move-to-storage' @{
        orderId="$storageId";storageLocation='Smoke warehouse';storageReference='SMOKE'
    } $admin).data
    $afterTransfer=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    Sql "UPDATE washgo.orders SET moved_to_storage_at=NOW()-INTERVAL '25 hours' WHERE id='$storageId';" | Out-Null
    Assert-Status 400 Post "/wash-order/$storageId/pickup-storage" @{} $customer
    $fee=(Call-Api Post '/payment/overdue-fee/create-url' @{orderId="$storageId";paymentMethod=2} $customer).data
    $expected=[decimal]$moved.overdueFeeTotal+200000
    if($fee.amount -ne $expected){throw "Fee mismatch: $($fee.amount), expected $expected"}
    Call-Api Post '/payment/callback' @{orderId="$storageId";transactionReference=$fee.transactionReference;success=$true} $admin | Out-Null
    Sql "UPDATE washgo.orders SET payment_status=1 WHERE id='$storageId';" | Out-Null
    Assert-Status 400 Post "/wash-order/$storageId/pickup-storage" @{} $customer
    Sql "UPDATE washgo.orders SET payment_status=2 WHERE id='$storageId';" | Out-Null
    $finished=(Call-Api Post "/wash-order/$storageId/pickup-storage" @{note='Simulated handover'} $customer).data
    if($finished.status -ne 12){throw 'Warehouse order not completed.'}
    Call-Api Post "/wash-order/$storageId/pickup-storage" @{} $customer | Out-Null
    $afterWarehouse=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if($afterWarehouse -ne $afterTransfer){throw 'Warehouse pickup changed locker count.'}
    Write-Host "`n=== TAT CA CAC TEST LUONG QR/PIN GIAO NHAN THANH CONG 100%! ===" -ForegroundColor Green
    $completed = $true
}
finally {
    # Only this run's private locker/accounts are touched; preserve orders and payment audit records.
    foreach($session in $sessions.Values){
        try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null }
        catch { Write-Warning 'Could not revoke a smoke-test session.' }
    }
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN (12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,is_deleted=true,status=3 WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id='$fixtureUser';" | Out-Null
}