param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db',
    [string]$VnPayTmnCode='CITEST01',[string]$VnPaySecret='CI-only-vnpay-secret-not-real',
    [string]$MomoPartner='CITESTMOMO',[string]$MomoAccess='ci-only-access',[string]$MomoSecret='CI-only-momo-secret-not-real',
    [string]$Frontend='http://localhost:5173')
$ErrorActionPreference='Stop'
if (-not ([Uri]$BaseUrl).IsLoopback) { throw 'Use the isolated CI Backend (Mock payments, CI-only signing keys).' }
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV/CI ONLY: gateway browser returns -> FE result URL chosen from verified server state; Customer result API.
# Return/IPN requests are signed here with the CI-only keys of tests/ci/compose.yml; no real gateway is called.
Add-Type -AssemblyName System.Net.Http
$sessions=@{}; $lockerId=$null; $fixture=[Guid]::NewGuid(); $run=$fixture.ToString('N'); $email="payment-result-$run@example.test"
$http=New-Object System.Net.Http.HttpClient((New-Object System.Net.Http.HttpClientHandler -Property @{AllowAutoRedirect=$false}))

function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Hex([byte[]]$Bytes){ -join ($Bytes | ForEach-Object { $_.ToString('x2') }) }
function Enc([string]$Value){ [System.Net.WebUtility]::UrlEncode($Value) }
function Location([string]$Path){
    $response=$http.GetAsync("$BaseUrl$Path").GetAwaiter().GetResult()
    Check ([int]$response.StatusCode -eq 303) "Expected 303 for $($Path.Split('?')[0]), got $([int]$response.StatusCode)."
    "$($response.Headers.Location)"
}
function VnPay-Query([hashtable]$Fields){
    # Same canonical form as the server: keys in ordinal order, WebUtility-encoded key=value pairs.
    $keys=[string[]]@($Fields.Keys); [Array]::Sort($keys,[StringComparer]::Ordinal)
    $query=[string]::Join('&',[string[]]@($keys | ForEach-Object { "$(Enc $_)=$(Enc ([string]$Fields[$_]))" }))
    $hmac=New-Object System.Security.Cryptography.HMACSHA512 (,[Text.Encoding]::UTF8.GetBytes($VnPaySecret))
    "$query&vnp_SecureHash=$(Hex $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($query)))"
}
function VnPay-Fields($Reference,[decimal]$Amount,[string]$Response,[string]$Status){
    @{vnp_TmnCode=$VnPayTmnCode;vnp_TxnRef=$Reference;vnp_Amount=[string][long]($Amount*100);vnp_ResponseCode=$Response;vnp_TransactionStatus=$Status;
      vnp_TransactionNo='14000001';vnp_BankCode='NCB';vnp_PayDate='20261123120000';vnp_OrderInfo='Thanh toan don WashGo'}
}
function Momo-Fields($Reference,[decimal]$Amount,[int]$Code){
    $f=[ordered]@{partnerCode=$MomoPartner;orderId=$Reference;requestId=$Reference;amount=[string][long]$Amount;orderInfo='Thanh toan don WashGo';orderType='momo_wallet';
      transId='3100000001';resultCode="$Code";message='Test';payType='qr';responseTime='1795400000000';extraData=''}
    $raw="accessKey=$MomoAccess&amount=$($f.amount)&extraData=$($f.extraData)&message=$($f.message)&orderId=$($f.orderId)&orderInfo=$($f.orderInfo)&orderType=$($f.orderType)&partnerCode=$($f.partnerCode)&payType=$($f.payType)&requestId=$($f.requestId)&responseTime=$($f.responseTime)&resultCode=$($f.resultCode)&transId=$($f.transId)"
    $hmac=New-Object System.Security.Cryptography.HMACSHA256 (,[Text.Encoding]::UTF8.GetBytes($MomoSecret))
    $f.signature=Hex $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($raw))
    $f
}
function Momo-Query($Fields){ [string]::Join('&',[string[]]@($Fields.Keys | ForEach-Object { "$_=$(Enc ([string]$Fields[$_]))" })) }
function Momo-Ipn($Fields){
    $body=@{}; foreach($k in $Fields.Keys){ $body[$k]=$Fields[$k] }
    foreach($k in 'amount','transId','resultCode','responseTime'){ $body[$k]=[long]$Fields[$k] }
    Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/payment/momo-ipn" -ContentType 'application/json' -Body ($body | ConvertTo-Json) | Out-Null
}
function New-PayableOrder([decimal]$Amount){
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $body=@{lockerId="$lockerId";boxId=$locker.availableSlots[0].id;items=@(@{serviceId=$service.id;quantity=1});paymentMethod=2}
    if($service.unit -eq 'kg'){$body.items[0].weightKg=1}
    $order=(Call-Api Post '/wash-order/' $body $customer).data
    Sql "UPDATE washgo.orders SET status=8,merchant_id='$merchantId',total_amount=$Amount,discount_amount=0,final_amount=$Amount,actual_weight_kg=1 WHERE id='$($order.id)';" | Out-Null
    $order.id
}
function Start-Payment($OrderId,[int]$Method,[string]$Path='/payment/create-url',[int]$Purpose=1){
    $quote=(Call-Api Get "/payment/orders/$OrderId/quote?purpose=$Purpose" $null $customer).data
    $p=(Call-Api Post $Path @{orderId="$OrderId";paymentMethod=$Method;expectedQuoteToken=$quote.quoteToken} $customer).data
    Check ($p.paymentUrl -match 'reference=([^&]+)') 'Mock payment URL without reference.'
    @{Reference=[Uri]::UnescapeDataString($Matches[1]);Amount=[decimal]$p.amount;Session=(Sql "SELECT id FROM washgo.payment_transactions WHERE transaction_reference='$([Uri]::UnescapeDataString($Matches[1]))';").Trim()}
}
function Result($OrderId,[int]$Purpose,$Session,$Token=$customer){
    $path="/payment/orders/$OrderId/result?purpose=$Purpose"; if($Session){ $path+="&sessionId=$Session" }
    (Call-Api Get $path $null $Token).data
}

