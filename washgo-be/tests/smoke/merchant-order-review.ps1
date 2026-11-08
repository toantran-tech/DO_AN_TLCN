param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: summary skips soft-deleted orders, operationId fingerprint includes the note, stable paging on ties.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$http=New-Object System.Net.Http.HttpClient
function Send($Method,$Path,$Body,$Token){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json')
    $r=$http.SendAsync($req).Result; $t=$r.Content.ReadAsStringAsync().Result
    @{ Status=[int]$r.StatusCode; Json=$(if($t){$t|ConvertFrom-Json}else{$null}) }
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Review Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerId=[Guid]$locker.id
    $ids=@()
    foreach($i in 0..2){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $o.id deposit $customer | Out-Null
        $ids+=$o.id
    }
    Sql "UPDATE washgo.orders SET merchant_order_code='REV$run'||RIGHT(order_code,8) WHERE locker_id='$lockerId';" | Out-Null
    $kw="rev$run"

    Write-Output '1. Summary and list skip soft-deleted orders'
    Sql "UPDATE washgo.orders SET is_deleted=true WHERE id='$($ids[2])';" | Out-Null
    $s=(Call-Api Post '/wash-order/summary' @{keyword=$kw} $merchant).data
    $l=Call-Api Post '/wash-order/filter' @{keyword=$kw;page=1;take=10} $merchant
    if($s.total -ne 2 -or $l.total -ne 2 -or $s.statusCounts.deposited -ne 2){throw "Deleted order counted: summary=$($s.total) list=$($l.total)"}

    Write-Output '2. Stable paging when the sort value ties'
    Sql "UPDATE washgo.orders SET created_on=1794000000 WHERE locker_id='$lockerId';" | Out-Null
    $p=@()
    foreach($page in 1..2){ $p+=(Call-Api Post '/wash-order/filter' @{keyword=$kw;page=$page;take=1} $merchant).list[0].id }
    if($p[0] -eq $p[1]){throw 'Same order on two pages.'}

    Write-Output '3. operationId fingerprint includes the note'
    $k=[Guid]::NewGuid()
    $a=Send Put '/wash-order/assign-shipper' @{orderId=$ids[0];shipperId="$shipperId";note='Goi truoc khi den';operationId=$k} $merchant
    $b=Send Put '/wash-order/assign-shipper' @{orderId=$ids[0];shipperId="$shipperId";note='Goi truoc khi den';operationId=$k} $merchant
    $c=Send Put '/wash-order/assign-shipper' @{orderId=$ids[0];shipperId="$shipperId";note='Ghi chu khac';operationId=$k} $merchant
    if($a.Status -ne 200 -or $b.Status -ne 200 -or !$b.Json.meta.replayed -or $c.Status -ne 409 -or $c.Json.meta.code -ne 'OperationIdReused'){throw "Assign fingerprint wrong: $($a.Status)/$($b.Status)/$($c.Status)"}
    if((Call-Api Get "/wash-order/$($ids[0])" $null $merchant).data.shipperNote -ne 'Goi truoc khi den'){throw 'Note changed by a reused key.'}
    $op=(Call-Api Get "/wash-order/$($ids[0])/operations/$k" $null $merchant).data
    if($op.request -notmatch "^shipperId=$shipperId;text=[0-9a-f]{16}$"){throw "Fingerprint format wrong: $($op.request)"}
    Invoke-TestDelivery $ids[0] collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($ids[0])/actual-weight" @{weightKg=6} $merchant | Out-Null
    $k2=[Guid]::NewGuid()
    $d=Send Put '/wash-order/status' @{orderId=$ids[0];newStatus=4;note='Bat dau giat';operationId=$k2} $merchant
    $e=Send Put '/wash-order/status' @{orderId=$ids[0];newStatus=4;note='Khac';operationId=$k2} $merchant
    if($d.Status -ne 200 -or $e.Status -ne 409 -or $e.Json.meta.code -ne 'OperationIdReused'){throw "Status fingerprint wrong: $($d.Status)/$($e.Status)"}

    Write-Output 'PASS: summary and list exclude soft-deleted orders; tied sort values page without repeats (Id tie-breaker); operationId replay with the same note is replayed, a different note with the same key gives 409 OperationIdReused (assign-shipper and status) and the stored note is unchanged; fingerprint keeps key fields plus a note hash.'
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
