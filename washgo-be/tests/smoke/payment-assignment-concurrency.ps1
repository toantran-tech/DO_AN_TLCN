param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
if (-not ([Uri]$BaseUrl).IsLoopback) { throw 'Use an isolated local/CI Backend with Mock payments.' }
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http
$sessions=@{}; $orders=@(); $lockerId=$null; $client=$null
$run=[Guid]::NewGuid().ToString('N'); $merchantIds=@([Guid]::NewGuid(),[Guid]::NewGuid())
function Parallel-Requests($Requests) {
    $messages=@();$responses=@()
    try {
        $tasks=@(foreach($entry in $Requests){
            $message=New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post,"$BaseUrl$($entry.path)")
            $message.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$entry.token)
            $message.Content=New-Object System.Net.Http.StringContent(($entry.body|ConvertTo-Json -Depth 12),[Text.Encoding]::UTF8,'application/json')
            $messages+=$message
            $client.SendAsync($message)
        })
        foreach($task in $tasks){
            $response=$task.GetAwaiter().GetResult();$responses+=$response
            $json=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            [PSCustomObject]@{code=[int]$response.StatusCode;body=($json|ConvertFrom-Json)}
        }
    } finally { foreach($response in $responses){$response.Dispose()};foreach($message in $messages){$message.Dispose()} }
}
function New-FixtureOrder {
    $view=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $body=@{lockerId="$lockerId";boxId=$view.availableSlots[0].id;items=@(@{serviceId=$service.id;quantity=1});paymentMethod=2;customerNote="Payment/assignment concurrency $run"}
    if($service.unit -eq 'kg'){$body.items[0].weightKg=1}
    $order=(Call-Api Post '/wash-order/' $body $customer).data
    $script:orders+=@($order.id)
    $order
}
function Payment($Id,$Path='/payment/create-url') { (Call-Api Post $Path @{orderId="$Id";paymentMethod=2} $customer).data }
try {
    foreach($role in @('Admin','Customer','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchant=[Guid]$sessions.Merchant.user.id
    foreach($id in $merchantIds){
        Sql @"
BEGIN;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
 'id','$id','email','concurrency-$id@example.invalid','full_name','Concurrency Merchant','auth_provider',1,'provider_id',NULL,'is_active',true))).*
FROM washgo.users u WHERE id='$seedMerchant';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
 'user_id','$id','business_name','Concurrency Merchant','tax_code',NULL,'status',1,'max_capacity',50,'contract_end_date',NULL))).*
FROM washgo.merchants m WHERE user_id='$seedMerchant';
COMMIT;
"@ | Out-Null
    }
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$($merchantIds[0])","$($merchantIds[1])");code="PAYR_$run";name='Payment/assignment race fixture';address='Fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=6} $admin).data
    $lockerId=[Guid]$locker.id
    # This suite seeds an already weighed order in SQL. Select a kg service explicitly.
    $service = (Call-Api Post '/customer/services/filter' @{page=1;take=100} $customer).list |
        Where-Object { $_.unit -eq 'kg' } | Select-Object -First 1
    if (!$service) { throw 'No active kg service available for the payment fixture.' }
    $handler=New-Object System.Net.Http.HttpClientHandler;$handler.UseProxy=$false
    $client=New-Object System.Net.Http.HttpClient($handler);$client.Timeout=[TimeSpan]::FromSeconds(40)

    $payOrder=New-FixtureOrder;$payId=[Guid]$payOrder.id
    # Only fixture state is advanced directly: these tests isolate payment races,
    # while actual-weight/last-slot suites retain the complete order lifecycle.
    Sql "UPDATE washgo.orders SET status=8,merchant_id='$($merchantIds[0])',total_amount=100000,discount_amount=0,final_amount=100000,actual_weight_kg=1 WHERE id='$payId';" | Out-Null
    $request=@{path='/payment/create-url';token=$customer;body=@{orderId="$payId";paymentMethod=2}}
    $pair=@(Parallel-Requests @($request,$request))
    if(@($pair|Where-Object{$_.code -eq 200}).Count -lt 1 -or @($pair|Where-Object{$_.code -notin @(200,409)}).Count){throw 'Concurrent session creation failed.'}
    $payment=Payment $payId
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Mock payment provider required; do not run against a live gateway.'}
    $reference=$payment.transactionReference
    if((Sql "SELECT count(*) FROM washgo.payment_transactions WHERE order_id='$payId';").Trim() -ne '1'){throw 'Duplicate payment references created.'}
    if((Payment $payId).transactionReference -ne $reference){throw 'Retry did not reuse payment reference.'}
    Assert-Status 409 Post '/payment/create-url' @{orderId="$payId";paymentMethod=3} $customer
    Sql "UPDATE washgo.payment_transactions SET session_state=3 WHERE transaction_reference='$reference';" | Out-Null
    Assert-Status 409 Post '/payment/create-url' @{orderId="$payId";paymentMethod=2} $customer
    # IPN is allowed before a URL exists; a late URL finalizer must not be needed
    # to identify the already-durable transaction.
    Sql "UPDATE washgo.payment_transactions SET session_state=1,payment_url=NULL WHERE transaction_reference='$reference';" | Out-Null
    Assert-Status 401 Get '/payment/reconciliations' $null $null
    Assert-Status 403 Get '/payment/reconciliations' $null $customer
    $callback=@{path='/payment/callback';token=$admin;body=@{orderId="$payId";transactionReference=$reference;success=$true}}
    $pair=@(Parallel-Requests @($callback,$callback))
    if(@($pair|Where-Object{$_.code -ne 200}).Count){throw 'Concurrent callback replay failed.'}
    if((Sql "SELECT count(*) FROM washgo.order_status_history WHERE order_id='$payId' AND changed_by_type='PaymentGateway';").Trim() -ne '1'){throw 'Repeated payment timeline.'}

    # A second verified receipt must be retained and refunded separately, including legacy references.
    $extraId=[Guid]::NewGuid();$extraRef="EXTRA_$run"
    Sql @"
