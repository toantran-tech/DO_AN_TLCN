param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Merchant reads the task history of one shipper (assigned / completed / reassigned / cancelled).
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function Tasks($Id,$Query){ Call-Api Post "/shipper/$Id/tasks" ($Query + @{page=1;take=20}) $team }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $x=New-FixtureShipper $team "Task $run X" $run
    $y=New-FixtureShipper $team "Task $run Y" $run
    $sessions.X=(Call-Api Post '/auth/login' @{email=$x.email;password=(Read-TempPassword $x.email)} $null).data
    $locker=(Call-Api Post '/locker/' @{merchantId="$teamId";name="Task Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $orders=@()
    foreach($i in 0..1){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $orders+=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $orders[$i].id deposit $customer | Out-Null
    }

    # o1: X, then Y, then X again (two reassignments), X collects. o2: X, then the Customer cancels.
    foreach($sid in @($x.id,$y.id,$x.id)){ Call-Api Put '/wash-order/assign-shipper' @{orderId=$orders[0].id;shipperId="$sid"} $team | Out-Null }
    $open=(Tasks $x.id @{status=1}).list
    if(@($open).Count -ne 1 -or $open[0].orderId -ne $orders[0].id -or !$open[0].assignedAt -or $open[0].completedAt){throw 'Open task wrong.'}
    Invoke-TestDelivery $orders[0].id collect $sessions.X.accessToken | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$orders[1].id;shipperId="$($x.id)"} $team | Out-Null
    Call-Api Post "/wash-order/$($orders[1].id)/cancel" @{reason="Test task history $run"} $customer | Out-Null

    Write-Output '1. History with real milestones per status'
    $h=Tasks $x.id @{}
    $codes=(@($h.list) | ForEach-Object { "$($_.orderCode):$([int]$_.status)" }) -join ','
    if($h.total -ne 3 -or $codes -ne "$($orders[1].orderCode):4,$($orders[0].orderCode):2,$($orders[0].orderCode):3"){throw "History wrong: $codes"}
    $done=$h.list[1]; $moved=$h.list[2]; $cancelled=$h.list[0]
    if(!$done.assignedAt -or !$done.completedAt -or $done.endedAt -or [int]$done.taskType -ne 1 -or $done.lockerName -ne "Task Locker $run" -or !$done.boxNumber){throw 'Completed task fields wrong.'}
    if(!$moved.endedAt -or $moved.completedAt -or !$cancelled.endedAt -or $cancelled.completedAt -or [int]$cancelled.orderStatus -ne 99){throw 'Ended task fields wrong.'}
    if([datetime]$done.completedAt -lt [datetime]$done.assignedAt){throw 'Completed before assigned.'}

    Write-Output '2. Filters, paging, other shipper'
    if((Tasks $x.id @{status=2}).total -ne 1 -or (Tasks $x.id @{taskType=2}).total -ne 0 -or (Tasks $x.id @{taskType=1;status=4}).total -ne 1){throw 'Filters wrong.'}
    $p2=Call-Api Post "/shipper/$($x.id)/tasks" @{page=2;take=2} $team
    if(@($p2.list).Count -ne 1 -or $p2.total -ne 3){throw 'Paging wrong.'}
    $yh=Tasks $y.id @{}
    if($yh.total -ne 1 -or [int]$yh.list[0].status -ne 3){throw 'Reassigned shipper history wrong.'}

    Write-Output '3. Scope; history stays after the shipper is deleted'
    Assert-Status 404 Post "/shipper/$($x.id)/tasks" @{page=1;take=5} $merchant
    Assert-Status 403 Post "/shipper/$($x.id)/tasks" @{page=1;take=5} $customer
    if((Call-Api Post "/shipper/$($x.id)/tasks" @{page=1;take=5} $admin).total -ne 3){throw 'Admin view wrong.'}
    Call-Api Delete "/shipper/$($y.id)?reason=Nghi" $null $team | Out-Null
    if((Tasks $y.id @{}).total -ne 1){throw 'Deleted shipper history lost.'}
    $legacy=[int](Sql "SELECT count(*) FROM washgo.delivery_tasks WHERE assigned_at IS NULL AND status=2;")
    Write-Output "   backfilled completed tasks without assignment time: $legacy"

    Write-Output 'PASS: per-shipper task history from recorded milestones (assigned, completed, reassigned, cancelled) with order code, task type, locker/box and order status; no guessed timestamps; status/taskType filters and server paging; reassigned shipper keeps its ended task; Merchant scope (other Merchant 404, Customer 403, Admin all); history kept after deleting the shipper.'
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
    Remove-FixtureMerchant $teamId
}