try {
    foreach($role in @('Admin','Customer','Merchant')){$sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data}
    $admin=$sessions.Admin.accessToken;$seedCustomer=$sessions.Customer.accessToken;$merchantId=$sessions.Merchant.user.id;$seed=$sessions.Customer.user.id
    Sql @"
BEGIN;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','$email','phone','0991234568','provider_id',NULL,'auth_provider',1))).* FROM washgo.users u WHERE id='$seed';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$fixture','loyalty_points',0,'total_orders',0,'total_spent',0))).* FROM washgo.customers c WHERE user_id='$seed';
COMMIT;
"@ | Out-Null
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$email;password='Customer@123'} $null).data;$customer=$sessions.Fixture.accessToken
    $lockerId=(Call-Api Post '/locker/' @{merchantIds=@("$merchantId");name="Payment result $run";address='Fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=6} $admin).data.id
    # This suite seeds an already weighed order in SQL. Select a kg service explicitly.
    $service = (Call-Api Post '/customer/services/filter' @{page=1;take=100} $customer).list |
        Where-Object { $_.unit -eq 'kg' } | Select-Object -First 1
    if (!$service) { throw 'No active kg service available for the payment fixture.' }

    Write-Output '1. VNPay: unsigned or forged returns reveal nothing and settle nothing'
    $o1=New-PayableOrder 20000
    $p1=Start-Payment $o1 2
    Check ((Location "/payment/vnpay-return?vnp_TxnRef=$(Enc $p1.Reference)&vnp_ResponseCode=00") -eq "$Frontend/orders") 'Unsigned return must go to the order list.'
    $forged=(VnPay-Query (VnPay-Fields $p1.Reference $p1.Amount '00' '00')) -replace 'vnp_SecureHash=[0-9a-f]{4}','vnp_SecureHash=0000'
    Check ((Location "/payment/vnpay-return?$forged") -eq "$Frontend/orders") 'Bad signature must not identify the order.'

    Write-Output '2. VNPay success shown only after the verified IPN'
    $signed=VnPay-Query (VnPay-Fields $p1.Reference $p1.Amount '00' '00')
    $target="$Frontend/orders/${o1}?orderId=$o1&purpose=1&sessionId=$($p1.Session)"
    Check ((Location "/payment/vnpay-return?$signed") -eq $target) 'Signed return before IPN must be pending.'
    Check ((Sql "SELECT payment_status FROM washgo.orders WHERE id='$o1';").Trim() -eq '1') 'Browser return must not mark Paid.'
    Location "/payment/vnpay-return?$signed" | Out-Null
    Check ((Sql "SELECT count(*) FROM washgo.payment_verifications WHERE transaction_id='$($p1.Session)';").Trim() -eq '1') 'Return queues exactly one gateway query.'
    $r=Result $o1 1 $p1.Session
    Check ($r.status -eq 'Pending' -and -not $r.resultPage -and -not $r.verified -and -not $r.capabilities.canRetry -and -not $r.obligationSettled -and $r.retryAfterSeconds -gt 0) "Pending result wrong: $($r.status)."
    Assert-Status 401 Get "/payment/orders/$o1/result?purpose=1" $null $null
    Assert-Status 403 Get "/payment/orders/$o1/result?purpose=1" $null $admin
    Assert-Status 404 Get "/payment/orders/$o1/result?purpose=1" $null $seedCustomer
    Assert-Status 404 Get "/payment/orders/$o1/result?purpose=1&sessionId=$([Guid]::NewGuid())" $null $customer
    Assert-Status 400 Get "/payment/orders/$o1/result?purpose=5" $null $customer
    $ipn=Call-Api Get "/payment/vnpay-ipn?$signed" $null $null
    Check ($ipn.rspCode -eq '00') "IPN not accepted: $($ipn.rspCode)."
    Check ((Location "/payment/vnpay-return?$signed") -eq "$Frontend/payment/success?orderId=$o1&purpose=1&sessionId=$($p1.Session)") 'Verified success must go to success.'
    $r=Result $o1 1 $p1.Session
    Check ($r.status -eq 'Succeeded' -and $r.verified -and $r.obligationSettled -and $r.amount -eq $p1.Amount -and $r.recordedAt) 'Success result wrong.'
    $before=(Sql "SELECT count(*)||':'||coalesce(sum(amount),0) FROM washgo.payment_transactions WHERE order_id='$o1' AND status=2;").Trim()
    Check ((Call-Api Get "/payment/vnpay-ipn?$signed" $null $null).rspCode -in @('00','02')) 'Repeated IPN must be acknowledged.'
    Check ((Sql "SELECT count(*)||':'||coalesce(sum(amount),0) FROM washgo.payment_transactions WHERE order_id='$o1' AND status=2;").Trim() -eq $before -and $before.StartsWith('1:')) 'Repeated IPN must not record the payment twice.'
    $wrong=VnPay-Query (VnPay-Fields $p1.Reference ($p1.Amount+1) '00' '00')
    Check ((Location "/payment/vnpay-return?$wrong") -like "$Frontend/orders/${o1}?orderId=$o1*") 'Amount mismatch must go to review.'

    Write-Output '3. VNPay verified final failure -> failed, retry allowed'
    $o2=New-PayableOrder 20000
    $p2=Start-Payment $o2 2
    $failed=VnPay-Query (VnPay-Fields $p2.Reference $p2.Amount '51' '02')
    Check ((Call-Api Get "/payment/vnpay-ipn?$failed" $null $null).rspCode -eq '00') 'Failure IPN not accepted.'
    Check ((Location "/payment/vnpay-return?$failed") -eq "$Frontend/payment/failed?orderId=$o2&purpose=1&sessionId=$($p2.Session)") 'Final failure page.'
    $r=Result $o2 1 $p2.Session
    Check ($r.status -eq 'Failed' -and $r.reasonCode -eq 'GatewayDeclined' -and $r.capabilities.canRetry -and $r.verified) 'Failed result wrong.'

    Write-Output '4. MoMo: customer cancellation verified by IPN -> cancelled; old attempt keeps its own result'
    $p3=Start-Payment $o2 3
    $cancel=Momo-Fields $p3.Reference $p3.Amount 1006
    Check ((Location "/payment/momo-return?$(Momo-Query $cancel)") -eq "$Frontend/orders/${o2}?orderId=$o2&purpose=1&sessionId=$($p3.Session)") 'Before IPN a MoMo cancel return is still pending.'
    Momo-Ipn $cancel
    Check ((Location "/payment/momo-return?$(Momo-Query $cancel)") -eq "$Frontend/payment/cancelled?orderId=$o2&purpose=1&sessionId=$($p3.Session)") 'Verified MoMo cancel page.'
    $r=Result $o2 1 $p3.Session
    Check ($r.status -eq 'Cancelled' -and $r.reasonCode -eq 'CustomerCancelled' -and $r.capabilities.canRetry) 'Cancelled result wrong.'
    $old=Result $o2 1 $p2.Session
    Check ($old.status -eq 'Failed' -and $old.supersededByNewerSession) 'Older attempt must keep its own result.'
    $badMomo=Momo-Fields $p3.Reference $p3.Amount 0; $badMomo.resultCode='0'
    $badMomo.signature='00'+$badMomo.signature.Substring(2)
    Check ((Location "/payment/momo-return?$(Momo-Query $badMomo)") -eq "$Frontend/orders") 'Forged MoMo return.'

    Write-Output '5. Cancellation fee (purpose 3) via MoMo: success without paying the laundry'
    $o3=New-PayableOrder 20000
    Sql "UPDATE washgo.orders SET status=99,cancel_fee_amount=15000,cancel_fee_paid_amount=0 WHERE id='$o3';" | Out-Null
    $p4=Start-Payment $o3 3 '/payment/cancellation-fee/create-url' 3
    $paid=Momo-Fields $p4.Reference $p4.Amount 0
    Momo-Ipn $paid
    Check ((Location "/payment/momo-return?$(Momo-Query $paid)") -eq "$Frontend/payment/success?orderId=$o3&purpose=3&sessionId=$($p4.Session)") 'Fee success page with purpose 3.'
    $r=Result $o3 3 $p4.Session
    Check ($r.status -eq 'Succeeded' -and $r.obligationSettled) 'Fee result wrong.'
    Check ((Sql "SELECT payment_status FROM washgo.orders WHERE id='$o3';").Trim() -eq '1') 'A fee payment must not mark the laundry Paid.'
    Check ((Result $o3 1 $null).status -eq 'NotStarted') 'Purpose 1 has no attempt for this order.'

    Write-Output '6. Combined checkout locks the allocation and blocks overlapping sessions'
    $beforeCombined=(Call-Api Post '/finance/revenue-report' @{} $admin).data
    $o4=New-PayableOrder 130000
    Sql "UPDATE washgo.orders SET status=13,storage_workflow_version=1,accumulated_storage_fee=100000,storage_fee_total=0,moved_to_storage_at=NULL WHERE id='$o4';" | Out-Null
    $summaryPath="/payment/orders/$o4/summary"
    $checkoutPath="/payment/orders/$o4/checkout"
    Assert-Status 404 Get $summaryPath $null $seedCustomer
    $q=(Call-Api Get $summaryPath $null $customer).data
    Check ($q.outstanding -eq 230000 -and $q.obligations.Count -eq 2 -and $q.quote.canCreate) 'Expected laundry + storage in one quote.'
    Sql "UPDATE washgo.orders SET accumulated_storage_fee=110000 WHERE id='$o4';" | Out-Null
    Assert-Status 409 Post $checkoutPath @{paymentMethod=3;expectedQuoteToken=$q.quote.quoteToken} $customer
    $q=(Call-Api Get $summaryPath $null $customer).data
    $g=(Call-Api Post $checkoutPath @{paymentMethod=3;expectedQuoteToken=$q.quote.quoteToken} $customer).data
    Check ($g.amount -eq 240000 -and $g.purpose -eq 4 -and $g.sessionId) 'Expected a single combined payment.'
    Assert-Status 409 Post '/payment/create-url' @{orderId=$o4;paymentMethod=2} $customer
    Sql "UPDATE washgo.orders SET accumulated_storage_fee=120000 WHERE id='$o4';" | Out-Null
    $reused=(Call-Api Post $checkoutPath @{paymentMethod=3;expectedQuoteToken=$q.quote.quoteToken} $customer).data
    Check ($reused.sessionId -eq $g.sessionId -and $reused.amount -eq 240000) 'Fee growth must preserve the session.'
    $combinedIpn=Momo-Fields $g.transactionReference $g.amount 0
    Momo-Ipn $combinedIpn
    Momo-Ipn $combinedIpn
    $r=Result $o4 4 $g.sessionId
    Check ($r.status -eq 'Succeeded' -and $r.allocations.Count -eq 2 -and $r.summary.outstanding -eq 10000 -and -not $r.obligationSettled) 'Combined recording or current balance is incorrect.'
    Check ((Sql "SELECT status||':'||payment_status||':'||storage_fee_total::integer FROM washgo.orders WHERE id='$o4';").Trim() -eq '13:2:110000') 'Storage custody or allocation changed on duplicate callback.'
    Check ((Location "/payment/momo-return?$(Momo-Query $combinedIpn)") -eq "$Frontend/payment/success?orderId=$o4&purpose=4&sessionId=$($g.sessionId)") 'Combined success must identify purpose 4.'

    $report=(Call-Api Post '/finance/revenue-report' @{} $admin).data
    Check (($report.laundryRevenue - $beforeCombined.laundryRevenue -eq 130000) -and
        ($report.storageFeeRevenue - $beforeCombined.storageFeeRevenue -eq 110000)) 'Finance report misclassified a combined allocation.'

    Write-Output '7. Cancelled order includes only cancellation and outstanding storage'
    $o5=New-PayableOrder 200000
    Sql "UPDATE washgo.orders SET status=99,cancel_fee_amount=40000,cancel_fee_paid_amount=0,cancellation_fee_basis=200000,cancellation_fee_percent=20,overdue_fee_total=15000,storage_fee_total=5000 WHERE id='$o5';" | Out-Null
    $q=(Call-Api Get "/payment/orders/$o5/summary" $null $customer).data
    Check ($q.outstanding -eq 50000 -and $q.cancellationRatePercent -eq 20 -and $q.obligations.Count -eq 2 -and -not ($q.obligations.purpose -contains 1)) 'Cancelled order included laundry or priced fees incorrectly.'
    $g=(Call-Api Post "/payment/orders/$o5/checkout" @{paymentMethod=2;expectedQuoteToken=$q.quote.quoteToken} $customer).data
    $cancelPayment=VnPay-Query (VnPay-Fields $g.transactionReference $g.amount '00' '00')
    Check ((Call-Api Get "/payment/vnpay-ipn?$cancelPayment" $null $null).rspCode -eq '00') 'Combined cancellation IPN not accepted.'
    $r=Result $o5 4 $g.sessionId
    Check ($r.status -eq 'Succeeded' -and $r.obligationSettled -and $r.summary.outstanding -eq 0 -and $r.allocations.Count -eq 2) 'Cancelled order balance is incorrect.'
    Check ((Sql "SELECT status||':'||payment_status FROM washgo.orders WHERE id='$o5';").Trim() -eq '99:1') 'Paying fees marked laundry paid.'
    $afterCancel=(Call-Api Post '/finance/revenue-report' @{} $admin).data
    Check (($afterCancel.laundryRevenue -eq $report.laundryRevenue) -and
        ($afterCancel.storageFeeRevenue - $report.storageFeeRevenue -eq 10000) -and
        ($afterCancel.cancellationFeeRevenue - $report.cancellationFeeRevenue -eq 40000)) 'Cancellation revenue was misclassified.'
    $zero=(Call-Api Post "/payment/orders/$o5/checkout" @{paymentMethod=2;expectedQuoteToken=$r.summary.quote.quoteToken} $customer).data
    Check (-not $zero.paymentUrl -and $zero.summary.settled) 'Settled order opened a gateway again.'

    Write-Output 'PASS: three verified result routes, order fallback, ownership, legacy and combined payments, overlap protection, fees after checkout and cancelled order balances.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){ Sql "UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99); UPDATE washgo.payment_verifications SET is_active=false WHERE customer_id='$fixture'; UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$lockerId'; UPDATE washgo.lockers SET status=3,is_deleted=true WHERE id='$lockerId';" | Out-Null }
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$fixture';" | Out-Null
}
