param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY (Mock payment provider): UC 1.2.4 flow 7b. An order still unpaid 7 days after "Cho thanh toan"
# is moved to storage at the Merchant's shop by the reminder scan; the customer then pays the laundry (MoMo method
# here, mock checkout) and the storage fee, and picks the laundry up at the storage.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid()

function Run-Scan { (Call-Api Post '/notification/reminders/run' $null $admin).data }
function Count-Note($Token,$LinkId,$Type) {
    @((Call-Api Get '/notification?page=1&take=100' $null $Token).list | Where-Object { $_.linkId -eq "$LinkId" -and $_.type -eq $Type }).Count
}
function Order($Id) { (Call-Api Get "/wash-order/$Id" $null $customer).data }
function Deadline($Id) { Sql "SELECT extract(epoch FROM payment_deadline)::bigint FROM washgo.orders WHERE id='$Id';" }
function To-AwaitingPayment {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=3;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB"} $merchant | Out-Null
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null
    return $o
}
function Pay($Id, $Path='/payment/create-url') {
    $p=(Call-Api Post $Path @{orderId="$Id";paymentMethod=3} $customer).data
    Call-Api Post '/payment/callback' @{orderId="$Id";transactionReference=$p.transactionReference;success=$true} $admin | Out-Null
    return $p
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
'id','$merchantB','email','unpaid-m-$run@example.com','full_name','Unpaid Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Unpaid Smoke Laundry','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','UP_$run','name','Unpaid Smoke Package $run','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','unpaid-s-$run@example.com','full_name','Unpaid Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.M=(Call-Api Post '/auth/login' @{email="unpaid-m-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.M.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="unpaid-s-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";name="Unpaid Smoke Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id

    Write-Output '1. Awaiting payment sets a 7-day payment deadline'
    $o=To-AwaitingPayment
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $d=[long](Deadline $o.id)
    if([Math]::Abs($d-($now+7*86400)) -gt 120){throw "Payment deadline not 7 days ahead: $d vs $now"}
    $paid=To-AwaitingPayment

    Write-Output '2. Reminder 24 hours before the deadline, sent once'
    Sql "UPDATE washgo.orders SET payment_deadline=now()+interval '20 hours' WHERE id='$($o.id)';" | Out-Null
    Run-Scan | Out-Null; Run-Scan | Out-Null
    if((Count-Note $customer $o.id 5) -ne 2){throw 'Payment reminder (type 5) not sent exactly once after the awaiting-payment notice.'}
    if((Order $o.id).status -ne 8){throw 'Order moved before the deadline.'}

    Write-Output '3. Deadline passed: moved to storage at the Merchant shop (UC 1.2.4 7b)'
    Sql "UPDATE washgo.orders SET payment_deadline=now()-interval '1 minute' WHERE id IN('$($o.id)','$($paid.id)');" | Out-Null
    Pay $paid.id | Out-Null
    $scan=Run-Scan
    if($scan.sent.'payment-storage' -lt 1){throw 'Scan did not report the move to storage.'}
    $v=Order $o.id
    if($v.status -ne 13 -or $v.paymentStatus -ne 1 -or !$v.storageLocation -or $v.storageReference -ne $o.merchantOrderCode){throw "Order not moved to storage: status=$($v.status) location=$($v.storageLocation) ref=$($v.storageReference)"}
    if(@($v.timeline | Where-Object { $_.status -eq 13 }).Count -ne 1){throw 'Storage timeline missing.'}
    if((Count-Note $customer $o.id 11) -ne 1 -or (Count-Note $merchant $o.id 11) -ne 1){throw 'Customer/Merchant not notified of the move.'}
    if((Order $paid.id).status -ne 9){throw 'Paid order was moved to storage.'}
    Run-Scan | Out-Null
    if((Count-Note $customer $o.id 11) -ne 1){throw 'Second scan moved/notified again.'}

    Write-Output '4. Pay laundry (MoMo method), then the storage fee, then pick up at the storage'
    Assert-Status 400 Post "/wash-order/$($o.id)/pickup-storage" @{} $customer
    Assert-Status 400 Post '/payment/overdue-fee/create-url' @{orderId="$($o.id)";paymentMethod=3} $customer
    $p=Pay $o.id
    if($p.paymentMethod -ne 3 -or $p.transactionReference.Length -gt 50){throw 'MoMo reference too long or method wrong.'}
    $v=Order $o.id
    if($v.status -ne 13 -or $v.paymentStatus -ne 2){throw 'Paid storage order should stay in storage.'}
    if((Count-Note $customer $o.id 6) -ne 1 -or (Count-Note $merchant $o.id 6) -ne 1){throw 'Payment success not notified to Customer and Merchant.'}
    Assert-Status 400 Post '/payment/create-url' @{orderId="$($o.id)";paymentMethod=3} $customer
    Assert-Status 400 Post "/wash-order/$($o.id)/pickup-storage" @{} $customer
    $fee=Pay $o.id '/payment/overdue-fee/create-url'
    if($fee.amount -le 0){throw 'Storage fee not charged.'}
    $done=(Call-Api Post "/wash-order/$($o.id)/pickup-storage" @{} $customer).data
    if($done.status -ne 12){throw 'Storage pickup did not complete the order.'}

    Write-Output 'PASS: 7-day payment deadline, reminder 24h before (once), unpaid order moved to storage at the Merchant shop with timeline and notifications (once), paid orders untouched, laundry payment by MoMo keeps the order in storage and notifies Customer/Merchant, storage fee required before pickup, pickup completes the order.'
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
