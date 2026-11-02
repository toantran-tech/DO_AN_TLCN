param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db',
    [string]$BackendContainer='washgo_backend'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: checks the VNPay sandbox integration (UC 1.2.4) with the merchant's TEST credentials.
# Run only when the backend uses Payment__Provider=VnPay; the HashSecret is read from the backend container
# so it never appears in this file. The IPN call is signed here exactly like VNPay signs it.
$secret=(docker exec $BackendContainer printenv Payment__VnPay__HashSecret 2>$null)
$tmn=(docker exec $BackendContainer printenv Payment__VnPay__TmnCode 2>$null)
$provider=(docker exec $BackendContainer printenv Payment__Provider 2>$null)
if($provider -ne 'VnPay' -or !$secret -or !$tmn){ Write-Output 'SKIP: backend is not running with Payment__Provider=VnPay.'; return }

$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid()

function Sign([System.Collections.IDictionary]$Fields) {
    $keys=@($Fields.Keys | Where-Object { $_ -like 'vnp_*' -and $_ -ne 'vnp_SecureHash' -and $_ -ne 'vnp_SecureHashType' } | Sort-Object -CaseSensitive)
    $query=($keys | ForEach-Object { [Net.WebUtility]::UrlEncode($_)+'='+[Net.WebUtility]::UrlEncode([string]$Fields[$_]) }) -join '&'
    $hmac=New-Object System.Security.Cryptography.HMACSHA512(,[Text.Encoding]::UTF8.GetBytes($secret))
    $hash=-join ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($query)) | ForEach-Object { $_.ToString('x2') })
    return "$query&vnp_SecureHash=$hash"
}
function Ipn($Query) { Invoke-RestMethod -Method Get -Uri "$BaseUrl/payment/vnpay-ipn?$Query" }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','vnpay-m-$run@example.com','full_name','VNPay Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','VNPay Smoke Laundry','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','VP_$run','name','VNPay Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','vnpay-s-$run@example.com','full_name','VNPay Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.M=(Call-Api Post '/auth/login' @{email="vnpay-m-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.M.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="vnpay-s-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken

    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";code="VP_$run";name="VNPay Smoke Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB"} $merchant | Out-Null
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null

    Write-Output '1. Payment URL signed with the sandbox merchant'
    $p=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    $uri=[Uri]$p.paymentUrl
    if($uri.Host -ne 'sandbox.vnpayment.vn' -or $p.paymentUrl -notmatch "vnp_TmnCode=$tmn" -or $p.paymentUrl -notmatch 'vnp_SecureHash=[0-9a-f]{128}$'){throw "Payment URL wrong: $($p.paymentUrl)"}
    $fields=[ordered]@{}
    foreach($pair in $uri.Query.TrimStart('?').Split('&')){ $kv=$pair.Split('=',2); $fields[[Net.WebUtility]::UrlDecode($kv[0])]=[Net.WebUtility]::UrlDecode($kv[1]) }
    $resigned=Sign $fields
    if(($uri.Query.TrimStart('?')) -ne $resigned){throw 'Backend signature differs from the VNPay algorithm.'}
    Write-Output "   Open in a browser to pay with the NCB test card: $($p.paymentUrl)"

    Write-Output '2. VNPay sandbox accepts the URL (no merchant/signature error page)'
    $page=Invoke-WebRequest -UseBasicParsing -Uri $p.paymentUrl -MaximumRedirection 5
    $final=$page.BaseResponse.ResponseUri.AbsoluteUri
    if($final -match '(?i)error' -or $page.Content -match '(?i)Sai ch\S* k|Invalid signature|Website n\S* ch\S*a \S*\s?\S*c ph\S* duy\S*t'){throw "VNPay rejected the URL: $final"}
    Write-Output "   VNPay page: $final"

    Write-Output '3. Signed IPN: wrong signature, wrong amount, success, replay'
    $ipn=[ordered]@{
        vnp_Amount=$fields.vnp_Amount; vnp_BankCode='NCB'; vnp_BankTranNo="VNP$run"; vnp_CardType='ATM'
        vnp_OrderInfo=$fields.vnp_OrderInfo; vnp_PayDate=(Get-Date).ToString('yyyyMMddHHmmss'); vnp_ResponseCode='00'
        vnp_TmnCode=$tmn; vnp_TransactionNo="$(Get-Random -Minimum 10000000 -Maximum 99999999)"; vnp_TransactionStatus='00'; vnp_TxnRef=$fields.vnp_TxnRef
    }
    $good=Sign $ipn
    if((Ipn ($good -replace 'vnp_SecureHash=[0-9a-f]+',('vnp_SecureHash='+('0'*128)))).RspCode -ne '97'){throw 'Bad signature accepted.'}
    $wrong=[ordered]@{}; foreach($k in $ipn.Keys){$wrong[$k]=$ipn[$k]}; $wrong.vnp_Amount='100'
    if((Ipn (Sign $wrong)).RspCode -ne '04'){throw 'Wrong amount accepted.'}
    $r=Ipn $good
    if($r.RspCode -ne '00'){throw "IPN failed: $($r.RspCode) $($r.Message)"}
    if((Call-Api Get "/wash-order/$($o.id)" $null $customer).data.paymentStatus -ne 2){throw 'Order not marked paid.'}
    if((Ipn $good).RspCode -ne '02'){throw 'IPN replay not detected.'}
    Assert-Status 400 Post '/payment/callback' @{orderId=$o.id;transactionReference=$p.transactionReference;success=$true} $admin

    Write-Output 'PASS: VNPay sandbox URL uses the TEST merchant and the documented HMAC-SHA512 signature, sandbox accepts it, IPN rejects bad signature/amount, marks the order paid once, replay answered 02, mock callback blocked.'
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
