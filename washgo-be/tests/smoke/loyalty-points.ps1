param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
if (-not ([Uri]$BaseUrl).IsLoopback) { throw 'Use isolated local/CI Backend with Mock payments.' }
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http
$sessions=@{};$orders=@();$lockerId=$null;$client=$null
$fixture=[Guid]::NewGuid();$run=$fixture.ToString('N');$fixtureEmail="loyalty-$run@example.test"
function Parallel-Loyalty($Requests) {
    $messages=@();$responses=@()
    try {
        $tasks=@(foreach($entry in $Requests){
            $message=New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post,"$BaseUrl$($entry.path)")
            $message.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$entry.token)
            $message.Content=New-Object System.Net.Http.StringContent(($entry.body|ConvertTo-Json -Depth 8),[Text.Encoding]::UTF8,'application/json')
            $messages+=$message;$client.SendAsync($message)
        })
        foreach($task in $tasks){$response=$task.GetAwaiter().GetResult();$responses+=$response
            [PSCustomObject]@{code=[int]$response.StatusCode;body=($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json)}
        }
    } finally {foreach($response in $responses){$response.Dispose()};foreach($message in $messages){$message.Dispose()}}
}
function New-LoyaltyOrder([decimal]$Amount=180000) {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $slot=$locker.availableSlots|Select-Object -First 1
    if(!$slot){throw 'Fixture locker has no slot.'}
    $body=@{lockerId="$lockerId";boxId=$slot.id;items=@(@{serviceId=$service.id;quantity=1});paymentMethod=2;customerNote='Loyalty isolated fixture'}
    if($service.unit -eq 'kg'){$body.items[0].weightKg=1}
    $order=(Call-Api Post '/wash-order/' $body $customer).data
    $script:orders+=([Guid]$order.id)
    Sql "UPDATE washgo.orders SET status=8,merchant_id='$merchantId',commission_rate_applied=20,total_amount=$Amount,discount_amount=0,final_amount=$Amount,actual_weight_kg=1 WHERE id='$($order.id)';"|Out-Null
    return $order
}
function Pay-Loyalty($Id,[long]$Points=0) {
    $payment=(Call-Api Post '/payment/create-url' @{orderId="$Id";paymentMethod=2;pointsToRedeem=$Points} $customer).data
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Mock provider required.'}
    Call-Api Post '/payment/callback' @{orderId="$Id";transactionReference=$payment.transactionReference;success=$true} $admin|Out-Null
    return $payment
}
function Refund-Loyalty($Id,[decimal]$Amount) {
    $refund=(Call-Api Post '/payment/refund-requests' @{orderId="$Id";amount=$Amount;reason='Loyalty fixture refund for regression'} $customer).data
    Call-Api Post "/payment/refund-requests/$($refund.id)/review" @{approve=$true;expectedVersion=$refund.version;operationId=[Guid]::NewGuid().ToString()} $admin|Out-Null
    $refund=(Call-Api Get "/payment/refund-requests/$($refund.id)" $null $admin).data
    $confirm=@{expectedVersion=$refund.version;refundReference="LOYALTY_$($refund.id)";operationId=[Guid]::NewGuid().ToString()}
    $pair=@(Parallel-Loyalty @(@{path="/payment/refund-requests/$($refund.id)/confirm-refunded";token=$admin;body=$confirm},@{path="/payment/refund-requests/$($refund.id)/confirm-refunded";token=$admin;body=$confirm}))
    if(@($pair|Where-Object{$_.code -ne 200}).Count){throw 'Concurrent refund confirmation failed.'}
    return (Call-Api Get "/payment/refund-requests/$($refund.id)" $null $admin).data
}
try {
    foreach($role in @('Admin','Customer','Merchant')){$sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data}
    $admin=$sessions.Admin.accessToken;$seedCustomer=$sessions.Customer.accessToken;$merchantId=$sessions.Merchant.user.id
    $seedId=$sessions.Customer.user.id
    # Clone only this fixture account; no changes to seed users, points or prices.
    Sql @"
BEGIN;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','$fixtureEmail','phone','0991234567','provider_id',NULL,'auth_provider',1))).* FROM washgo.users u WHERE id='$seedId';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$fixture','loyalty_points',50000,'total_orders',0,'total_spent',0))).* FROM washgo.customers c WHERE user_id='$seedId';
COMMIT;
"@|Out-Null
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$fixtureEmail;password='Customer@123'} $null).data
    $customer=$sessions.Fixture.accessToken
    $handler=New-Object System.Net.Http.HttpClientHandler;$handler.UseProxy=$false
    $client=New-Object System.Net.Http.HttpClient($handler);$client.Timeout=[TimeSpan]::FromSeconds(40)
    $service=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0]
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$merchantId");code="LOYALTY_$run";name='Loyalty fixture';address='Fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=10} $admin).data
    $lockerId=[Guid]$locker.id
    Assert-Status 401 Get '/customer/loyalty/balance' $null $null
    Assert-Status 403 Get '/customer/loyalty/balance' $null $admin
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.totalPoints -ne 50000 -or $balance.availablePoints -ne 50000){throw 'Legacy points were not preserved.'}
    $a=New-LoyaltyOrder;$b=New-LoyaltyOrder
    Assert-Status 404 Get "/customer/loyalty/quote/$($a.id)?pointsToRedeem=1" $null $seedCustomer
    Assert-Status 400 Get "/customer/loyalty/quote/$($a.id)?pointsToRedeem=36001" $null $customer
    $quote=(Call-Api Get "/customer/loyalty/quote/$($a.id)?pointsToRedeem=36000" $null $customer).data
    if($quote.cashAmount -ne 144000 -or $quote.expectedEarnedPoints -ne 1440){throw 'Quote used wrong cash basis.'}
    $requests=@($a,$b)|ForEach-Object{@{path='/payment/create-url';token=$customer;body=@{orderId=$_.id;paymentMethod=2;pointsToRedeem=36000}}}
    $pair=@(Parallel-Loyalty $requests)
    if(@($pair|Where-Object{$_.code -eq 200}).Count -ne 1 -or @($pair|Where-Object{$_.code -eq 409}).Count -ne 1){throw 'Two orders overspent one wallet.'}
    $payment=($pair|Where-Object{$_.code -eq 200}).body.data;$id=$payment.orderId
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Mock provider required.'}
    $retry=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=2;pointsToRedeem=36000} $customer).data
    if($retry.transactionReference -ne $payment.transactionReference){throw 'Retry duplicated points hold.'}
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=2;pointsToRedeem=0} $customer
    Sql "UPDATE washgo.payment_transactions SET session_state=3 WHERE transaction_reference='$($payment.transactionReference)';"|Out-Null
    Assert-Status 409 Post '/payment/create-url' @{orderId=$id;paymentMethod=2;pointsToRedeem=36000} $customer
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.heldPoints -ne 36000 -or $balance.availablePoints -ne 14000){throw 'Unknown gateway session released points.'}
    $callback=@{path='/payment/callback';token=$admin;body=@{orderId=$id;transactionReference=$payment.transactionReference;success=$true}}
    $pair=@(Parallel-Loyalty @($callback,$callback))
    if(@($pair|Where-Object{$_.code -ne 200}).Count){throw 'Repeated callback failed.'}
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.totalPoints -ne 15440 -or $balance.heldPoints -ne 0){throw 'Redeem/earn repeated or was not atomic.'}
    $count=(Sql "SELECT count(*) FROM washgo.loyalty_entries WHERE customer_id='$fixture' AND kind='Earn';").Trim()
    if($count -ne '1'){throw 'Duplicate reward ledger event.'}
    $from=[DateTimeOffset]::UtcNow.AddMinutes(-10).ToUnixTimeSeconds();$to=[DateTimeOffset]::UtcNow.AddMinutes(1).ToUnixTimeSeconds()
    $commissions=(Call-Api Post '/finance/commissions' @{fromDate=$from;toDate=$to;merchantId=$merchantId} $admin).data
    if(($commissions|Select-Object -First 1).pointsFunding -lt 36000){throw 'Merchant commission omitted WashGo points funding.'}
    $view=(Call-Api Get "/wash-order/$id" $null $admin).data
    if($view.collectedLaundryAmount -ne 180000 -or $view.commissionOnCollected -ne 36000 -or $view.merchantPayoutOnCollected -ne 144000){throw 'Merchant lost payout when Customer used points.'}
    # Check exact fixture order rather than sums polluted by earlier CI suites.
    $cash=(Sql "SELECT amount FROM washgo.payment_transactions WHERE transaction_reference='$($payment.transactionReference)';").Trim()
    if([decimal]$cash -ne 144000){throw 'Points were recorded as gateway cash.'}
    $refund=Refund-Loyalty $id 72000
    if($refund.pointsAmount -ne 18000 -or $refund.rewardPointsReversed -ne 720){throw 'Partial refund points were incorrect.'}
    $refund=Refund-Loyalty $id 72000
    if($refund.pointsAmount -ne 18000 -or $refund.rewardPointsReversed -ne 720){throw 'Second partial refund drifted.'}
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.totalPoints -ne 50000){throw 'Full refund did not restore initial wallet.'}
    $failedOrder=New-LoyaltyOrder
    $failed=(Call-Api Post '/payment/create-url' @{orderId=$failedOrder.id;paymentMethod=2;pointsToRedeem=1000} $customer).data
    Assert-Status 400 Post '/payment/callback' @{orderId=$failedOrder.id;transactionReference=$failed.transactionReference;success=$false} $admin
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.heldPoints -ne 0 -or $balance.totalPoints -ne 50000){throw 'Final failure did not release hold.'}
    Call-Api Post '/payment/callback' @{orderId=$failedOrder.id;transactionReference=$failed.transactionReference;success=$true} $admin|Out-Null
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.totalPoints -ne 50000){throw 'Late unapplied receipt earned points.'}
    $large=New-LoyaltyOrder 10000000;Pay-Loyalty $large.id|Out-Null
    $spend=New-LoyaltyOrder 750000;Pay-Loyalty $spend.id 150000|Out-Null
    Refund-Loyalty $large.id 10000000|Out-Null
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.debtPoints -ne 94000 -or $balance.availablePoints -ne 0){throw 'Refund after reward spending did not record debt.'}
    Assert-Status 400 Get "/customer/loyalty/quote/$($b.id)?pointsToRedeem=1" $null $customer
    $recover=New-LoyaltyOrder 10000000;Pay-Loyalty $recover.id|Out-Null
    $balance=(Call-Api Get '/customer/loyalty/balance' $null $customer).data
    if($balance.debtPoints -ne 0 -or $balance.totalPoints -ne 6000){throw 'New purchase did not repay reward debt.'}
    $history=(Call-Api Get '/customer/loyalty/history?page=1&take=2' $null $customer)
    if(@($history.list).Count -ne 2){throw 'Ledger pagination failed.'}
    Assert-Status 400 Get '/customer/loyalty/history?page=0&take=2' $null $customer
    Write-Output 'PASS: legacy opening balance, ownership, caps, concurrent holds/callbacks/refunds, uncertain/final failure and late receipts, partial refunds, debt/recovery and history pagination.'
}
finally {
    if($client){$client.Dispose()}
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Fixture logout failed.'}}}
    if($lockerId){
        Sql @"
BEGIN;
DELETE FROM washgo.loyalty_entries WHERE customer_id='$fixture';
DELETE FROM washgo.loyalty_wallets WHERE id='$fixture';
DELETE FROM washgo.administrative_operations WHERE resource_id IN (SELECT id::text FROM washgo.refund_requests WHERE customer_id='$fixture');
DELETE FROM washgo.notifications WHERE user_id='$fixture' OR link_id IN (SELECT id FROM washgo.orders WHERE customer_id='$fixture') OR link_id IN (SELECT id FROM washgo.refund_requests WHERE customer_id='$fixture');
DELETE FROM washgo.orders WHERE customer_id='$fixture';
DELETE FROM washgo.locker_merchants WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId';
DELETE FROM washgo.lockers WHERE id='$lockerId';
COMMIT;
"@|Out-Null
    }
    Sql "DELETE FROM washgo.loyalty_entries WHERE customer_id='$fixture'; DELETE FROM washgo.loyalty_wallets WHERE id='$fixture'; DELETE FROM washgo.refresh_tokens WHERE user_id='$fixture'; DELETE FROM washgo.customers WHERE user_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture';"|Out-Null
}
