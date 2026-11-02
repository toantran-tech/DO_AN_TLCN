param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: fixture Merchant + Shipper on a private locker; one order goes collect -> return -> pickup.
# Covers UC 4.3.1 (task list), 4.3.2 (task detail) and 4.3.6 (delivery history).
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid()

function Tasks([hashtable]$Body=@{}) { $b=@{page=1;take=20};foreach($k in $Body.Keys){$b[$k]=$Body[$k]};Call-Api Post '/wash-order/filter' $b $shipper }
function Delivery-History([hashtable]$Body=@{}) { $b=@{page=1;take=20};foreach($k in $Body.Keys){$b[$k]=$Body[$k]};Call-Api Post '/shipper-delivery/history' $b $shipper }
function Detail($Token) { (Call-Api Get "/wash-order/$($o.id)" $null $Token).data }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','tasks-m-$run@example.com','full_name','Tasks Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Tasks Smoke Laundry','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','TS_$run','name','Tasks Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','tasks-s-$run@example.com','full_name','Tasks Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.M=(Call-Api Post '/auth/login' @{email="tasks-m-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.M.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="tasks-s-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken

    Write-Output '1. Empty task list and history (4.3.1 2a, 4.3.6 2a)'
    $t=Tasks
    if($t.total -ne 0 -or !$t.message){throw 'Empty task message missing.'}
    $h=Delivery-History
    if($h.total -ne 0 -or !$h.message){throw 'Empty history message missing.'}
    Assert-Status 403 Post '/shipper-delivery/history' @{} $merchant

    Write-Output '2. Collect task: list, filters, detail with Merchant note (4.3.1, 4.3.2)'
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";code="TS_$run";name="Tasks Smoke Locker $run";address='12 Fixture Road';ward='Ward 1';district='Tasks District';city='Test';latitude=10.77;longitude=106.69;boxCount=3;defaultBoxSize=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Assert-Status 400 Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB";note=('x'*501)} $merchant
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB";note='Tui do mau xanh, de nhe tay'} $merchant | Out-Null
    $row=(Tasks).list[0]
    if($row.id -ne $o.id -or !$row.isNew -or $row.assignedTaskType -ne 1 -or !$row.shipperAssignedAt -or $row.customerName -ne '***'){throw 'Task row wrong.'}
    if((Tasks @{taskType=2}).total -ne 0 -or (Tasks @{taskType=1}).total -ne 1 -or (Tasks @{pendingOnly=$true}).total -ne 1){throw 'Task filters wrong.'}
    $d=Detail $shipper
    if($d.shipperNote -ne 'Tui do mau xanh, de nhe tay' -or $d.taskLockerName -ne "Tasks Smoke Locker $run" -or $d.taskLockerAddress -notmatch '12 Fixture Road' -or $d.taskBoxNumber -ne $o.boxNumber -or $d.nextAction -ne 1 -or $d.boxSize -ne 2 -or !$d.shipperTaskPinCode){throw 'Task detail wrong.'}
    if((Detail $customer).shipperNote){throw 'Customer sees the Merchant note.'}

    Write-Output '3. After collecting: no pending task, history has the collect (4.3.6)'
    $before=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-5
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    $row=(Tasks).list[0]
    if($row.isNew -or (Tasks @{pendingOnly=$true}).total -ne 0 -or (Detail $shipper).nextAction){throw 'Collected task still pending.'}
    $h=Delivery-History
    if($h.total -ne 1 -or $h.list[0].taskType -ne 1 -or $h.list[0].orderCode -ne $o.orderCode -or $h.list[0].boxNumber -ne $o.boxNumber -or $h.list[0].lockerName -ne "Tasks Smoke Locker $run"){throw 'Collect history wrong.'}

    Write-Output '4. Return task and full history with filters'
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null
    $p=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$p.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperB"} $merchant | Out-Null
    $d=Detail $shipper
    if($d.nextAction -ne 2 -or $d.taskBoxNumber -or $d.shipperNote){throw 'Return task detail wrong (note must reset per assignment).'}
    if((Tasks @{taskType=2;pendingOnly=$true}).total -ne 1){throw 'Return task not pending.'}
    Invoke-TestDelivery $o.id return $shipper | Out-Null
    $h=Delivery-History
    if($h.total -ne 2 -or $h.list[0].taskType -ne 2 -or !$h.list[0].boxNumber){throw 'Return history wrong.'}
    if((Delivery-History @{taskType=1}).total -ne 1 -or (Delivery-History @{taskType=2}).total -ne 1){throw 'History task filter wrong.'}
    if((Delivery-History @{fromDate=$before;toDate=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+5}).total -ne 2 -or (Delivery-History @{fromDate=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+3600}).total -ne 0){throw 'History time filter wrong.'}
    Invoke-TestDelivery $o.id pickup $customer | Out-Null
    if((Delivery-History).list[0].orderStatus -ne 12){throw 'History does not show the current order status.'}

    Write-Output 'PASS: empty task/history messages, 403 for Merchant on history, task row highlight/filters by task type and pending, task detail (Merchant note, locker address, box, next action), note hidden from customer and reset per assignment, collect and return recorded in history with type/time filters and current order status.'
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
