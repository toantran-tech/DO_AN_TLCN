param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: private locker/service/users; timestamps and a legacy paid
# fixture are changed only on orders created by this test. No real provider calls.
$sessions=@{}; $orders=@(); $lockerId=$null
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid()
$intruderId=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid()
function New-CancelOrder {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $slot=$locker.availableSlots | Where-Object {$_.size -eq 2} | Select-Object -First 1
    if(!$slot){throw 'No private fixture slot available.'}
    $order=(Call-Api Post '/wash-order/' @{
        lockerId="$lockerId";boxId=$slot.id;paymentMethod=2
        items=@(@{serviceId="$serviceId";quantity=1});customerNote='Cancellation smoke fixture'
    } $customer).data
    $script:orders+=[Guid]$order.id
    return $order
}
function Collect-CancelOrder($Order) {
    Invoke-TestDelivery $Order.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$Order.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $Order.id collect $shipper | Out-Null
}
function Pay-CancelFee($Id,$Expected) {
    $payment=(Call-Api Post '/payment/cancellation-fee/create-url' @{orderId="$Id";paymentMethod=2;amount=1} $customer).data
    if($payment.amount -ne $Expected -or $payment.purpose -ne 3){throw 'Cancellation fee trusted client amount or wrong purpose.'}
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Local sandbox required.'}
    Assert-Status 400 Post '/payment/cancellation-fee/create-url' @{orderId="$Id";paymentMethod=2} $customer
    $callback=@{orderId="$Id";transactionReference=$payment.transactionReference;success=$true}
    Assert-Status 403 Post '/payment/callback' $callback $customer
    Call-Api Post '/payment/callback' $callback $admin | Out-Null
    $before=(Call-Api Get "/wash-order/$Id" $null $customer).data
    Call-Api Post '/payment/callback' $callback $admin | Out-Null
    $after=(Call-Api Get "/wash-order/$Id" $null $customer).data
    if($after.status -ne 99 -or $after.cancelFeePaidAmount -ne $Expected -or $before.timeline.Count -ne $after.timeline.Count){throw 'Fee replay changed status, amount, or timeline.'}
    if($after.paymentStatus -ne 1){throw 'Cancellation payment marked laundry as paid.'}
    Assert-Status 400 Post '/payment/create-url' @{orderId="$Id";paymentMethod=2} $customer
    Assert-Status 400 Post '/payment/cancellation-fee/create-url' @{orderId="$Id";paymentMethod=2} $customer
}
function Pickup-CancelledOrder($Id) {
    $before=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    $view=(Call-Api Get "/wash-order/$Id" $null $customer).data
    $opened=(Call-Api Post "/wash-order/$Id/open-pickup-box" @{qrCode=$view.pickupQrCodeString} $customer).data
    $same=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if($before -ne $same){throw 'Opening cancelled pickup released occupied box early.'}
    $body=@{pickupSessionToken=$opened.pickupSessionToken}
    $done=(Call-Api Post "/wash-order/$Id/confirm-pickup" $body $customer).data
    if($done.status -ne 99 -or $done.cancelledGoodsStatus -ne 4 -or !$done.cancelledGoodsReturnedAt){throw 'Cancelled goods pickup changed business status or lost handover.'}
    Call-Api Post "/wash-order/$Id/confirm-pickup" $body $customer | Out-Null
    $after=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if([int]$after -ne [int]$before+1){throw 'Pickup did not release exactly one box.'}
    if(@($done.timeline | Where-Object {$_.status -eq 12}).Count){throw 'Cancelled order became Completed.'}
}
function Parallel-Requests($Requests) {
    $jobs=@()
    try {
        foreach($request in $Requests){
            $jobs+=Start-Job -ScriptBlock {
                param($Url,$Token,$Body)
                try {
                    $r=Invoke-WebRequest $Url -UseBasicParsing -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $Token"} -Body $Body
                    [int]$r.StatusCode
                } catch {if(!$_.Exception.Response){throw};[int]$_.Exception.Response.StatusCode}
            } -ArgumentList $request.url,$request.token,$request.body
        }
        @($jobs|Wait-Job|Receive-Job)
    } finally {$jobs|Stop-Job -ErrorAction SilentlyContinue;$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}
try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id;$seedShipper=[Guid]$sessions.Shipper.user.id;$seedCustomer=[Guid]$sessions.Customer.user.id
    $email="cancel-$($fixtureUser.ToString('N'))@example.com";$intruderEmail="cancel-$($intruderId.ToString('N'))@example.com"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureUser','email','$email','full_name','Cancellation Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipper';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipper';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$intruderId','email','$intruderEmail','full_name','Cancellation Smoke Other Customer','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$intruderId'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','code','CANCEL_$($serviceId.ToString('N'))','name','Cancellation Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$email;password='Shipper@123'} $null).data;$shipper=$sessions.Fixture.accessToken
    $sessions.Intruder=(Call-Api Post '/auth/login' @{email=$intruderEmail;password='Customer@123'} $null).data;$intruder=$sessions.Intruder.accessToken
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="CX_$([Guid]::NewGuid().ToString('N'))";name='Cancellation Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2},@{boxNumber='3';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Free Pending cancellation, ownership, payload validation and replay'
    $o=New-CancelOrder;$id=$o.id
    Assert-Status 401 Post "/wash-order/$id/cancel" @{reason='Smoke'} $null
    Assert-Status 403 Post "/wash-order/$id/cancel" @{reason='Smoke'} $merchant
    Assert-Status 404 Get "/wash-order/$id/cancellation-quote" $null $intruder
    Assert-Status 404 Post "/wash-order/$id/cancel" @{reason='Smoke'} $intruder
    Assert-Status 400 Post "/wash-order/$id/cancel" @{reason='  '} $customer
    $quote=(Call-Api Get "/wash-order/$id/cancellation-quote" $null $customer).data
    if(!$quote.canCancel -or $quote.feeAmount -ne 0){throw 'Free cancellation quote wrong.'}
    $result=(Call-Api Post "/wash-order/$id/cancel" @{reason='Smoke free cancel';cancelFeeAmount=123} $customer).data
    if($result.cancelFeeAmount -ne 0 -or $result.cancelledGoodsStatus -ne 0){throw 'Free cancellation wrong.'}
    Call-Api Post "/wash-order/$id/cancel" @{reason='Replay'} $customer | Out-Null
    Assert-Status 400 Post '/payment/cancellation-fee/create-url' @{orderId=$id;paymentMethod=2} $customer

    Write-Output '2. Late Pending 30%, payment failure/retry, fee separated from laundry'
    $o=New-CancelOrder;$id=$o.id
    Sql "UPDATE washgo.orders SET created_on=EXTRACT(EPOCH FROM NOW())::bigint-1860 WHERE id='$id';" | Out-Null
    $quote=(Call-Api Get "/wash-order/$id/cancellation-quote" $null $customer).data
    if($quote.feeAmount -ne 30000){throw 'Late Pending quote wrong.'}
    $result=(Call-Api Post "/wash-order/$id/cancel" @{reason='Late Pending';cancelFeeAmount=1} $customer).data
    if($result.cancelFeeAmount -ne 30000){throw 'Fee trusted client.'}
    $failed=(Call-Api Post '/payment/cancellation-fee/create-url' @{orderId=$id;paymentMethod=2} $customer).data
    Assert-Status 400 Post '/payment/callback' @{orderId=$id;transactionReference=$failed.transactionReference;success=$false} $admin
    Pay-CancelFee $id 30000

    Write-Output '3. Deposited cancel keeps belongings, blocks staff free-box and requires fee before pickup'
    $o=New-CancelOrder;$id=$o.id
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    $before=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    $result=(Call-Api Post "/wash-order/$id/cancel" @{reason='Deposited cancel'} $customer).data
    $after=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if($result.cancelledGoodsStatus -ne 1 -or $before -ne $after -or $result.cancelFeeAmount -ne 30000){throw 'Occupied deposit slot was freed at cancellation.'}
    Assert-Status 400 Put '/locker/box-status' @{lockerId="$lockerId";boxId=$o.boxId;newStatus=1} $admin
    Assert-Status 400 Post "/wash-order/$id/shipper-collect" @{pinCode='000000'} $shipper
    Assert-Status 400 Post "/wash-order/$id/open-pickup-box" @{pinCode=$o.pickupPinCode} $customer
    Pay-CancelFee $id 30000
    Pickup-CancelledOrder $id

    Write-Output '4. Collected needs weight; 50% actual price; shipper returns cancelled goods; pickup requires fee, not laundry payment'
    $o=New-CancelOrder;$id=$o.id;Collect-CancelOrder $o
    Assert-Status 400 Post "/wash-order/$id/cancel" @{reason='Not weighed'} $customer
    Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=6.5} $merchant | Out-Null
    $quote=(Call-Api Get "/wash-order/$id/cancellation-quote" $null $customer).data
    if($quote.basisAmount -ne 122500 -or $quote.feeAmount -ne 61250){throw 'Collected fee did not use actual price.'}
    Call-Api Post "/wash-order/$id/cancel" @{reason='Collected weighed cancel'} $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    $returned=(Invoke-TestDelivery $id return $shipper).data
    if($returned.status -ne 99 -or $returned.cancelledGoodsStatus -ne 3){throw 'Cancelled return lost cancellation status.'}
    Invoke-TestDelivery $id return $shipper | Out-Null
    Assert-Status 400 Post "/wash-order/$id/open-pickup-box" @{pinCode=$o.pickupPinCode} $customer
    Pay-CancelFee $id 61250
    Pickup-CancelledOrder $id

    Write-Output '5. Washing cannot cancel; normal lifecycle still works'
    $o=New-CancelOrder;$normalId=$o.id;Collect-CancelOrder $o
    Call-Api Put "/wash-order/$normalId/actual-weight" @{weightKg=3} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$normalId;newStatus=4} $merchant | Out-Null
    Assert-Status 400 Post "/wash-order/$normalId/cancel" @{reason='Already washing'} $customer
    Call-Api Put '/wash-order/status' @{orderId=$normalId;newStatus=8} $merchant | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$normalId;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$normalId;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$normalId;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $normalId return $shipper | Out-Null
    Invoke-TestDelivery $normalId pickup $customer | Out-Null

    Write-Output '6. Legacy prepaid Pending: refund only balance after fee, no duplicate refund'
    $o=New-CancelOrder;$id=$o.id;$transactionId=[Guid]::NewGuid()
    Sql @"
