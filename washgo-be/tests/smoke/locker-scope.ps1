param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: private lockers for the seed Merchant and for fixture Merchant B, a fixture
# Shipper and service; one order is driven through deposit/collect/return/pickup for the history.
$sessions=@{}; $lockers=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperId=[Guid]::NewGuid()

function New-Locker($MerchantId,$Name) {
    $l=(Call-Api Post '/locker/' @{
        merchantId="$MerchantId";code="LS_$run`_$(Get-Random)";name=$Name
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2},@{boxNumber='3';size=2})
    } $admin).data
    $script:lockers+=[Guid]$l.id
    return $l
}
function New-Order($LockerId) {
    $l=(Call-Api Get "/customer/lockers/$LockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$LockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','locker-b-$run@example.com','full_name','Locker Smoke Merchant B','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$merchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Locker Smoke Laundry B','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$merchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','code','LOCKER_KG_$run','name','Locker Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','locker-shipper-$run@example.com','full_name','Locker Smoke Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperId','user_id','$shipperUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="locker-b-$run@example.com";password='Merchant@123'} $null).data;$merchantBTok=$sessions.B.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="locker-shipper-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken

    Write-Output '1. Merchant with no locker (UC 2.5.1 2a)'
    $none=Call-Api Post '/locker/filter' @{page=1;take=50} $merchantBTok
    if($none.total -ne 0 -or !$none.message){throw 'Empty locker message missing.'}

    Write-Output '2. Lists are scoped to the Merchant; other lockers are hidden'
    $la=New-Locker $merchantId 'Locker Smoke A'
    $lb=New-Locker $merchantB 'Locker Smoke B'
    $listA=Call-Api Post '/locker/filter' @{page=1;take=100;keyword='Locker Smoke'} $merchant
    if(@($listA.list | Where-Object { $_.id -eq $la.id }).Count -ne 1 -or @($listA.list | Where-Object { $_.id -eq $lb.id }).Count){throw 'Merchant A list scope wrong.'}
    $listB=Call-Api Post '/locker/filter' @{page=1;take=100} $merchantBTok
    if($listB.total -ne 1 -or $listB.list[0].id -ne $lb.id){throw 'Merchant B list scope wrong.'}
    if((Call-Api Post '/locker/filter' @{page=1;take=100;keyword='Locker Smoke'} $admin).total -lt 2){throw 'Admin cannot see all lockers.'}
    Assert-Status 404 Get "/locker/$($lb.id)" $null $merchant
    Assert-Status 404 Get "/locker/$($lb.id)/usage-history" $null $merchant
    Assert-Status 403 Get "/locker/$($la.id)" $null $customer

    Write-Output '3. Read-only for Merchants (UC 2.5)'
    Assert-Status 403 Post '/locker/' @{merchantId="$merchantId";code="X_$run";name='x';address='x';district='x';city='x';boxes=@(@{boxNumber='1';size=2})} $merchant
    Assert-Status 403 Put '/locker/' @{id=$la.id;merchantId="$merchantId";code=$la.code;name='Renamed';address='x';district='x';city='x';status=1} $merchant
    Assert-Status 403 Put '/locker/box-status' @{lockerId=$la.id;boxId=$la.boxes[0].id;newStatus=4} $merchant

    Write-Output '4. Box states highlight occupied boxes (UC 2.5.2)'
    $o1=New-Order $la.id
    $o2=New-Order $la.id
    Invoke-TestDelivery $o2.id deposit $customer | Out-Null
    $detail=(Call-Api Get "/locker/$($la.id)" $null $merchant).data
    $occupied=@($detail.boxes | Where-Object { $_.hasItems })
    $reserved=@($detail.boxes | Where-Object { $_.status -eq 3 })
    if($occupied.Count -ne 1 -or $occupied[0].id -ne $o2.boxId -or $reserved.Count -ne 1 -or $reserved[0].hasItems){throw 'Box states wrong.'}

    Write-Output '5. Usage history with put-in and taken-out times (UC 2.5.3)'
    $hEmpty=Call-Api Get "/locker/$($lb.id)/usage-history" $null $merchantBTok
    if($hEmpty.total -ne 0 -or !$hEmpty.message){throw 'Unused locker message missing.'}
    Call-Api Post "/wash-order/$($o1.id)/cancel" @{reason='Locker smoke cleanup'} $customer | Out-Null
    $id=$o2.id
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=8} $merchant | Out-Null
    $p=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$p.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $id return $shipper | Out-Null
    $mid=Call-Api Get "/locker/$($la.id)/usage-history" $null $merchant
    $ret=@($mid.list | Where-Object { $_.orderId -eq $id -and $_.usageType -eq 2 })
    if($ret.Count -ne 1 -or !$ret[0].putInAt -or $ret[0].takenOutAt){throw 'Return row before pickup wrong.'}
    Invoke-TestDelivery $id pickup $customer | Out-Null
    $h=Call-Api Get "/locker/$($la.id)/usage-history?page=1&take=50" $null $merchant
    $rows=@($h.list | Where-Object { $_.orderId -eq $id })
    $dep=$rows | Where-Object { $_.usageType -eq 1 }; $back=$rows | Where-Object { $_.usageType -eq 2 }
    if($rows.Count -ne 2 -or !$dep.putInAt -or !$dep.takenOutAt -or !$back.putInAt -or !$back.takenOutAt -or $dep.orderStatus -ne 12){throw "History rows wrong: $($rows | ConvertTo-Json -Compress)"}
    if([datetime]$dep.takenOutAt -lt [datetime]$dep.putInAt -or [datetime]$back.takenOutAt -lt [datetime]$back.putInAt){throw 'History times out of order.'}
    if(@($h.list | Where-Object { $_.orderId -eq $o1.id }).Count){throw 'Never-deposited order shown in history.'}
    $future=[DateTimeOffset]::UtcNow.AddDays(1).ToUnixTimeSeconds()
    if((Call-Api Get "/locker/$($la.id)/usage-history?fromDate=$future" $null $merchant).total -ne 0){throw 'Date filter wrong.'}
    if((Call-Api Get "/locker/$($la.id)/usage-history" $null $admin).total -ne $h.total){throw 'Admin history differs.'}

    Write-Output 'PASS: locker list/detail scoped to Merchant, empty messages, Merchant read-only, occupied box highlight, usage history with times/filters and scope.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($lockerId in $lockers){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$shipperId'; UPDATE washgo.users SET is_active=false WHERE id IN('$merchantB','$shipperUser'); UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';"|Out-Null
}
