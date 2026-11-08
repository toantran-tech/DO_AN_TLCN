param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: Merchant assignment capacity under concurrency, revision/commission checks, idempotent single and batch
# assignment with lookup, move-to-storage guard and allowedActions metadata.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$NoAutoAssign=$true
$http=New-Object System.Net.Http.HttpClient
function New-Request($Method,$Path,$Body,$Token){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    if($null -ne $Body){ $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json') }
    $req
}
function Read-Response($R){ $t=$R.Content.ReadAsStringAsync().Result; @{ Status=[int]$R.StatusCode; Json=$(if($t){$t|ConvertFrom-Json}else{$null}) } }
function Send($Method,$Path,$Body,$Token){ Read-Response ($http.SendAsync((New-Request $Method $Path $Body $Token)).Result) }
function New-Order(){
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $o
}
function Rev($Id){ [long](Call-Api Get "/wash-order/$Id" $null $admin).data.revision }

try {
    foreach($role in @('Admin','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $teamId="$($sessions.Team.user.id)"
    Sql "UPDATE washgo.merchants SET max_capacity=1, commission_rate=15 WHERE user_id='$teamId';" | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId=$teamId;name="Assign Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=8} $admin).data
    $lockerId=[Guid]$locker.id
    $o=@(); foreach($i in 0..5){ $o+=New-Order }

    Write-Output '1. Last free slot: two concurrent assignments, only one succeeds'
    $t1=$http.SendAsync((New-Request Post "/order-assignment/$($o[0].id)/assign" @{merchantId=$teamId} $admin))
    $t2=$http.SendAsync((New-Request Post "/order-assignment/$($o[1].id)/assign" @{merchantId=$teamId} $admin))
    [void][System.Threading.Tasks.Task]::WaitAll(@($t1,$t2))
    $r1=Read-Response $t1.Result; $r2=Read-Response $t2.Result
    if((@($r1.Status,$r2.Status) | Sort-Object) -join ',' -ne '200,400' -or @($r1,$r2 | Where-Object { $_.Status -eq 400 })[0].Json.meta.code -ne 'CapacityFull'){throw "Capacity race: $($r1.Status)/$($r2.Status)"}
    Sql "UPDATE washgo.merchants SET max_capacity=50 WHERE user_id='$teamId';" | Out-Null
    $free=@($o[0],$o[1] | Where-Object { !(Call-Api Get "/wash-order/$($_.id)" $null $admin).data.merchantId })[0]

    Write-Output '2. Stale revision / changed commission give 409; candidates expose public rating count'
    $c=(Call-Api Get "/order-assignment/$($free.id)/candidates" $null $admin).data.merchants | Where-Object { $_.merchantId -eq $teamId }
    if($null -eq $c.publicRatingCount -or $c.hasPublicRating){throw 'publicRatingCount missing.'}
    $rev=Rev $free.id
    $x=Send Post "/order-assignment/$($free.id)/assign" @{merchantId=$teamId;expectedRevision=$rev-1} $admin
    if($x.Status -ne 409 -or $x.Json.meta.code -ne 'RevisionMismatch'){throw "Stale revision: $($x.Status)"}
    $x=Send Post "/order-assignment/$($free.id)/assign" @{merchantId=$teamId;expectedRevision=$rev;expectedCommissionRate=20} $admin
    if($x.Status -ne 409 -or $x.Json.meta.code -ne 'CommissionChanged'){throw "Commission check: $($x.Status)"}

    Write-Output '3. Idempotent single assignment and lookup'
    $k=[Guid]::NewGuid()
    $a=Send Post "/order-assignment/$($free.id)/assign" @{merchantId=$teamId;expectedRevision=$rev;expectedCommissionRate=15;operationId=$k} $admin
    $b=Send Post "/order-assignment/$($free.id)/assign" @{merchantId=$teamId;expectedRevision=$rev;expectedCommissionRate=15;operationId=$k} $admin
    $other=Send Post "/order-assignment/$($o[2].id)/assign" @{merchantId=$teamId;operationId=$k} $admin
    if($a.Status -ne 200 -or $b.Status -ne 200 -or !$b.Json.meta.replayed -or $b.Json.data.orderId -ne $free.id -or $other.Status -ne 409){throw "Single idempotency: $($a.Status)/$($b.Status)/$($other.Status)"}
    $op=(Call-Api Get "/order-assignment/operations/$k" $null $admin).data
    if(@($op.results).Count -ne 1 -or $op.results[0].merchantId -ne $teamId -or $op.operation -ne 'AssignMerchant'){throw 'Single lookup wrong.'}
    Assert-Status 404 Get "/order-assignment/operations/$([Guid]::NewGuid())" $null $admin

    Write-Output '4. Batch: all-or-nothing with revisions, idempotent replay and lookup'
    $bad=Send Post '/order-assignment/auto-assign' @{assignments=@(@{orderId=$o[2].id;merchantId=$teamId},@{orderId=$o[3].id;merchantId=$teamId;expectedRevision=0})} $admin
    if($bad.Status -ne 409 -or (Call-Api Get "/wash-order/$($o[2].id)" $null $admin).data.merchantId){throw 'Batch not rolled back on a stale revision.'}
    $k2=[Guid]::NewGuid()
    $body=@{operationId=$k2;assignments=@(@{orderId=$o[2].id;merchantId=$teamId;expectedRevision=(Rev $o[2].id)},@{orderId=$o[3].id;merchantId=$teamId})}
    $first=Send Post '/order-assignment/auto-assign' $body $admin
    $again=Send Post '/order-assignment/auto-assign' $body $admin
    if($first.Status -ne 200 -or $again.Status -ne 200 -or !$again.Json.meta.replayed -or @($again.Json.data).Count -ne 2){throw "Batch idempotency: $($first.Status)/$($again.Status)"}
    $changed=Send Post '/order-assignment/auto-assign' @{operationId=$k2;assignments=@(@{orderId=$o[2].id;merchantId=$teamId})} $admin
    if($changed.Status -ne 409){throw 'Batch key reused for a different set.'}
    if(@((Call-Api Get "/order-assignment/operations/$k2" $null $admin).data.results).Count -ne 2){throw 'Batch lookup wrong.'}

    Write-Output '5. Move to storage: allowedActions, guard, replay'
    $s=$o[4]; $box=Sql "SELECT locker_slot_id FROM washgo.orders WHERE id='$($s.id)';"
    Sql "UPDATE washgo.orders SET merchant_id='$teamId', status=10, payment_status=2, return_locker_id='$lockerId', return_box_id='$box', return_box_number=ls.slot_number, pickup_deadline=now() AT TIME ZONE 'utc' - interval '1 hour' FROM washgo.locker_slots ls WHERE washgo.orders.id='$($s.id)' AND ls.id='$box'; UPDATE washgo.locker_slots SET status=2 WHERE id='$box';" | Out-Null
    $d=(Call-Api Get "/wash-order/$($s.id)" $null $admin).data.allowedActions
    if($d.canMoveToStorage -or $d.moveToStorageReasonCode -ne 'NotYetDue' -or !$d.transferToStorageEligibleAt){throw "Not-yet-due metadata wrong: $($d | ConvertTo-Json -Compress)"}
    Sql "UPDATE washgo.orders SET pickup_deadline=now() AT TIME ZONE 'utc' - interval '80 hours' WHERE id='$($s.id)';" | Out-Null
    $d=(Call-Api Get "/wash-order/$($s.id)" $null $admin).data
    if(!$d.allowedActions.canMoveToStorage){throw 'canMoveToStorage false when due.'}
    $mv=@{orderId=$s.id;storageLocation='Kho A';storageReference="REF-$run";expectedRevision=$d.revision-1}
    $stale=Send Post '/wash-order/move-to-storage' $mv $admin
    if($stale.Status -ne 409){throw 'Stale move accepted.'}
    $k3=[Guid]::NewGuid(); $mv.expectedRevision=$d.revision; $mv.operationId=$k3
    $m1=Send Post '/wash-order/move-to-storage' $mv $admin
    $m2=Send Post '/wash-order/move-to-storage' $mv $admin
    if($m1.Status -ne 200 -or $m1.Json.data.status -ne 13 -or $m2.Status -ne 200 -or !$m2.Json.meta.replayed){throw "Move idempotency: $($m1.Status)/$($m2.Status) $($m1.Json.message)"}
    if((Call-Api Get "/wash-order/$($s.id)/operations/$k3" $null $admin).data.operation -ne 'MoveToStorage'){throw 'Move lookup wrong.'}
    $after=(Call-Api Get "/wash-order/$($s.id)" $null $admin).data.allowedActions
    if($after.canMoveToStorage -or $after.moveToStorageReasonCode -ne 'AlreadyMoved'){throw 'Moved order still movable.'}

    Write-Output 'PASS: last capacity slot under two concurrent assignments: one 200, one 400 CapacityFull; stale revision 409 RevisionMismatch, changed commission 409 CommissionChanged; candidates carry publicRatingCount; single and batch assignment replay with the same operationId (no repeat), reused key for other pairs 409, lookup by operationId (404 unknown); batch with a stale item rolls back; move-to-storage exposes canMoveToStorage/reason/eligibleAt, rejects a stale revision, replays with operationId and is found by lookup.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,13,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Remove-FixtureMerchant $teamId
    $http.Dispose()
}
