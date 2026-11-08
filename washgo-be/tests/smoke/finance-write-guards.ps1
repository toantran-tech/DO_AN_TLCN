param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$fixture=[Guid]::NewGuid();$orderId=[Guid]::NewGuid();$paymentId=[Guid]::NewGuid();$sessions=@();$jobs=@();$keys=@()
try {
    $a=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$sessions+=$a;$admin=$a.accessToken
    $c=(Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data;$sessions+=$c;$customer=$c.accessToken
    $source=Sql "SELECT id FROM washgo.users WHERE email='merchant@washgo.vn';"
    $template=Sql "SELECT id FROM washgo.orders LIMIT 1;"
    if(!$template){throw 'An existing development order is required as clone template.'}
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','fin-guard-$fixture@example.test','provider_id',NULL))).* FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object('user_id','$fixture','tax_code',NULL,'commission_rate',20))).* FROM washgo.merchants m WHERE user_id='$source';
INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object('id','$orderId','order_code','FG_$($orderId.ToString('N'))','merchant_order_code','FG_$($orderId.ToString('N'))','merchant_id','$fixture','customer_id','$($c.user.id)','status',12,'payment_status',2,'payment_method',2,'final_amount',100000,'commission_rate_applied',20,'is_deleted',false))).* FROM washgo.orders o WHERE id='$template';
INSERT INTO washgo.payment_transactions(id,order_id,transaction_reference,payment_method,purpose,status,amount,completed_at,merchant_id_snapshot,commission_rate_snapshot,financial_snapshot_basis,created_on,modified_on,is_deleted)
VALUES('$paymentId','$orderId','FG_$paymentId',2,1,2,100000,'2026-01-05T12:00:00Z','$fixture',20,'test-fixture',1,1,false);
"@|Out-Null
    $from=[DateTimeOffset]::Parse('2026-01-01T00:00:00Z').ToUnixTimeSeconds();$to=[DateTimeOffset]::Parse('2026-01-31T23:59:59Z').ToUnixTimeSeconds()
    $body=@{merchantId="$fixture";fromDate=$from;toDate=$to}
    $quote=(Call-Api Post '/finance/settlements/quote' $body $admin).data
    if(!$quote.canCreate -or $quote.summary.payoutAmount -ne 80000){throw 'Quote did not reconcile receipt.'}
    Assert-Status 400 Post '/finance/settlements' $body $admin
    $body.quoteToken=$quote.quoteToken;$body.operationId=[Guid]::NewGuid().ToString();$keys+=$body.operationId
    $saved=(Call-Api Post '/finance/settlements' $body $admin).data
    $storedLines=Call-Api Get "/finance/settlements/$($saved.id)/lines?page=1&take=1" $null $admin
    if($storedLines.total -ne 1 -or $storedLines.list[0].orderId -ne "$orderId" -or $storedLines.list[0].collected -ne 100000){throw 'Persisted API-created snapshot paging mismatch.'}
    $again=(Call-Api Post '/finance/settlements' $body $admin).data
    if($again.id -ne $saved.id){throw 'Settlement replay duplicated.'}
    $overlap=$body.Clone();$overlap.operationId=[Guid]::NewGuid().ToString();$keys+=$overlap.operationId
    Assert-Status 409 Post '/finance/settlements' $overlap $admin
    $confirm=@{reference='fixture-transfer';expectedVersion=$saved.version;operationId=[Guid]::NewGuid().ToString()};$keys+=$confirm.operationId
    $paid=(Call-Api Post "/finance/settlements/$($saved.id)/confirm-paid" $confirm $admin).data
    Call-Api Post "/finance/settlements/$($saved.id)/confirm-paid" $confirm $admin|Out-Null
    Assert-Status 409 Post "/finance/settlements/$($saved.id)/confirm-paid" @{reference='different';expectedVersion=$paid.version} $admin
    $refund=(Call-Api Post '/payment/refund-requests' @{orderId="$orderId";reason='Partial refund fixture';amount=30000} $customer).data
    $approved=(Call-Api Post "/payment/refund-requests/$($refund.id)/review" @{approve=$true;expectedVersion=$refund.version} $admin).data
    $confirmed=(Call-Api Post "/payment/refund-requests/$($refund.id)/confirm-refunded" @{refundReference='part-1';expectedVersion=$approved.version} $admin).data
    if((Sql "SELECT status FROM washgo.payment_transactions WHERE id='$paymentId';") -ne '2'){throw 'Partial refund marked full transaction refunded.'}
    Call-Api Post "/payment/refund-requests/$($refund.id)/confirm-refunded" @{refundReference='part-1'} $admin|Out-Null
    Assert-Status 409 Post "/payment/refund-requests/$($refund.id)/confirm-refunded" @{refundReference='other'} $admin
    $breakdown=(Call-Api Get "/finance/settlements/$($saved.id)/orders" $null $admin).data
    if(!$breakdown.isSnapshot -or $breakdown.lines[0].refunded -ne 0 -or $breakdown.settledPayoutAmount -ne 80000){throw 'Confirmed refund rewrote closed settlement.'}
    $now=[DateTimeOffset]::UtcNow
    $report=(Call-Api Post '/finance/commissions' @{merchantId="$fixture";fromDate=$now.AddHours(-1).ToUnixTimeSeconds();toDate=$now.AddMinutes(1).ToUnixTimeSeconds()} $admin).data
    if($report[0].refundedAmount -ne 30000 -or $report[0].netRevenue -ne -30000 -or $report[0].commissionAmount -ne -6000 -or $report[0].payoutAmount -ne -24000){throw 'Cross-period refund adjustment lost.'}
    $second=(Call-Api Post '/payment/refund-requests' @{orderId="$orderId";reason='Remaining refund fixture'} $customer).data
    if($second.amount -ne 70000){throw 'Remaining refundable amount wrong.'}
    $approved=(Call-Api Post "/payment/refund-requests/$($second.id)/review" @{approve=$true;expectedVersion=$second.version} $admin).data
    Call-Api Post "/payment/refund-requests/$($second.id)/confirm-refunded" @{refundReference='part-2';expectedVersion=$approved.version} $admin|Out-Null
    if((Sql "SELECT payment_status FROM washgo.orders WHERE id='$orderId';") -ne '3'){throw 'Full cumulative refund did not mark refunded.'}
    Assert-Status 400 Post '/payment/refund-requests' @{orderId="$orderId";reason='Cannot over refund fixture';amount=1} $customer
    Write-Output 'PASS: quote, overlap, durable replay, payout reference conflict, partial/cumulative refund, remaining balance, cross-period negative adjustment and immutable settled lines.'
} catch {
    Write-Output $_.ScriptStackTrace
    if($_.ErrorDetails.Message){try{$err=$_.ErrorDetails.Message|ConvertFrom-Json;Write-Output $err.message;Write-Output ($err.meta|ConvertTo-Json -Compress)}catch{}}
    throw
} finally {
    Sql "DELETE FROM washgo.merchant_settlements WHERE merchant_id='$fixture'; DELETE FROM washgo.refund_requests WHERE order_id='$orderId'; DELETE FROM washgo.payment_transactions WHERE order_id='$orderId'; DELETE FROM washgo.orders WHERE id='$orderId'; DELETE FROM washgo.users WHERE id='$fixture';"|Out-Null
    if($keys.Count){$ids=($keys|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.administrative_operations WHERE id IN ($ids);"|Out-Null}
    foreach($s in $sessions){try{Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken|Out-Null}catch{}}
}