UPDATE washgo.orders SET payment_status=2,created_on=EXTRACT(EPOCH FROM NOW())::bigint-1860 WHERE id='$id';
INSERT INTO washgo.payment_transactions SELECT (jsonb_populate_record(NULL::washgo.payment_transactions,to_jsonb(t)||jsonb_build_object(
'id','$transactionId','order_id','$id','transaction_reference','CANCEL_LEGACY_$transactionId','amount',100000,'status',2,'purpose',1))).*
FROM washgo.payment_transactions t WHERE transaction_reference='$($pay.transactionReference)';
"@ | Out-Null
    $cancelled=(Call-Api Post "/wash-order/$id/cancel" @{reason='Legacy prepaid fixture'} $customer).data
    if($cancelled.cancelFeePaidAmount -ne 30000 -or $cancelled.cancellationRefundAmount -ne 70000){throw 'Prepaid cancellation fee/refund wrong.'}
    Call-Api Post "/wash-order/$id/cancel" @{reason='Replay prepaid cancel'} $customer | Out-Null
    $refunds=@((Call-Api Get '/payment/refund-requests/mine' $null $customer).data | Where-Object {$_.orderId -eq $id})
    if($refunds.Count -ne 1 -or $refunds[0].amount -ne 70000){throw 'Cancellation created duplicate/full refund.'}
    Assert-Status 400 Post '/payment/refund-requests' @{orderId=$id;reason='Attempt to refund fee too'} $customer
    $refundId=$refunds[0].id
    Call-Api Post "/payment/refund-requests/$refundId/review" @{approve=$true} $admin | Out-Null
    Call-Api Post "/payment/refund-requests/$refundId/confirm-refunded" @{refundReference='SMOKE-NO-REAL-BANK-TRANSFER'} $admin | Out-Null

    Write-Output '7. Concurrent cancel/deposit: consistent occupied slot or free slot, no lost goods'
    $o=New-CancelOrder;$id=$o.id
    $results=Parallel-Requests @(
        @{url="$BaseUrl/wash-order/$id/cancel";token=$customer;body='{"reason":"Race cancel"}'},
        @{url="$BaseUrl/wash-order/$id/deposit";token=$customer;body=(@{pinCode=$o.depositPinCode}|ConvertTo-Json -Compress)}
    )
    if(@($results|Where-Object{$_ -notin @(200,400)}).Count){throw 'Unexpected race response.'}
    $view=(Call-Api Get "/wash-order/$id" $null $customer).data
    if($view.status -ne 99){throw 'Concurrent cancel/deposit lost cancellation.'}
    if($view.cancelledGoodsStatus -eq 1){Pay-CancelFee $id 30000;Pickup-CancelledOrder $id}
    elseif($view.cancelledGoodsStatus -ne 0 -or $view.cancelFeeAmount -ne 0){throw 'Race left invalid goods state.'}
    $free=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if([int]$free -ne 3){throw 'Fixture locker counter not restored.'}
    Write-Output 'PASS: cancellation fees, quote, authorization, sandbox fee/replay, occupied goods protection, shipper return, pickup, prepaid refund and concurrent cancel/deposit.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id IN('$fixtureUser','$intruderId'); UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';"|Out-Null
}
