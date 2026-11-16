param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
$ErrorActionPreference='Stop'
if (-not ([Uri]$BaseUrl).IsLoopback) { throw 'Use isolated local/CI Backend with Mock payments.' }
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$sessions=@{};$lockerId=$null;$fixture=[Guid]::NewGuid();$run=$fixture.ToString('N');$email="payment-recovery-$run@example.test"
function New-RecoveryOrder([decimal]$Amount) {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $body=@{lockerId="$lockerId";boxId=$locker.availableSlots[0].id;items=@(@{serviceId=$service.id;quantity=1});paymentMethod=2}
    if($service.unit -eq 'kg'){$body.items[0].weightKg=1}
    $order=(Call-Api Post '/wash-order/' $body $customer).data
    Sql "UPDATE washgo.orders SET status=8,merchant_id='$merchantId',total_amount=$Amount,discount_amount=0,final_amount=$Amount,actual_weight_kg=1 WHERE id='$($order.id)';" | Out-Null
    return $order.id
}
function Fingerprint {
    (Sql "SELECT (SELECT count(*) FROM washgo.payment_transactions WHERE order_id IN (SELECT id FROM washgo.orders WHERE customer_id='$fixture'))::text||':'||(SELECT count(*) FROM washgo.loyalty_entries WHERE customer_id='$fixture')::text||':'||(SELECT count(*) FROM washgo.loyalty_wallets WHERE id='$fixture')::text;").Trim()
}
try {
    foreach($role in @('Admin','Customer','Merchant')){$sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data}
    $admin=$sessions.Admin.accessToken;$seedCustomer=$sessions.Customer.accessToken;$merchantId=$sessions.Merchant.user.id;$seed=$sessions.Customer.user.id
    Sql @"
BEGIN;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','$email','phone','0991234567','provider_id',NULL,'auth_provider',1))).* FROM washgo.users u WHERE id='$seed';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$fixture','loyalty_points',50000,'total_orders',0,'total_spent',0))).* FROM washgo.customers c WHERE user_id='$seed';
COMMIT;
"@ | Out-Null
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$email;password='Customer@123'} $null).data;$customer=$sessions.Fixture.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$merchantId");code="RECOVERY_$run";name='Payment recovery fixture';address='Fixture';district='Test';city='Test';latitude=10.7;longitude=106.7;boxCount=5} $admin).data
    $lockerId=$locker.id
    $service=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0]
    $id=New-RecoveryOrder 9000
    $path="/payment/orders/$id"
    Assert-Status 401 Get "$path/active-session?purpose=1" $null $null
    Assert-Status 403 Get "$path/active-session?purpose=1" $null $admin
    Assert-Status 404 Get "$path/active-session?purpose=1" $null $seedCustomer
    Assert-Status 400 Get "$path/quote?purpose=4" $null $customer
    Assert-Status 400 Get "$path/quote?purpose=2&pointsToRedeem=1" $null $customer
    $before=Fingerprint
    if((Call-Api Get "$path/active-session?purpose=1" $null $customer).data){throw 'New order has active session.'}
    $quote=(Call-Api Get "$path/quote?purpose=1" $null $customer).data
    if(!$quote.canCreate -or $quote.cashAmount -ne 9000 -or ($quote.methods|Where-Object{$_.paymentMethod -eq 2}).enabled -or !($quote.methods|Where-Object{$_.paymentMethod -eq 3}).enabled){throw '9000 VND capabilities mismatch.'}
    Assert-Status 400 Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=3;expectedQuoteToken='outdated'} $customer
    if((Fingerprint) -ne $before){throw 'Read/rejected request created wallet, entries or payment.'}
    $payment=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=3;expectedQuoteToken=$quote.quoteToken} $customer).data
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Mock provider required.'}
    $snapshot=Fingerprint
    $active=(Call-Api Get "$path/active-session?purpose=1" $null $customer).data
    if($active.paymentMethod -ne 3 -or !$active.canContinue -or $active.canCreateReplacement -or $active.PSObject.Properties['paymentUrl'] -or $active.PSObject.Properties['transactionReference']){throw 'Unsafe session projection.'}
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer
    if((Call-Api Get "$path/active-session?purpose=2" $null $customer).data){throw 'Purpose isolation broken.'}
    $op=[Guid]::NewGuid().ToString()
    $verification=(Call-Api Post "$path/verify-session" @{operationId=$op;purpose=1} $customer).data
    if($verification.state -ne 'Queued'){throw 'Verification was not queued (CI worker must be disabled).'}
    $verifying=(Call-Api Get "$path/active-session?purpose=1" $null $customer).data
    if($verifying.verificationOperationId -ne $op -or $verifying.canContinue -or $verifying.canVerify){throw 'Session read did not restore in-progress verification.'}
    Call-Api Post "$path/verify-session" @{operationId=$op;purpose=1} $customer | Out-Null
    $alias=[Guid]::NewGuid().ToString()
    Call-Api Post "$path/verify-session" @{operationId=$alias;purpose=1} $customer | Out-Null
    if((Sql "SELECT count(*) FROM washgo.payment_verifications WHERE customer_id='$fixture' AND is_active;").Trim() -ne '1'){throw 'Repeated verify duplicated gateway jobs.'}
    Assert-Status 404 Get "/payment/verification-operations/$op" $null $seedCustomer
    $other=New-RecoveryOrder 100000
    Assert-Status 409 Post "/payment/orders/$other/verify-session" @{operationId=$op;purpose=1} $customer
    if((Fingerprint) -ne $snapshot){throw 'Verification changed wallet or created a new payment reference.'}
    Assert-Status 400 Post '/payment/callback' @{orderId=$id;transactionReference=$payment.transactionReference;success=$false} $admin
    $done=(Call-Api Get "/payment/verification-operations/$op" $null $customer).data
    if($done.state -ne 'Completed' -or $done.reasonCode -ne 'GatewayVerifiedFailure'){throw 'Closed callback not reflected in operation read.'}
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=3} $customer
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=3;expectedQuoteToken=$quote.quoteToken} $customer
    $fresh=(Call-Api Get "$path/quote?purpose=1" $null $customer).data
    $replacement=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=3;expectedQuoteToken=$fresh.quoteToken} $customer).data
    if($replacement.transactionReference -eq $payment.transactionReference){throw 'Closed reference reused.'}
    # Late money on the OLD closed reference is retained for refund; no reward is earned.
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$payment.transactionReference;success=$true} $admin | Out-Null
    if((Sql "SELECT count(*) FROM washgo.refund_requests WHERE order_id='$id' AND is_excess_payment;").Trim() -ne '1'){throw 'Late receipt lost.'}
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$replacement.transactionReference;success=$true} $admin | Out-Null
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$replacement.transactionReference;success=$true} $admin | Out-Null
    if((Sql "SELECT count(*) FROM washgo.loyalty_entries WHERE customer_id='$fixture' AND kind='Earn';").Trim() -ne '1'){throw 'Payment replay earned twice or excess receipt earned points.'}
    # Unresolved excess receipt intentionally remains visible instead of hiding it behind Paid.
    $active=(Call-Api Get "$path/active-session?purpose=1" $null $customer).data
    if(!$active.requiresReconciliation -or $active.canContinue -or $active.canCreateReplacement){throw 'Excess receipt not safely blocked.'}
    # Fee purposes quote their own outstanding amount and never award/redeem points.
    Sql "UPDATE washgo.orders SET status=99,cancel_fee_amount=30000,cancel_fee_paid_amount=10000 WHERE id='$other';" | Out-Null
    $cancelQuote=(Call-Api Get "/payment/orders/$other/quote?purpose=3" $null $customer).data
    if($cancelQuote.grossDue -ne 20000 -or $cancelQuote.earnPercent -ne 0 -or $cancelQuote.maxUsePercent -ne 0){throw 'Cancellation quote is not isolated.'}
    $cancel=(Call-Api Post '/payment/cancellation-fee/create-url' @{orderId=$other;paymentMethod=2;expectedQuoteToken=$cancelQuote.quoteToken} $customer).data
    if((Call-Api Get "/payment/orders/$other/active-session?purpose=3" $null $customer).data.paymentMethod -ne 2){throw 'Cancellation session method lost on reload.'}
    if((Call-Api Post '/payment/cancellation-fee/create-url' @{orderId=$other;paymentMethod=2} $customer).data.transactionReference -ne $cancel.transactionReference){throw 'Cancellation session not reused.'}
    $fees=New-RecoveryOrder 100000
    Sql "UPDATE washgo.orders SET status=10,payment_status=2,pickup_deadline=now()-interval '1 hour',storage_fee_total=0 WHERE id='$fees';" | Out-Null
    $feeQuote=(Call-Api Get "/payment/orders/$fees/quote?purpose=2" $null $customer).data
    if($feeQuote.grossDue -le 0 -or $feeQuote.earnPercent -ne 0 -or $feeQuote.maxUsePercent -ne 0){throw 'Storage fee quote missing or grants points.'}
    $fee=(Call-Api Post '/payment/overdue-fee/create-url' @{orderId=$fees;paymentMethod=2;expectedQuoteToken=$feeQuote.quoteToken} $customer).data
    if($fee.amount -ne $feeQuote.cashAmount -or (Call-Api Get "/payment/orders/$fees/active-session?purpose=2" $null $customer).data.paymentMethod -ne 2){throw 'Storage fee amount/session mismatch.'}
    Write-Output 'PASS: pure quotes/session reads, ownership, amount limits, purpose isolation, durable verify replay/collision, fresh quote replacement, late receipt/refund and earn-once.'
}
finally {
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Fixture logout failed.'}}}
    if($lockerId){
        Sql @"
BEGIN;
DELETE FROM washgo.payment_verifications WHERE customer_id='$fixture';
DELETE FROM washgo.loyalty_entries WHERE customer_id='$fixture';
DELETE FROM washgo.loyalty_wallets WHERE id='$fixture';
DELETE FROM washgo.notifications WHERE user_id='$fixture' OR link_id IN (SELECT id FROM washgo.orders WHERE customer_id='$fixture') OR link_id IN (SELECT id FROM washgo.refund_requests WHERE customer_id='$fixture') OR link_id IN (SELECT id FROM washgo.payment_transactions WHERE order_id IN (SELECT id FROM washgo.orders WHERE customer_id='$fixture'));
DELETE FROM washgo.orders WHERE customer_id='$fixture';
DELETE FROM washgo.locker_merchants WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId';
DELETE FROM washgo.lockers WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "DELETE FROM washgo.refresh_tokens WHERE user_id='$fixture'; DELETE FROM washgo.customers WHERE user_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture';" | Out-Null
}