INSERT INTO washgo.payment_transactions
SELECT (jsonb_populate_record(NULL::washgo.payment_transactions,to_jsonb(p)||jsonb_build_object(
 'id','$extraId','transaction_reference','$extraRef','provider_transaction_id',NULL,'status',1,'session_state',0,
 'is_active_session',false,'gateway_verified_at',NULL,'completed_at',NULL))).*
FROM washgo.payment_transactions p WHERE transaction_reference='$reference';
"@ | Out-Null
    foreach($i in 1..2){Call-Api Post '/payment/callback' @{orderId="$payId";transactionReference=$extraRef;success=$true} $admin | Out-Null}
    $review=(Call-Api Get '/payment/reconciliations?take=100' $null $admin).list | Where-Object {$_.id -eq "$extraId"}
    if(!$review -or $review.status -ne 5 -or !$review.refundRequestId){throw 'Unapplied receipt has no reconciliation/refund record.'}
    if((Sql "SELECT count(*) FROM washgo.refund_requests WHERE original_payment_transaction_id='$extraId' AND is_excess_payment;").Trim() -ne '1'){throw 'IPN replay duplicated excess refund.'}
    $refund=(Call-Api Get "/payment/refund-requests/$($review.refundRequestId)" $null $admin).data
    Call-Api Post "/payment/refund-requests/$($refund.id)/review" @{approve=$true;expectedVersion=$refund.version;operationId=[Guid]::NewGuid().ToString()} $admin | Out-Null
    $refund=(Call-Api Get "/payment/refund-requests/$($refund.id)" $null $admin).data
    $confirm=@{expectedVersion=$refund.version;refundReference="REFUND_$run";operationId=[Guid]::NewGuid().ToString()}
    foreach($i in 1..2){Call-Api Post "/payment/refund-requests/$($refund.id)/confirm-refunded" $confirm $admin | Out-Null}
    $stats=(Sql "SELECT payment_status,(SELECT status FROM washgo.payment_transactions WHERE id='$extraId'),(SELECT requires_reconciliation FROM washgo.payment_transactions WHERE id='$extraId'),(SELECT status FROM washgo.payment_transactions WHERE transaction_reference='$reference') FROM washgo.orders WHERE id='$payId';").Trim()
    if($stats -ne '2|4|t|2'){throw "Excess refund changed laundry payment or classification: $stats"}

    # Same session policy applies to cancellation fees.
    $cancelOrder=New-FixtureOrder;$cancelId=[Guid]$cancelOrder.id
    Sql "UPDATE washgo.orders SET status=99,cancel_fee_amount=30000,cancel_fee_paid_amount=0 WHERE id='$cancelId';" | Out-Null
    $fee=Payment $cancelId '/payment/cancellation-fee/create-url'
    if((Payment $cancelId '/payment/cancellation-fee/create-url').transactionReference -ne $fee.transactionReference){throw 'Cancellation fee was duplicated.'}
    $legacyRef="LEGACY_$run"
    Sql @"
INSERT INTO washgo.payment_transactions
SELECT (jsonb_populate_record(NULL::washgo.payment_transactions,to_jsonb(p)||jsonb_build_object(
 'id',gen_random_uuid(),'transaction_reference','$legacyRef','session_state',0,'is_active_session',false))).*
