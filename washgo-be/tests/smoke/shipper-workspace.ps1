param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEVELOPMENT ONLY. Dedicated users/orders/locker, no SMTP or external payment calls.
$sessions=@{}; $ids=@(); $users=@(); $ships=@(); $lockerId=$null; $jobs=@()
$run=[Guid]::NewGuid().ToString('N')
function Delivery-Body($Task,$Credential) {
    @{taskId=$Task.taskId;expectedAssignmentVersion=$Task.assignmentVersion;expectedTaskType=$Task.taskType;
      expectedRevision=$Credential.orderRevision;operationId=[Guid]::NewGuid().ToString();pinCode=$Credential.pinCode}
}
function View-Task($Token,$OrderId) {
    $page=Call-Api Post '/shipper-tasks/filter' @{page=1;take=100} $Token
    $page.list | Where-Object {$_.orderId -eq "$OrderId"} | Select-Object -First 1
}
function Check($Condition,$Message) {if(!$Condition){throw $Message}}
function Parallel-Status($Path,$Body,$Token) {
    $json=$Body|ConvertTo-Json -Depth 10 -Compress
    $localJobs=@()
    try {
        foreach($n in 1..2){
            $localJobs+=Start-Job -ScriptBlock {
                param($Url,$Json,$Token)
                try {Invoke-RestMethod -Method Post -Uri $Url -ContentType 'application/json' -Headers @{Authorization="Bearer $Token"} -Body $Json|Out-Null;200}
                catch {if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
            } -ArgumentList "$BaseUrl$Path",$json,$Token
        }
        @($localJobs|Receive-Job -Wait)
    } finally {foreach($j in $localJobs){Stop-Job $j -ErrorAction SilentlyContinue;Remove-Job $j -Force -ErrorAction SilentlyContinue}}
}

try {
    foreach($role in @('Admin','Merchant','Customer')) {
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken; $customer=$sessions.Customer.accessToken
    $merchantId=$sessions.Merchant.user.id
    foreach($i in 1..2) {
        $u=[Guid]::NewGuid();$s=[Guid]::NewGuid();$users+=$u;$ships+=$s
        $email="shipper-workspace-$run-$i@example.com"
        Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$u','email','$email','full_name','Workspace fixture $i','phone',NULL,'avatar',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).*
FROM washgo.users u WHERE email='shipper@washgo.vn';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$s','user_id','$u','merchant_id','$merchantId','status',1,'is_active',true,'is_deleted',false,'total_deliveries',0,
'create_request_id',NULL,'vehicle_type',3,'vehicle_plate',NULL))).* FROM washgo.shippers s
JOIN washgo.users u ON u.id=s.user_id WHERE u.email='shipper@washgo.vn';
"@ | Out-Null
        $sessions["S$i"]=(Call-Api Post '/auth/login' @{email=$email;password='Shipper@123'} $null).data
    }
    $s1=$sessions.S1.accessToken; $s2=$sessions.S2.accessToken
    foreach($token in @($admin,$merchant,$customer)) {Assert-Status 403 Get '/shipper-dashboard' $null $token}
    Assert-Status 401 Get '/shipper-dashboard' $null $null
    $empty=(Call-Api Get '/shipper-dashboard' $null $s1).data
    Check ($empty.performance.onTimeRate -eq $null -and $empty.completedCollectCount -eq 0) 'Empty KPI must be null.'
    Assert-Status 400 Post '/shipper-delivery/history' @{taskType=9} $s1
    Assert-Status 400 Post '/shipper-delivery/history' @{fromDate=100;toDate=99} $s1
    Assert-Status 400 Post '/shipper-delivery/history' @{fromDate=-1} $s1
    Assert-Status 400 Post '/shipper-tasks/filter' @{sortBy=@(@{field='CustomerName'})} $s1

    $profile=(Call-Api Get '/shipper-profile/' $null $s1).data
    Check ($profile.revision -and $profile.merchantPhone -and $profile.ratingCount -eq $null) 'Profile metadata missing.'
    $phone='09'+(Get-Random -Minimum 10000000 -Maximum 99999999)
    $pb=@{fullName='Workspace updated';phone=$phone;vehicleType=3;vehiclePlate=$null;
          expectedRevision=$profile.revision;operationId=[Guid]::NewGuid().ToString()}
    $updated=(Call-Api Put '/shipper-profile/' $pb $s1).data
    Check ($updated.fullName -eq 'Workspace updated' -and $updated.revision -ne $profile.revision) 'Profile update/version failed.'
    $replay=(Call-Api Put '/shipper-profile/' $pb $s1).data
    Check ($replay.revision -eq $updated.revision) 'Profile replay changed revision.'
    $operation=(Call-Api Get "/shipper-profile/operations/$($pb.operationId)" $null $s1).data
    Check ($operation.revision -eq $updated.revision) 'Profile lookup missing.'
    $stale=$pb.Clone();$stale.operationId=[Guid]::NewGuid().ToString()
    Assert-Status 409 Put '/shipper-profile/' $stale $s1
    # Stale revision checks are also exercised by two distinct operations under a real row lock below.
    $currentProfile=(Call-Api Get '/shipper-profile/' $null $s1).data
    $profileJobs=@()
    try {
        foreach($n in 1..2){
            $candidate=$pb.Clone();$candidate.expectedRevision=$currentProfile.revision;$candidate.operationId=[Guid]::NewGuid().ToString()
            $profileJobs+=Start-Job -ScriptBlock {
                param($Url,$Json,$Token)
                try{Invoke-RestMethod -Method Put -Uri $Url -ContentType 'application/json' -Headers @{Authorization="Bearer $Token"} -Body $Json|Out-Null;200}
                catch{if($_.Exception.Response){[int]$_.Exception.Response.StatusCode}else{throw}}
            } -ArgumentList "$BaseUrl/shipper-profile/",($candidate|ConvertTo-Json -Compress),$s1
        }
        $profileCodes=@($profileJobs|Receive-Job -Wait)
        Check ((($profileCodes|Sort-Object)-join ',') -eq '200,409') 'Concurrent profile overwrite was not rejected.'
    } finally {foreach($j in $profileJobs){Stop-Job $j -ErrorAction SilentlyContinue;Remove-Job $j -Force -ErrorAction SilentlyContinue}}
    $other=$pb.Clone();$other.fullName='Changed request'
    Assert-Status 409 Put '/shipper-profile/' $other $s1
    $bike=$pb.Clone();$bike.expectedRevision=(Call-Api Get '/shipper-profile/' $null $s1).data.revision;$bike.operationId=[Guid]::NewGuid().ToString();$bike.vehiclePlate='X'*100
    Assert-Status 400 Put '/shipper-profile/' $bike $s1

    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantId";code="WS_$run";name="Original locker $run";
      address='Original address';district='Test';city='Test';latitude=10.77;longitude=106.69;
      boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=3})} $admin).data
    $lockerId=$locker.id
    $slot=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots | Select-Object -First 1
    $service=(Call-Api Post '/customer/services/filter' @{page=1;take=100} $customer).list | Select-Object -First 1
    $order=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot.id;items=@(@{serviceId=$service.id;quantity=1});paymentMethod=2} $customer).data
    $id=$order.id;$ids+=$id
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$id";shipperId="$($ships[0])"} $merchant | Out-Null
    $task=View-Task $s1 $id
    Check ($task.taskId -and $task.taskLockerAddress -and $task.canCollect) 'Task preview missing.'
    $list=Call-Api Post '/wash-order/filter' @{page=1;take=100;pendingOnly=$true} $s1
    $preview=$list.list | Where-Object {$_.id -eq $id}
    Check (!$preview.shipperTaskPinCode -and !$preview.shipperTaskQrString -and $preview.taskLockerName) 'Legacy list credential leak or missing preview.'
    $cred=(Call-Api Get "/shipper-tasks/$($task.taskId)/credentials" $null $s1).data
    $body=Delivery-Body $task $cred
    Assert-Status 404 Get "/shipper-tasks/$($task.taskId)/credentials" $null $s2
    Assert-Status 400 Post "/wash-order/$id/shipper-collect" @{pinCode=$cred.pinCode} $s1
    $wrong=$body.Clone();$wrong.pinCode='000000';if($cred.pinCode -eq '000000'){$wrong.pinCode='999999'}
    Assert-Status 400 Post "/wash-order/$id/shipper-collect" $wrong $s1
    $body.expectedRevision=(Call-Api Get "/shipper-tasks/$($task.taskId)/credentials" $null $s1).data.orderRevision
    $codes=@(Parallel-Status "/wash-order/$id/shipper-collect" $body $s1)
    Check ((($codes|Sort-Object)-join ',') -eq '200,200') 'Concurrent collect failed.'
    $taken=(Call-Api Post "/wash-order/$id/shipper-collect" $body $s1).data
    Check ($taken.status -eq 3) 'Collect failed.'
    $history=Call-Api Post '/shipper-delivery/history' @{page=1;take=10} $s1
    Check ($history.total -eq 1 -and $history.list[0].snapshotSource -eq 'CapturedAtCompletion') 'Completion event missing.'
    $event=$history.list[0]
    Sql "UPDATE washgo.lockers SET name='Renamed fixture',address='New address' WHERE id='$lockerId';" | Out-Null
    $frozen=(Call-Api Get "/shipper-delivery/history/$($event.eventId)" $null $s1).data
    Check ($frozen.lockerName -eq "Original locker $run" -and $frozen.lockerAddress -like '*Original address*') 'History changed with resource.'
    Assert-Status 404 Get "/shipper-delivery/history/$($event.eventId)" $null $s2
    $summary=(Call-Api Post '/shipper-delivery/summary' @{taskType=2} $s1).data
    Check ($summary.total -eq 1 -and $summary.collectCount -eq 1) 'Summary must ignore table type.'
    $dashboard=(Call-Api Get '/shipper-dashboard?period=day' $null $s1).data
    Check ($dashboard.completedCollectCount -eq 1 -and $dashboard.performance.total -eq 1 -and $dashboard.pendingCollectCount -eq 0) 'Dashboard counts mismatch.'
    $csv=Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/shipper-delivery/export" -Headers @{Authorization="Bearer $s1"} -ContentType 'application/json' -Body '{}'
    Check ($csv.Content -like "*$($event.eventId)*" -and $csv.Content -notlike '*PinCode*') 'CSV scope/columns wrong.'

    # Local fixture-only payment state: this smoke does not call VNPay/MoMo or claim to validate a gateway.
    Sql "UPDATE washgo.orders SET status=9,payment_status=2,paid_at=now() WHERE id='$id';" | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$id";shipperId="$($ships[1])"} $merchant | Out-Null
    $replayed=Call-Api Post "/wash-order/$id/shipper-collect" $body $s1
    Check ($replayed.meta.replayed -and $replayed.data.status -eq 3) 'Replay after reassignment must preserve original result.'
    $lookup=(Call-Api Get "/shipper-tasks/$($task.taskId)/operations/$($body.operationId)" $null $s1).data
    Check ($lookup.state -eq 'succeeded' -and $lookup.result.status -eq 3) 'Delivery lookup after reassignment failed.'
    Assert-Status 409 Get "/shipper-tasks/$($task.taskId)/credentials" $null $s1
    $returnTask=View-Task $s2 $id
    $returnCred=(Call-Api Get "/shipper-tasks/$($returnTask.taskId)/credentials" $null $s2).data
    $returnBody=Delivery-Body $returnTask $returnCred
    Sql "UPDATE washgo.locker_slots SET status=4 WHERE locker_id='$lockerId';" | Out-Null
    Assert-Status 409 Post "/wash-order/$id/shipper-return" $returnBody $s2
    Check ((Sql "SELECT status FROM washgo.orders WHERE id='$id';") -eq '9') 'No-box failure changed order.'
    Sql "UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';" | Out-Null
    $returned=(Call-Api Post "/wash-order/$id/shipper-return" $returnBody $s2).data
    Check ($returned.status -eq 10 -and $returned.returnBoxNumber) 'Return failed.'
    Call-Api Post "/wash-order/$id/shipper-return" $returnBody $s2 | Out-Null
    Check ((Sql "SELECT total_deliveries FROM washgo.shippers WHERE id='$($ships[1])';") -eq '1') 'Replay counted twice.'
    $oldHistory=Call-Api Post '/shipper-delivery/history' @{} $s1
    Check ($oldHistory.total -eq 1) "Reassignment history count=$($oldHistory.total); DB=$(Sql "SELECT count(*) FROM washgo.delivery_tasks WHERE completed_by='$($users[0])';")"
    Check ((Call-Api Post '/shipper-delivery/history' @{} $s2).total -eq 1) 'Return actor history incorrect.'
    Call-Api Get '/shipper-profile/activities?page=1&take=10' $null $s1 | Out-Null
    Call-Api Get '/auth/security' $null $s1 | Out-Null
    Call-Api Get '/auth/sessions' $null $s1 | Out-Null
    Assert-Status 404 Get "/shipper-profile/operations/$($pb.operationId)" $null $s2
    Check ((Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';") -eq '1') 'Return replay changed free slot counter.'
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$($ships[0])';" | Out-Null
    Check (!(Call-Api Get '/shipper-profile/' $null $s1).data.isActive) 'Temporary rest must not hide own profile.'
    Check (!(Call-Api Get '/shipper-dashboard' $null $s1).data.isActive) 'Temporary rest must not hide own dashboard.'
    Check ((Call-Api Get "/shipper-tasks/$($task.taskId)/operations/$($body.operationId)" $null $s1).data.state -eq 'succeeded') 'Rest must preserve own operation lookup.'

    Write-Output 'PASS: Shipper workspace scopes, versioned profile/replay, credential isolation, collect/return operation lookup, reassignment, frozen history, dashboard, CSV and no-box rollback.'
}
finally {
    foreach($job in $jobs){Stop-Job $job -ErrorAction SilentlyContinue;Remove-Job $job -Force -ErrorAction SilentlyContinue}
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}}
    foreach($id in $ids){Sql "DELETE FROM washgo.order_operations WHERE order_id='$id'; DELETE FROM washgo.delivery_tasks WHERE order_id='$id'; DELETE FROM washgo.order_status_history WHERE order_id='$id'; DELETE FROM washgo.order_items WHERE order_id='$id'; DELETE FROM washgo.orders WHERE id='$id';"|Out-Null}
    if($lockerId){Sql "DELETE FROM washgo.locker_merchants WHERE locker_id='$lockerId'; DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId'; DELETE FROM washgo.lockers WHERE id='$lockerId';"|Out-Null}
    foreach($u in $users){Sql "DELETE FROM washgo.administrative_operations WHERE actor_id='$u'; DELETE FROM washgo.refresh_tokens WHERE user_id='$u'; DELETE FROM washgo.shippers WHERE user_id='$u'; DELETE FROM washgo.users WHERE id='$u';"|Out-Null}
}
