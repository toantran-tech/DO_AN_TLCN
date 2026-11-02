param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: fixture Merchant/Shipper/Customer with no orders, then one order on a private locker.
# Covers UC 1.2.6, 1.2.8 (Customer) and 2.3.1, 2.3.2 (Merchant).
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $customerB=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid()

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedCustomer=Sql "SELECT id FROM washgo.users WHERE email='customer@washgo.vn';"
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','list-m-$run@example.com','full_name','List Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','List Smoke Laundry','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','LS_$run','name','List Smoke Package','is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND is_deleted=false AND status=2 LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','list-c-$run@example.com','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','list-s-$run@example.com','full_name','List Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.M=(Call-Api Post '/auth/login' @{email="list-m-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.M.accessToken
    $sessions.C=(Call-Api Post '/auth/login' @{email="list-c-$run@example.com";password='Customer@123'} $null).data;$customer=$sessions.C.accessToken

    Write-Output '1. Empty lists (1.2.6 2a, 1.2.8 2a, 2.3.1 2a)'
    $e=Call-Api Post '/wash-order/mine' @{page=1;take=20} $customer
    if($e.total -ne 0 -or !$e.message){throw 'Customer empty message missing.'}
    $e=Call-Api Post '/wash-order/filter' @{page=1;take=20} $merchant
    if($e.total -ne 0 -or !$e.message){throw 'Merchant empty message missing.'}

    Write-Output '2. Order shows services, box size, Merchant code'
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";code="LS_$run";name="List Smoke Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2;defaultBoxSize=3} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    if($o.merchantOrderCode -ne ('MC'+$o.orderCode.Substring(2))){throw "Merchant order code wrong: $($o.merchantOrderCode)"}
    $mine=(Call-Api Post '/wash-order/mine' @{page=1;take=20} $customer).list[0]
    if((@($mine.serviceNames) -join ',') -ne 'List Smoke Package' -or $mine.boxSize -ne 3 -or $mine.isNew){throw 'Customer row wrong.'}
    $row=(Call-Api Post '/wash-order/filter' @{page=1;take=20} $merchant).list[0]
    if($row.isNew -or $row.merchantOrderCode -ne $o.merchantOrderCode -or (@($row.serviceNames) -join ',') -ne 'List Smoke Package'){throw 'Pending order flagged new or Merchant row wrong.'}

    Write-Output '3. Highlight new orders (2.3.1 step 4)'
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $row=(Call-Api Post '/wash-order/filter' @{page=1;take=20} $merchant).list[0]
    if(!$row.isNew){throw 'Deposited order not flagged new.'}
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB"} $merchant | Out-Null
    if((Call-Api Post '/wash-order/filter' @{page=1;take=20} $merchant).list[0].isNew){throw 'Assigned order still new.'}

    Write-Output '4. Filters (1.2.8 status + time range)'
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    if((Call-Api Post '/wash-order/mine' @{status=2;page=1;take=20} $customer).total -ne 1){throw 'Status filter wrong.'}
    if((Call-Api Post '/wash-order/mine' @{status=12;page=1;take=20} $customer).total -ne 0){throw 'Other status matched.'}
    if((Call-Api Post '/wash-order/mine' @{fromDate=$now-3600;toDate=$now+60;page=1;take=20} $customer).total -ne 1){throw 'Time range missed the order.'}
    if((Call-Api Post '/wash-order/mine' @{fromDate=$now+3600;page=1;take=20} $customer).total -ne 0){throw 'Future range matched.'}

    Write-Output '5. Merchant detail (2.3.2)'
    $d=(Call-Api Get "/wash-order/$($o.id)" $null $merchant).data
    if($d.boxSize -ne 3 -or $d.merchantOrderCode -ne $o.merchantOrderCode -or $d.orderQrString -ne "WASHGO:ORDER:$($o.orderCode)" -or $d.customerName -ne '***' -or $d.qrCodeString -or $d.depositPinCode -or $d.timeline.Count -lt 2){throw 'Merchant detail wrong.'}
    if((Sql "SELECT count(*) FROM washgo.orders WHERE merchant_order_code IS NULL;") -ne '0'){throw 'Old orders not backfilled.'}

    Write-Output 'PASS: empty messages for Customer/Merchant, services/box size/Merchant order code on rows, new-order highlight until a Shipper is assigned, status and time filters, Merchant detail with identification QR and hidden customer data, old orders backfilled.'
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
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$shipperB'; UPDATE washgo.users SET is_active=false WHERE id IN('$merchantB','$customerB','$shipperUser'); UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';" | Out-Null
}
