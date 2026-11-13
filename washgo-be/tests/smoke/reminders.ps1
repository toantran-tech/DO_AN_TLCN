param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: private locker/service/customer/shipper. Timestamps are moved by SQL only on
# orders created here, then a reminder scan is triggered; every reminder must arrive exactly once.
$sessions=@{}; $lockerId=$null
$kgServiceId=[Guid]::NewGuid(); $customerA=[Guid]::NewGuid()
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid()

function Run-Scan { (Call-Api Post '/notification/reminders/run' $null $admin).data }
function Count-Note($Token,$LinkId,$Type) {
    @((Call-Api Get '/notification?page=1&take=100' $null $Token).list |
        Where-Object { $_.linkId -eq "$LinkId" -and $_.type -eq $Type }).Count
}
function Expect-Count($Token,$LinkId,$Type,$Expected,$What) {
    $n=Count-Note $Token $LinkId $Type
    if($n -ne $Expected){throw "$What : expected $Expected notification(s) of type $Type, found $n."}
}
function Shift($Id,$Sql) { Sql "UPDATE washgo.orders SET $Sql WHERE id='$Id';" | Out-Null }
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2
        items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id;$seedCustomer=[Guid]$sessions.Customer.user.id;$seedShipper=[Guid]$sessions.Shipper.user.id
    $emailA="remind-$($customerA.ToString('N'))@example.com";$shipperEmail="remind-$($fixtureUser.ToString('N'))@example.com"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerA','email','$emailA','full_name','Remind Smoke Customer','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerA'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureUser','email','$shipperEmail','full_name','Remind Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipper';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipper';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','REMIND_KG_$($kgServiceId.ToString('N'))','name','Remind Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $sessions.A=(Call-Api Post '/auth/login' @{email=$emailA;password='Customer@123'} $null).data;$customer=$sessions.A.accessToken
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$shipperEmail;password='Shipper@123'} $null).data;$shipper=$sessions.Fixture.accessToken
    $boxes=1..4 | ForEach-Object { @{boxNumber="$_";size=2} }
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="RX_$([Guid]::NewGuid().ToString('N'))";name='Remind Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=$boxes
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Only Admin can trigger a scan'
    Assert-Status 401 Post '/notification/reminders/run' $null $null
    Assert-Status 403 Post '/notification/reminders/run' $null $merchant

    Write-Output '2. Deposit reminder 15 minutes before the window ends; expired orders are skipped'
    $o1=New-Order; $expired=New-Order
    Shift $o1.id "created_on=EXTRACT(EPOCH FROM NOW())::bigint-16*60,deposit_deadline=deposit_deadline-(created_on-(EXTRACT(EPOCH FROM NOW())::bigint-16*60))*interval '1 second'"
    Shift $expired.id "created_on=EXTRACT(EPOCH FROM NOW())::bigint-31*60,deposit_deadline=deposit_deadline-(created_on-(EXTRACT(EPOCH FROM NOW())::bigint-31*60))*interval '1 second'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $customer $o1.id 13 1 'Deposit reminder'
    Expect-Count $customer $expired.id 13 0 'Expired order'
    $note=@((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.linkId -eq $o1.id -and $_.type -eq 13 })[0]
    if($note.content -notmatch 'Remind Smoke Locker'){throw 'Deposit reminder missing locker.'}
    Call-Api Post "/wash-order/$($expired.id)/cancel" @{reason='Reminder smoke cleanup'} $customer | Out-Null

    Write-Output '3. Shipper collect reminder after 30 minutes; Merchant/Admin warning after collect SLA'
    Shift $o1.id "created_on=EXTRACT(EPOCH FROM NOW())::bigint-60,deposit_deadline=deposit_deadline-(created_on-(EXTRACT(EPOCH FROM NOW())::bigint-60))*interval '1 second'"
    Invoke-TestDelivery $o1.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o1.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Run-Scan | Out-Null
    Expect-Count $shipper $o1.id 33 0 'Shipper reminder too early'
    Shift $o1.id "shipper_assigned_at=NOW()-interval '31 minutes'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $shipper $o1.id 33 1 'Shipper collect reminder'
    Shift $o1.id "deposited_at=NOW()-interval '5 hours'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $merchant $o1.id 22 1 'Merchant collect SLA warning'
    Expect-Count $admin $o1.id 22 1 'Admin collect SLA warning'

    Write-Output '4. Return reminder at half of the return SLA and warning after it'
    Invoke-TestDelivery $o1.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o1.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o1.id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o1.id;newStatus=8} $merchant | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o1.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o1.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o1.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Shift $o1.id "paid_at=NOW()-interval '13 hours'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $merchant $o1.id 21 1 'Merchant return reminder'
    Expect-Count $shipper $o1.id 34 1 'Shipper return reminder'
    Shift $o1.id "paid_at=NOW()-interval '25 hours'"
    Run-Scan | Out-Null
    Expect-Count $merchant $o1.id 22 2 'Merchant return SLA warning'
    Expect-Count $admin $o1.id 22 2 'Admin late-return warning'

    Write-Output '5. Pickup reminder, overdue fee per day, storage transfer due, daily summary'
    Invoke-TestDelivery $o1.id return $shipper | Out-Null
    Run-Scan | Out-Null
    Expect-Count $customer $o1.id 14 0 'Pickup reminder too early'
    Shift $o1.id "pickup_deadline=NOW()+interval '11 hours'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $customer $o1.id 14 1 'Pickup reminder'
    Shift $o1.id "pickup_deadline=NOW()-interval '1 hour'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $customer $o1.id 15 1 'Overdue day 1'
    $day1=@((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.linkId -eq $o1.id -and $_.type -eq 15 })[0]
    if($day1.content -notmatch '10,000|10\.000'){throw 'Day-1 fee missing amount.'}
    Shift $o1.id "pickup_deadline=NOW()-interval '25 hours'"
    Run-Scan | Out-Null
    Expect-Count $customer $o1.id 15 2 'Overdue day 2'
    Shift $o1.id "pickup_deadline=NOW()-interval '73 hours'"
    Run-Scan | Out-Null; Run-Scan | Out-Null
    Expect-Count $admin $o1.id 23 1 'Admin storage transfer due'
    Expect-Count $merchant $o1.id 23 1 'Merchant storage transfer due'
    $today=(Get-Date).ToUniversalTime().AddHours(7).ToString('yyyy-MM-dd')
    if((Sql "SELECT count(*) FROM washgo.reminder_logs WHERE key='overdue-summary:$today';") -ne '1'){throw 'Daily overdue summary not recorded once.'}

    Write-Output '6. Picked-up orders are no longer reminded'
    # Exercise the legacy route only on this fixture; v1 is covered by storage-lifecycle.
    Sql "UPDATE washgo.orders SET storage_workflow_version=0 WHERE id='$($o1.id)';" | Out-Null
    Call-Api Post '/wash-order/move-to-storage' @{orderId=$o1.id;storageLocation='Kho test';storageReference='REM-1'} $admin | Out-Null
    $before=Sql "SELECT count(*) FROM washgo.reminder_logs WHERE order_id='$($o1.id)';"
    Run-Scan | Out-Null
    if((Sql "SELECT count(*) FROM washgo.reminder_logs WHERE order_id='$($o1.id)';") -ne $before){throw 'Order in storage still reminded.'}

    Write-Output 'PASS: admin-only scan, deposit/shipper/collect-SLA/return/pickup/overdue-fee/storage reminders to the right roles, exactly once per reminder, expired and finished orders skipped.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id IN('$customerA','$fixtureUser'); UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$kgServiceId';"|Out-Null
}