FROM washgo.payment_transactions p WHERE transaction_reference='$($fee.transactionReference)';
"@ | Out-Null
    Assert-Status 409 Post '/payment/cancellation-fee/create-url' @{orderId="$cancelId";paymentMethod=2} $customer
    if((Sql "SELECT count(*) FROM washgo.payment_transactions WHERE order_id='$cancelId' AND status=1;").Trim() -ne '2'){throw 'Legacy duplicate references were deleted/changed.'}

    $waiting=@(New-FixtureOrder;New-FixtureOrder;New-FixtureOrder;New-FixtureOrder)
    foreach($order in $waiting){Sql "UPDATE washgo.orders SET status=2,deposited_at=now() WHERE id='$($order.id)';" | Out-Null}
    $key=[Guid]::NewGuid().ToString()
    $assignment=@{path="/order-assignment/$($waiting[0].id)/assign";token=$admin;body=@{merchantId="$($merchantIds[0])";operationId=$key}}
    $pair=@(Parallel-Requests @($assignment,$assignment))
    if(@($pair|Where-Object{$_.code -ne 200}).Count -or @($pair|Where-Object{$_.body.meta.replayed}).Count -ne 1){throw 'Concurrent assignment did not replay exactly once.'}
    Assert-Status 409 Post "/order-assignment/$($waiting[1].id)/assign" @{merchantId="$($merchantIds[1])";operationId=$key} $admin
    $collision=[Guid]::NewGuid().ToString()
    $pair=@(Parallel-Requests @(
        @{path="/order-assignment/$($waiting[1].id)/assign";token=$admin;body=@{merchantId="$($merchantIds[0])";operationId=$collision}},
        @{path="/order-assignment/$($waiting[2].id)/assign";token=$admin;body=@{merchantId="$($merchantIds[1])";operationId=$collision}}
    ))
    if((($pair.code|Sort-Object)-join ',') -ne '200,409'){throw 'Cross-order operation key collision did not return 409.'}
    # Reset only these fixture assignments to reproduce reversed batch order.
    foreach($order in $waiting[1..2]){Sql "UPDATE washgo.orders SET merchant_id=NULL,commission_rate_applied=NULL,status=2 WHERE id='$($order.id)';" | Out-Null}
    $pair=@(Parallel-Requests @(
        @{path='/order-assignment/auto-assign';token=$admin;body=@{operationId=[Guid]::NewGuid().ToString();assignments=@(@{orderId=$waiting[1].id;merchantId="$($merchantIds[0])"},@{orderId=$waiting[2].id;merchantId="$($merchantIds[0])"})}},
        @{path='/order-assignment/auto-assign';token=$admin;body=@{operationId=[Guid]::NewGuid().ToString();assignments=@(@{orderId=$waiting[2].id;merchantId="$($merchantIds[1])"},@{orderId=$waiting[1].id;merchantId="$($merchantIds[1])"})}}
    ))
    if(@($pair|Where-Object{$_.code -eq 200}).Count -ne 1 -or @($pair|Where-Object{$_.code -notin @(200,400,409)}).Count){throw 'Reversed batch deadlocked or partially succeeded.'}
    if((Sql "SELECT count(DISTINCT merchant_id) FROM washgo.orders WHERE id IN ('$($waiting[1].id)','$($waiting[2].id)');").Trim() -ne '1'){throw 'Batch was partially assigned.'}
    Assert-Status 400 Post '/order-assignment/auto-assign' @{operationId=[Guid]::NewGuid().ToString();assignments=@(@{orderId=$waiting[3].id;merchantId="$($merchantIds[0])"},@{orderId=$waiting[0].id;merchantId="$($merchantIds[0])"})} $admin
    if((Sql "SELECT count(*) FROM washgo.orders WHERE id='$($waiting[3].id)' AND merchant_id IS NOT NULL;").Trim() -ne '0'){throw 'Failed batch left a partial assignment.'}
    Write-Output 'PASS: payment session reuse/conflict/unknown, concurrent callbacks, excess receipt/refund isolation, same-key replay/collision and reversed batch locking/rollback.'
}
finally {
    if($client){$client.Dispose()}
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Fixture logout failed.'}}}
    if($lockerId){
        Sql @"
BEGIN;
DELETE FROM washgo.notifications WHERE link_id IN (SELECT id FROM washgo.orders WHERE locker_id='$lockerId')
 OR link_id IN (SELECT id FROM washgo.payment_transactions WHERE order_id IN (SELECT id FROM washgo.orders WHERE locker_id='$lockerId'))
 OR link_id IN (SELECT id FROM washgo.refund_requests WHERE order_id IN (SELECT id FROM washgo.orders WHERE locker_id='$lockerId'));
DELETE FROM washgo.orders WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_merchants WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId';
DELETE FROM washgo.lockers WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    $ids=($merchantIds|ForEach-Object{"'$_'"}) -join ','
    Sql "DELETE FROM washgo.merchants WHERE user_id IN ($ids); DELETE FROM washgo.users WHERE id IN ($ids);" | Out-Null
}
