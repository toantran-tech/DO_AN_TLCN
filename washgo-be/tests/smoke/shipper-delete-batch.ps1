param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: soft delete of a shipper and batch lock/delete with per-ID results.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function Status-Of($Method,$Path,$Body,$Token){ try { Call-Api $Method $Path $Body $Token | Out-Null; 200 } catch { [int]$_.Exception.Response.StatusCode } }
function Item($Batch,$Id){ @($Batch.results | Where-Object { $_.shipperId -eq $Id })[0] }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $s=@{}
    foreach($n in 'A','B','C','D','E'){ $s[$n]=New-FixtureShipper $team "Del $run $n" $run }
    $passA=Read-TempPassword $s.A.email
    $sessions.A=(Call-Api Post '/auth/login' @{email=$s.A.email;password=$passA} $null).data

    # B gets an active collect task; A completes one so the delivery history keeps its name.
    $locker=(Call-Api Post '/locker/' @{merchantId="$teamId";name="Del Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $orders=@()
    foreach($i in 0..1){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $orders+=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $orders[$i].id deposit $customer | Out-Null
    }
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$orders[0].id;shipperId="$($s.A.id)"} $team | Out-Null
    Invoke-TestDelivery $orders[0].id collect $sessions.A.accessToken | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$orders[1].id;shipperId="$($s.B.id)"} $team | Out-Null

    Write-Output '1. Single delete: reason, scope, active-task guard'
    Assert-Status 400 Delete "/shipper/$($s.A.id)" $null $team
    Assert-Status 404 Delete "/shipper/$($s.A.id)?reason=x" $null $merchant
    Assert-Status 409 Delete "/shipper/$($s.B.id)?reason=Nghi+viec" $null $team
    $d=Call-Api Delete "/shipper/$($s.A.id)?reason=Nghi+viec+$run" $null $team
    if($d.meta.code -ne 'Deleted'){throw "Delete result wrong: $($d.meta.code)"}

    Write-Output '2. Deleted shipper is hidden, cannot sign in, history kept'
    Assert-Status 404 Get "/shipper/$($s.A.id)" $null $team
    if(@((Call-Api Post '/shipper/filter' @{page=1;take=50} $team).list | Where-Object { $_.id -eq $s.A.id }).Count){throw 'Deleted shipper listed.'}
    if((Call-Api Get '/shipper/summary' $null $team).data.total -ne 4){throw 'Deleted shipper counted.'}
    if((Status-Of Get '/shipper-profile/' $null $sessions.A.accessToken) -ne 401){throw 'Deleted shipper session still valid.'}
    if((Status-Of Post '/auth/login' @{email=$s.A.email;password=$passA} $null) -ne 400){throw 'Deleted shipper can log in.'}
    $sessions.Remove('A')
    $kept=(Call-Api Get "/wash-order/$($orders[0].id)" $null $team).data
    if($kept.shipperId -ne $s.A.id -or $kept.shipperName -ne "Del $run A"){throw 'Order lost the deleted shipper.'}
    $again=Call-Api Delete "/shipper/$($s.A.id)?reason=again" $null $team
    if($again.meta.code -ne 'AlreadyDeleted'){throw 'Repeated delete not idempotent.'}
    $audit=(Call-Api Get "/shipper/$($s.A.id)/audits" $null $team).data
    if([int]$audit[0].action -ne 3 -or $audit[0].reason -ne "Nghi viec $run"){throw 'Delete audit missing.'}

    Write-Output '3. Batch lock: per-ID results, duplicates once, partial success, safe to resend'
    $foreign=(Call-Api Post '/shipper/filter' @{page=1;take=1} $merchant).list[0].id
    $body=@{shipperIds=@($s.C.id,$s.C.id,$s.B.id,$foreign,$s.D.id);reason="Kiem tra $run"}
    $b=(Call-Api Post '/shipper/batch/lock' $body $team).data
    if($b.requested -ne 5 -or $b.duplicatesIgnored -ne 1 -or $b.succeeded -ne 2 -or $b.failed -ne 2 -or $b.results.Count -ne 4){throw "Batch counts wrong: $($b | ConvertTo-Json -Depth 3 -Compress)"}
    if((Item $b $s.C.id).code -ne 'Locked' -or (Item $b $s.B.id).code -ne 'HasActiveTasks' -or (Item $b $foreign).code -ne 'NotFound' -or (Item $b $s.D.id).code -ne 'Locked'){throw 'Batch item codes wrong.'}
    $b2=(Call-Api Post '/shipper/batch/lock' $body $team).data
    if((Item $b2 $s.C.id).code -ne 'AlreadyLocked' -or $b2.succeeded -ne 2){throw 'Batch resend not safe.'}
    if(!(Call-Api Get "/shipper/$($s.D.id)" $null $team).data.isLocked){throw 'Batch lock not applied.'}
    Assert-Status 400 Post '/shipper/batch/lock' @{shipperIds=@();reason='x'} $team
    Assert-Status 400 Post '/shipper/batch/lock' @{shipperIds=@($s.C.id);reason=''} $team
    Assert-Status 400 Post '/shipper/batch/lock' @{shipperIds=@(1..51 | ForEach-Object { [Guid]::NewGuid() });reason='x'} $team

    Write-Output '4. Batch delete'
    $bd=(Call-Api Post '/shipper/batch/delete' @{shipperIds=@($s.D.id,$s.E.id,$s.B.id,$s.A.id);reason="Giai the $run"} $team).data
    if((Item $bd $s.D.id).code -ne 'Deleted' -or (Item $bd $s.E.id).code -ne 'Deleted' -or (Item $bd $s.B.id).code -ne 'HasActiveTasks' -or (Item $bd $s.A.id).code -ne 'AlreadyDeleted' -or $bd.succeeded -ne 3){throw "Batch delete wrong: $($bd | ConvertTo-Json -Depth 3 -Compress)"}
    $sum=(Call-Api Get '/shipper/summary' $null $team).data
    if($sum.total -ne 2 -or $sum.locked -ne 1){throw "Summary after batch wrong: $($sum | ConvertTo-Json -Compress)"}

    Write-Output 'PASS: delete needs a reason and the owning Merchant, active task gives 409; deleted shipper hidden from detail/list/summary, sessions revoked, login blocked, orders keep shipper id/name, repeated delete idempotent, audit kept; batch lock/delete process each distinct ID in its own transaction with per-ID codes (Locked/AlreadyLocked/Deleted/AlreadyDeleted/HasActiveTasks/NotFound), partial success, duplicates ignored, safe resend, size 1-50 and reason required.'
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
