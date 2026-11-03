param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: private locker/service/customers/shipper; the order goes through the
# real action endpoints and each step is checked for the expected notifications.
$sessions=@{}; $lockerId=$null
$kgServiceId=[Guid]::NewGuid(); $customerA=[Guid]::NewGuid(); $customerB=[Guid]::NewGuid()
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid()

function Notes($Token) { @((Call-Api Get '/notification?page=1&take=100' $null $Token).list) }
function Find-Note($Token,$LinkId,$Type) {
    @(Notes $Token | Where-Object { $_.linkId -eq "$LinkId" -and $_.type -eq $Type })
}
function Expect-Note($Token,$LinkId,$Type,$Who) {
    $found=@(Find-Note $Token $LinkId $Type)
    if($found.Count -ne 1){throw "Expected one notification type $Type for $Who, found $($found.Count)."}
    return $found[0]
}
function Unread($Token) { [int](Call-Api Get '/notification/unread-count' $null $Token).data }

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id;$seedCustomer=[Guid]$sessions.Customer.user.id;$seedShipper=[Guid]$sessions.Shipper.user.id
    Call-Api Post '/auth/logout' @{refreshToken=$sessions.Customer.refreshToken} $sessions.Customer.accessToken | Out-Null
    $sessions.Remove('Customer')
    $emailA="notify-$($customerA.ToString('N'))@example.com";$emailB="notify-$($customerB.ToString('N'))@example.com"
    $shipperEmail="notify-$($fixtureUser.ToString('N'))@example.com"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerA','email','$emailA','full_name','Notify Smoke Customer A','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerA'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','$emailB','full_name','Notify Smoke Customer B','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureUser','email','$shipperEmail','full_name','Notify Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipper';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipper';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','NOTIFY_KG_$($kgServiceId.ToString('N'))','name','Notify Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $sessions.A=(Call-Api Post '/auth/login' @{email=$emailA;password='Customer@123'} $null).data;$customer=$sessions.A.accessToken
    $sessions.B=(Call-Api Post '/auth/login' @{email=$emailB;password='Customer@123'} $null).data;$tokenB=$sessions.B.accessToken
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$shipperEmail;password='Shipper@123'} $null).data;$shipper=$sessions.Fixture.accessToken
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="NX_$([Guid]::NewGuid().ToString('N'))";name='Notify Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Authorization and empty inbox (UC 5.1)'
    Assert-Status 401 Get '/notification' $null $null
    $empty=Call-Api Get '/notification' $null $customer
    if($empty.total -ne 0 -or !$empty.message){throw 'Empty inbox message missing.'}

    Write-Output '2. Order created: Customer notified in real time over SignalR (no Merchant until the Admin assigns one)'
    $listenerOut=Join-Path ([IO.Path]::GetTempPath()) "washgo-signalr-$([Guid]::NewGuid().ToString('N')).txt"
    $listenerScript=Join-Path $PSScriptRoot 'signalr-listen.cjs'
    $listener=Start-Process node -ArgumentList "`"$listenerScript`"",$BaseUrl,$customer,'20000' -NoNewWindow -PassThru -RedirectStandardOutput $listenerOut
    $deadline=(Get-Date).AddSeconds(15)
    while(!(Test-Path $listenerOut) -or !((Get-Content $listenerOut -Raw) -match 'CONNECTED')){
        if((Get-Date) -gt $deadline -or $listener.HasExited){throw "SignalR listener did not connect: $(Get-Content $listenerOut -Raw)"}
        Start-Sleep -Milliseconds 200
    }
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $order=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2
        items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
    $id=$order.id
    if(!$listener.WaitForExit(20000)){$listener.Kill();throw 'SignalR listener timed out.'}
    $pushed=(Get-Content $listenerOut | Where-Object {$_ -like '{*'} | Select-Object -First 1) | ConvertFrom-Json
    Remove-Item $listenerOut -ErrorAction SilentlyContinue
    if($pushed.type -ne 1 -or $pushed.linkId -ne $id -or $pushed.linkType -ne 'order'){throw 'Real-time payload wrong.'}
    $created=Expect-Note $customer $id 1 'Customer'
    if($created.isRead -or $created.content -notmatch $order.orderCode -or $created.content -notmatch $order.boxNumber){throw 'Order-created content wrong.'}
    if(@(Find-Note $merchant $id 20).Count){throw 'Merchant notified before the Admin assigned the order.'}
    if((Unread $customer) -ne 1){throw 'Unread count wrong after order creation.'}

    Write-Output '3. Failed actions do not notify; deposit notifies Customer and Admin, assignment notifies the Merchant'
    Assert-Status 400 Post "/wash-order/$id/deposit" @{pinCode='000000'} $customer
    if(@(Find-Note $customer $id 2).Count){throw 'Rejected deposit produced a notification.'}
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Expect-Note $customer $id 2 'Customer' | Out-Null
    Expect-Note $admin $id 43 'Admin' | Out-Null
    Expect-Note $merchant $id 20 'Merchant assigned by Admin' | Out-Null

    Write-Output '4. Shipper task, collection and laundry steps'
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Expect-Note $shipper $id 30 'Shipper' | Out-Null
    Invoke-TestDelivery $id collect $shipper | Out-Null
    Expect-Note $shipper $id 31 'Shipper' | Out-Null
    Expect-Note $customer $id 3 'Customer' | Out-Null
    Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=4} $merchant | Out-Null
    $washing=Expect-Note $customer $id 4 'Customer'
    if($washing.content -notmatch 'gi'){throw 'Processing content wrong.'}
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=8} $merchant | Out-Null
    $due=Expect-Note $customer $id 5 'Customer'
    if($due.content -notmatch '100,000|100\.000'){throw 'Payment reminder missing amount.'}

    Write-Output '5. Payment failed then succeeded'
    $p1=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer).data
    Assert-Status 400 Post '/payment/callback' @{orderId=$id;transactionReference=$p1.transactionReference;success=$false} $admin
    Expect-Note $customer $id 7 'Customer' | Out-Null
    $p2=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$p2.transactionReference;success=$true} $admin | Out-Null
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$p2.transactionReference;success=$true} $admin | Out-Null
    Expect-Note $customer $id 6 'Customer' | Out-Null

    Write-Output '6. Return to locker and pickup'
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $id return $shipper | Out-Null
    Expect-Note $shipper $id 32 'Shipper' | Out-Null
    Expect-Note $merchant $id 32 'Merchant' | Out-Null
    $ready=Expect-Note $customer $id 8 'Customer'
    if($ready.content -notmatch 'Notify Smoke Locker'){throw 'Ready-for-pickup missing locker.'}
    Invoke-TestDelivery $id pickup $customer | Out-Null
    Expect-Note $customer $id 9 'Customer' | Out-Null
    Expect-Note $admin $id 44 'Admin' | Out-Null

    Write-Output '7. Cancellation notifies Customer (and the Merchant once assigned)'
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o2=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
    Call-Api Post "/wash-order/$($o2.id)/cancel" @{reason='Notify smoke cancel'} $customer | Out-Null
    $cancel=Expect-Note $customer $o2.id 10 'Customer'
    if($cancel.content -notmatch 'Notify smoke cancel'){throw 'Cancellation reason missing.'}
    if(@(Find-Note $merchant $o2.id 10).Count){throw 'Unassigned Merchant notified of the cancellation.'}
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o3=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o3.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o3.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Call-Api Post "/wash-order/$($o3.id)/cancel" @{reason='Notify smoke cancel deposited'} $customer | Out-Null
    Expect-Note $shipper $o3.id 10 'Shipper with voided collect task' | Out-Null

    Write-Output '8. Ownership, mark read, read-all (UC 5.2)'
    $target=Expect-Note $customer $id 1 'Customer'
    Assert-Status 404 Post "/notification/$($target.id)/read" $null $tokenB
    Assert-Status 404 Delete "/notification/$($target.id)" $null $tokenB
    if(@(Notes $tokenB).Count -ne 0){throw 'Another customer sees notifications.'}
    $before=Unread $customer
    $read=(Call-Api Post "/notification/$($target.id)/read" $null $customer).data
    if(!$read.isRead -or !$read.readAt -or (Unread $customer) -ne $before-1){throw 'Mark read failed.'}
    $again=(Call-Api Post "/notification/$($target.id)/read" $null $customer).data
    if($again.readAt -ne $read.readAt){throw 'Re-reading changed readAt.'}
    $all=Call-Api Post '/notification/read-all' $null $customer
    if($all.data -ne $before-1 -or (Unread $customer) -ne 0 -or !$all.message){throw 'Read-all failed.'}
    if((Call-Api Get '/notification?unreadOnly=true' $null $customer).total -ne 0){throw 'Unread filter wrong.'}

    Write-Output '9. Delete one and delete all (UC 5.3)'
    $total=(Call-Api Get '/notification' $null $customer).total
    Call-Api Delete "/notification/$($target.id)" $null $customer | Out-Null
    if((Call-Api Get '/notification' $null $customer).total -ne $total-1){throw 'Delete one failed.'}
    Assert-Status 404 Post "/notification/$($target.id)/read" $null $customer
    $cleared=Call-Api Delete '/notification' $null $customer
    if($cleared.data -ne $total-1){throw 'Delete-all count wrong.'}
    $after=Call-Api Get '/notification' $null $customer
    if($after.total -ne 0 -or !$after.message){throw 'Inbox not empty after delete-all.'}

    Write-Output 'PASS: SignalR real-time push, order lifecycle notifications for Customer/Merchant/Shipper/Admin, payment fail/success, cancel, no notification on failed action, ownership, read/read-all, delete/delete-all.'
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
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id IN('$customerA','$customerB','$fixtureUser'); UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$kgServiceId';"|Out-Null
}
