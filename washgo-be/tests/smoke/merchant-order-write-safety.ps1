param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: revision preconditions (409) and operationId idempotency on status / actual-weight / assign-shipper.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$http=New-Object System.Net.Http.HttpClient

function New-Request($Method,$Path,$Body,$Token,$Headers){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    if($Headers){ foreach($k in $Headers.Keys){ [void]$req.Headers.TryAddWithoutValidation($k,[string]$Headers[$k]) } }
    if($null -ne $Body){ $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json') }
    $req
}
function Read-Response($Response){
    $text=$Response.Content.ReadAsStringAsync().Result
    @{ Status=[int]$Response.StatusCode; Json=$(if($text){$text|ConvertFrom-Json}else{$null}) }
}
function Send($Method,$Path,$Body,$Token,$Headers){ Read-Response ($http.SendAsync((New-Request $Method $Path $Body $Token $Headers)).Result) }
function Detail(){ (Call-Api Get "/wash-order/$($o.id)" $null $merchant).data }
function Notes($Pattern){ @((Detail).timeline | Where-Object { $_.note -like $Pattern }).Count }

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Write Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null

    Write-Output '1. Revision is bumped by every update, also raw SQL'
    $r0=[long](Detail).revision
    Sql "UPDATE washgo.orders SET shipper_note=NULL WHERE id='$($o.id)';" | Out-Null
    $r1=[long](Detail).revision
    if($r1 -ne $r0+1){throw "Revision not bumped: $r0 -> $r1"}

    Write-Output '2. Assign: stale revision 409, then operationId replay'
    $k1=[Guid]::NewGuid()
    $stale=Send Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId";expectedRevision=$r0} $merchant
    if($stale.Status -ne 409 -or $stale.Json.meta.code -ne 'RevisionMismatch' -or $stale.Json.meta.currentRevision -ne $r1 -or $stale.Json.data.revision -ne $r1 -or $stale.Json.data.shipperId){throw "Stale assign: $($stale.Status) $($stale.Json.meta.code)"}
    $first=Send Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId";expectedRevision=$r1;operationId=$k1} $merchant
    if($first.Status -ne 200 -or $first.Json.data.revision -le $r1){throw "Assign failed: $($first.Status) $($first.Json.message)"}
    $again=Send Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId";expectedRevision=$r1;operationId=$k1} $merchant
    if($again.Status -ne 200 -or !$again.Json.meta.replayed -or $again.Json.meta.resultRevision -ne $first.Json.data.revision){throw "Replay failed: $($again.Status) $($again.Json.meta | ConvertTo-Json -Compress)"}
    if((Notes 'Ph*n c*ng Shipper*') -ne 1){throw 'Replay assigned twice.'}
    $other=Send Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$([Guid]::NewGuid())";operationId=$k1} $merchant
    if($other.Status -ne 409 -or $other.Json.meta.code -ne 'OperationIdReused'){throw "Reused key: $($other.Status)"}
    $op=(Call-Api Get "/wash-order/$($o.id)/operations/$k1" $null $merchant).data
    if($op.operation -ne 'AssignShipper' -or $op.request -ne "shipperId=$shipperId" -or $op.resultRevision -ne $first.Json.data.revision){throw 'Operation lookup wrong.'}
    Assert-Status 404 Get "/wash-order/$($o.id)/operations/$([Guid]::NewGuid())" $null $merchant
    Assert-Status 403 Get "/wash-order/$($o.id)/operations/$k1" $null $customer

    Write-Output '3. Weight with If-Match / expectedStatus / Idempotency-Key headers'
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    $rev=[long](Detail).revision
    $bad=Send Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $merchant @{'If-Match'="`"$($rev-1)`""}
    if($bad.Status -ne 409 -or $bad.Json.meta.code -ne 'RevisionMismatch' -or $bad.Json.data.actualWeightKg){throw "If-Match ignored: $($bad.Status)"}
    $bad=Send Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6;expectedStatus=2} $merchant
    if($bad.Status -ne 409 -or $bad.Json.meta.code -ne 'StatusMismatch' -or $bad.Json.meta.currentStatus -ne 3){throw "expectedStatus ignored: $($bad.Status)"}
    $k2=[Guid]::NewGuid()
    $w=Send Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $merchant @{'If-Match'="`"$rev`"";'Idempotency-Key'="$k2"}
    if($w.Status -ne 200 -or $w.Json.data.actualWeightKg -ne 6){throw "Weight failed: $($w.Status) $($w.Json.message)"}
    $w2=Send Put "/wash-order/$($o.id)/actual-weight" @{weightKg=7} $merchant @{'Idempotency-Key'="$k2"}
    if($w2.Status -ne 409 -or $w2.Json.meta.code -ne 'OperationIdReused' -or (Detail).actualWeightKg -ne 6){throw 'Key reused for another weight.'}

    Write-Output '4. Status: two concurrent sends with the same key run once; replay still answers after the order moved on'
    $k3=[Guid]::NewGuid()
    $body=@{orderId=$o.id;newStatus=4;operationId=$k3}
    $t1=$http.SendAsync((New-Request Put '/wash-order/status' $body $merchant $null))
    $t2=$http.SendAsync((New-Request Put '/wash-order/status' $body $merchant $null))
    [void][System.Threading.Tasks.Task]::WaitAll(@($t1,$t2))
    $a1=Read-Response $t1.Result; $a2=Read-Response $t2.Result
    if($a1.Status -ne 200 -or $a2.Status -ne 200 -or (@($a1.Json.meta.replayed,$a2.Json.meta.replayed) | Where-Object {$_}).Count -ne 1){throw "Concurrent sends: $($a1.Status)/$($a2.Status)"}
    if(@((Detail).timeline | Where-Object { $_.status -eq 4 }).Count -ne 1){throw 'Washing recorded twice.'}
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=7} $merchant | Out-Null
    $late=Send Put '/wash-order/status' $body $merchant
    if($late.Status -ne 200 -or !$late.Json.meta.replayed -or $late.Json.data.status -ne 7){throw "Late replay: $($late.Status) $($late.Json.message)"}
    $empty=Send Put '/wash-order/status' @{orderId=$o.id;newStatus=8;operationId=[Guid]::Empty} $merchant
    if($empty.Status -ne 400){throw 'Empty operationId accepted.'}

    Write-Output 'PASS: revision bumped on every update (incl. raw SQL); stale expectedRevision/If-Match and expectedStatus give 409 with the current order and nothing changed; operationId/Idempotency-Key replays without repeating (assign, weight, status, two concurrent sends), reused key for another request 409, replay still answered after later changes; operation lookup by key (404 unknown, Customer denied); empty key rejected.'
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
    $http.Dispose()
}
