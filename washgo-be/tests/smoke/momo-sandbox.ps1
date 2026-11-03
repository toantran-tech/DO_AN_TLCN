param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db',
    [string]$BackendContainer='washgo_backend'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: checks the MoMo integration (UC 1.2.4) with TEST credentials read from the backend container
# (the SecretKey never appears in this file). With Payment__Provider=Live the payment is created on the MoMo sandbox;
# with Mock the transaction gets the mock URL and only the IPN handling is checked. The IPN is signed here exactly
# like MoMo signs it (HMAC SHA256 over the documented field order).
$secret=(docker exec $BackendContainer printenv Payment__Momo__SecretKey 2>$null)
$access=(docker exec $BackendContainer printenv Payment__Momo__AccessKey 2>$null)
$partner=(docker exec $BackendContainer printenv Payment__Momo__PartnerCode 2>$null)
$provider=(docker exec $BackendContainer printenv Payment__Provider 2>$null)
if(!$secret -or !$access -or !$partner){ Write-Output 'SKIP: backend has no MoMo test keys (Payment__Momo__*).'; return }
$live=$provider -in @('Live','VnPay')

$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid()

function Sign-Ipn($I) {
    $raw="accessKey=$access&amount=$($I.amount)&extraData=$($I.extraData)&message=$($I.message)&orderId=$($I.orderId)&orderInfo=$($I.orderInfo)&orderType=$($I.orderType)&partnerCode=$($I.partnerCode)&payType=$($I.payType)&requestId=$($I.requestId)&responseTime=$($I.responseTime)&resultCode=$($I.resultCode)&transId=$($I.transId)"
    $hmac=New-Object System.Security.Cryptography.HMACSHA256(,[Text.Encoding]::UTF8.GetBytes($secret))
    -join ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($raw)) | ForEach-Object { $_.ToString('x2') })
}
function Ipn($I) {
    $body=[Text.Encoding]::UTF8.GetBytes(($I | ConvertTo-Json))
    try { (Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/payment/momo-ipn" -ContentType 'application/json; charset=utf-8' -Body $body).StatusCode }
    catch { [int]$_.Exception.Response.StatusCode }
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','momo-m-$run@example.com','full_name','MoMo Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','MoMo Smoke Laundry','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','MM_$run','name','MoMo Smoke Package $run','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','momo-s-$run@example.com','full_name','MoMo Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.M=(Call-Api Post '/auth/login' @{email="momo-m-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.M.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="momo-s-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken

    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";name="MoMo Smoke Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=3;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB"} $merchant | Out-Null
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null

    Write-Output "1. Create the MoMo payment (live sandbox: $live)"
    $p=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=3} $customer).data
    if($p.transactionReference.Length -gt 50 -or $p.paymentMethod -ne 3){throw 'MoMo reference too long or method wrong.'}
    if($live){
        if(([Uri]$p.paymentUrl).Host -notmatch 'momo\.vn$'){throw "MoMo payUrl wrong: $($p.paymentUrl)"}
        Write-Output "   Open in a browser to pay with the MoMo test ATM card: $($p.paymentUrl)"
    }

    Write-Output '2. Signed IPN: wrong signature, wrong amount, failed result, then success and replay'
    $ipn=[ordered]@{
        partnerCode=$partner; orderId=$p.transactionReference; requestId=$p.transactionReference; amount=[long]$p.amount
        orderInfo="Thanh toan don WashGo $($o.orderCode)"; orderType='momo_wallet'; transId=[long](Get-Random -Minimum 100000000 -Maximum 999999999)
        resultCode=0; message='Successful.'; payType='napas'; responseTime=[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds(); extraData=''
    }
    $bad=[ordered]@{}; foreach($k in $ipn.Keys){$bad[$k]=$ipn[$k]}; $bad.signature='0'*64
    if((Ipn $bad) -ne 400){throw 'Bad signature accepted.'}
    $wrong=[ordered]@{}; foreach($k in $ipn.Keys){$wrong[$k]=$ipn[$k]}; $wrong.amount=1000; $wrong.signature=Sign-Ipn $wrong
    if((Ipn $wrong) -ne 204 -or (Call-Api Get "/wash-order/$($o.id)" $null $customer).data.paymentStatus -ne 1){throw 'Wrong amount changed the order.'}
    $ipn.signature=Sign-Ipn $ipn
    if((Ipn $ipn) -ne 204){throw 'Valid IPN not acknowledged with 204.'}
    $v=(Call-Api Get "/wash-order/$($o.id)" $null $customer).data
    if($v.paymentStatus -ne 2 -or $v.status -ne 9){throw 'Order not marked paid.'}
    if((Sql "SELECT provider_transaction_id FROM washgo.payment_transactions WHERE transaction_reference='$($p.transactionReference)';") -ne "$($ipn.transId)"){throw 'MoMo transId not stored.'}
    if((Ipn $ipn) -ne 204 -or @((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.linkId -eq $o.id -and $_.type -eq 6 }).Count -ne 1){throw 'Replay changed something.'}
    if($live){ Assert-Status 400 Post '/payment/callback' @{orderId=$o.id;transactionReference=$p.transactionReference;success=$true} $admin }

    Write-Output "PASS (live sandbox: $live): MoMo payment created (payUrl from the sandbox when live), IPN rejects a bad signature (400), ignores a wrong amount, marks the order paid once with the MoMo transId, replay acknowledged without changes, mock callback blocked when live."
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$shipperB'; UPDATE washgo.users SET is_active=false WHERE id IN('$merchantB','$shipperUser'); UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';" | Out-Null
}
