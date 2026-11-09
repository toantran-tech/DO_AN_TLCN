param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: locker L has Merchants A (priority 1) and B (priority 2); locker L2 has only A; C serves no locker.
# Orders are created without a Merchant and the Admin assigns one after the deposit (UC 3.3).
$NoAutoAssign=$true
$sessions=@{}; $lockerIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$mA=[Guid]::NewGuid();$mB=[Guid]::NewGuid();$mC=[Guid]::NewGuid()

function Merchant-Sql($Id,$Name,$Lat,$Rating) {
@"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$Id','email','assign-$Id@example.com','full_name','$Name','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$Id','business_name','$Name','tax_code',NULL,'status',1,'latitude',$Lat,'longitude',105.0,'service_radius_km',5,'rating',$Rating,'max_capacity',50))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@
}
function New-Order($LockerId) {
    $l=(Call-Api Get "/customer/lockers/$LockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$LockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    return $o
}
function Waiting([hashtable]$Body=@{}) { $b=@{district="Assign $run";take=50};foreach($k in $Body.Keys){$b[$k]=$Body[$k]};Call-Api Post '/order-assignment/filter' $b $admin }
function Order($Id) { (Call-Api Get "/wash-order/$Id" $null $admin).data }
function Login-Merchant($Id) { $s=(Call-Api Post '/auth/login' @{email="assign-$Id@example.com";password='Merchant@123'} $null).data; $sessions["m$Id"]=$s; $s.accessToken }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    Sql ((Merchant-Sql $mA "Assign A $run" 12.001 4.5) + (Merchant-Sql $mB "Assign B $run" 12.002 4.5) + (Merchant-Sql $mC "Assign C $run" 12.003 5.0)) | Out-Null
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id
    $L=(Call-Api Post '/locker/' @{merchantIds=@("$mA","$mB");code="ASSIGN_$run";name="Assign Locker $run";address='Private development fixture';district="Assign $run";city='Test';latitude=12.0;longitude=105.0;boxCount=4} $admin).data
    $L2=(Call-Api Post '/locker/' @{merchantIds=@("$mA");code="ASSIGN2_$run";name="Assign Locker Two $run";address='Private development fixture';district="Assign $run";city='Test';latitude=12.01;longitude=105.0;boxCount=2} $admin).data
    $lockerIds=@($L.id,$L2.id)
    $tokA=Login-Merchant $mA; $tokB=Login-Merchant $mB

    Write-Output '1. Locker with several Merchants (UC 3.4 / 2.5)'
    if((@($L.merchants | ForEach-Object { "$($_.priority):$($_.merchantId)" }) -join ',') -ne "1:$mA,2:$mB"){throw 'Locker Merchants/priorities wrong.'}
    if((Call-Api Post '/locker/filter' @{page=1;take=20} $tokB).list.id -notcontains $L.id){throw 'Second Merchant does not see the shared locker.'}
    Assert-Status 400 Post '/locker/' @{merchantIds=@("$mA","$mA");name="Dup $run";address='x';district='x';city='x';latitude=12.1;longitude=105.1;boxCount=1} $admin

    Write-Output '2. New order has no Merchant; waiting list after deposit (UC 3.3.1)'
    $empty=Waiting
    if($empty.total -ne 0 -or !$empty.message){throw 'Empty waiting message missing.'}
    $o1=New-Order $L.id
    if($null -ne (Order $o1.id).merchantId){throw 'Order got a Merchant before the Admin assigned it.'}
    if((Call-Api Post '/wash-order/filter' @{page=1;take=20} $tokA).total -ne 0){throw 'Merchant sees an unassigned order.'}
    Assert-Status 403 Post '/order-assignment/filter' @{} $tokA
    Assert-Status 400 Put '/wash-order/assign-shipper' @{orderId=$o1.id;shipperId="$([Guid]::NewGuid())"} $admin
    $o2=New-Order $L.id; $o3=New-Order $L.id
    Sql "UPDATE washgo.orders SET deposited_at=deposited_at - interval '2 hours' WHERE id='$($o2.id)';" | Out-Null
    $w=Waiting
    if($w.total -ne 3 -or $w.list[0].id -ne $o2.id -or !$w.list[0].isOverdue -or $w.list[0].waitingMinutes -lt 119){throw 'Waiting list order/overdue wrong.'}
    $row=$w.list | Where-Object { $_.id -eq $o1.id }
    if($row.isOverdue -or $row.lockerName -ne "Assign Locker $run" -or $row.boxSize -ne 2 -or $row.services.Count -lt 1){throw 'Waiting row fields wrong.'}
    if((Waiting @{overdueOnly=$true}).total -ne 1){throw 'Overdue filter wrong.'}

    Write-Output '3. Candidates = Merchants of the locker (UC 3.3.2 step 4)'
    $c=(Call-Api Get "/order-assignment/$($o1.id)/candidates" $null $admin).data
    $ids=@($c.merchants | ForEach-Object { $_.merchantId })
    if($ids.Count -ne 2 -or $ids -notcontains "$mA" -or $ids -notcontains "$mB" -or $ids -contains "$mC"){throw "Candidates wrong: $ids"}
    if($c.merchants[0].merchantId -ne "$mA" -or $c.merchants[0].priority -ne 1 -or $c.merchants[0].maxCapacity -ne 50 -or $null -eq $c.merchants[0].distanceKm){throw 'Priority-1 Merchant should rank first.'}

    Write-Output '4. Manual assignment (UC 3.3.2)'
    Assert-Status 400 Post "/order-assignment/$($o1.id)/assign" @{merchantId="$mC"} $admin
    Assert-Status 404 Post "/order-assignment/$([Guid]::NewGuid())/assign" @{merchantId="$mA"} $admin
    $ok=Call-Api Post "/order-assignment/$($o1.id)/assign" @{merchantId="$mB"} $admin
    if(!$ok.message -or $ok.data.merchantId -ne "$mB"){throw 'Assign failed.'}
    if((Order $o1.id).merchantId -ne "$mB" -or (Call-Api Get "/wash-order/$($o1.id)" $null $tokB).data.id -ne $o1.id){throw 'Merchant B does not own the order.'}
    Assert-Status 404 Get "/wash-order/$($o1.id)" $null $tokA
    if(@((Call-Api Get '/notification?take=50' $null $tokB).list | Where-Object { $_.type -eq 20 -and $_.linkId -eq $o1.id }).Count -ne 1){throw 'Merchant B not notified.'}
    Assert-Status 400 Post "/order-assignment/$($o1.id)/assign" @{merchantId="$mA"} $admin
    if((Waiting).total -ne 2){throw 'Assigned order still waiting.'}

    Write-Output '5. AI suggestion respects capacity; batch confirm is all-or-nothing (UC 3.3.3)'
    Sql "UPDATE washgo.merchants SET max_capacity=1 WHERE user_id='$mA';" | Out-Null
    $s=(Call-Api Post '/order-assignment/auto-suggest' @{orderIds=@($o2.id,$o3.id)} $admin).data
    if($s[0].suggested.merchantId -ne "$mA" -or $s[1].suggested.merchantId -ne "$mB"){throw "Suggestions wrong: $($s[0].suggested.businessName), $($s[1].suggested.businessName)"}
    Assert-Status 400 Post '/order-assignment/auto-suggest' @{orderIds=@($o1.id)} $admin
    Assert-Status 400 Post '/order-assignment/auto-assign' @{assignments=@(@{orderId=$o2.id;merchantId="$mA"},@{orderId=$o3.id;merchantId="$mC"})} $admin
    if($null -ne (Order $o2.id).merchantId){throw 'Failed batch was not rolled back.'}
    $auto=Call-Api Post '/order-assignment/auto-assign' @{assignments=@(@{orderId=$o2.id;merchantId="$mA"},@{orderId=$o3.id;merchantId="$mB"})} $admin
    if(!$auto.message -or $auto.data.Count -ne 2 -or (Order $o2.id).merchantId -ne "$mA" -or (Order $o3.id).merchantId -ne "$mB"){throw 'Auto assignment not applied.'}

    Write-Output '6. No active Merchant for the locker'
    $o4=New-Order $L2.id
    Sql "UPDATE washgo.merchants SET status=3 WHERE user_id='$mA';" | Out-Null
    $c4=Call-Api Get "/order-assignment/$($o4.id)/candidates" $null $admin
    if($c4.data.merchants.Count -ne 0 -or !$c4.message){throw 'Empty candidate message missing.'}
    $none=(Call-Api Post '/order-assignment/auto-suggest' @{orderIds=@($o4.id)} $admin).data
    if($none[0].suggested -or !$none[0].message){throw 'No-match suggestion wrong.'}
    Assert-Status 400 Post "/order-assignment/$($o4.id)/assign" @{merchantId="$mA"} $admin
    Assert-Status 404 Get "/customer/lockers/$($L2.id)" $null $customer
    if((Call-Api Get "/customer/lockers/$($L.id)" $null $customer).data.id -ne $L.id){throw 'Locker hidden although Merchant B is active.'}

    Write-Output 'PASS: locker with several Merchants and priorities, order created without Merchant, waiting list with overdue highlight/filter, candidates = locker Merchants, manual assign + ownership + notification, AI suggestion with capacity, all-or-nothing batch, no-active-Merchant messages, customer lockers need an active Merchant.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($lockerId in $lockerIds){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.users SET is_active=false WHERE id IN('$mA','$mB','$mC'); UPDATE washgo.merchants SET status=3 WHERE user_id IN('$mA','$mB','$mC');" | Out-Null
}
