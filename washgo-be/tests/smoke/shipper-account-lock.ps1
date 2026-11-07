param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Merchant locks/unlocks a shipper account (sessions revoked, active-task guard, audit).
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function Status-Of($Method,$Path,$Body,$Token){ try { Call-Api $Method $Path $Body $Token | Out-Null; 200 } catch { [int]$_.Exception.Response.StatusCode } }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $x=New-FixtureShipper $team "Lock $run" $run
    $password=Read-TempPassword $x.email
    $sessions.X=(Call-Api Post '/auth/login' @{email=$x.email;password=$password} $null).data; $xToken=$sessions.X.accessToken
    if(!$x.accountActive -or $x.isLocked){throw 'New shipper not active.'}

    Write-Output '1. Validation and scope'
    Assert-Status 400 Post "/shipper/$($x.id)/lock" @{reason=' '} $team
    Assert-Status 400 Post "/shipper/$($x.id)/lock" @{reason=('x'*501)} $team
    Assert-Status 404 Post "/shipper/$($x.id)/lock" @{reason='Other shop'} $merchant
    Assert-Status 403 Post "/shipper/$($x.id)/lock" @{reason='No'} $customer

    Write-Output '2. Active task blocks the lock'
    $locker=(Call-Api Post '/locker/' @{merchantId="$teamId";name="Lock Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$($x.id)"} $team | Out-Null
    Add-Type -AssemblyName System.Net.Http
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$team)
        $content=New-Object System.Net.Http.StringContent ('{"reason":"Busy"}',[Text.Encoding]::UTF8,'application/json')
        $resp=$client.PostAsync("$BaseUrl/shipper/$($x.id)/lock",$content).Result
        $body=$resp.Content.ReadAsStringAsync().Result | ConvertFrom-Json
        if([int]$resp.StatusCode -ne 409 -or $body.meta.code -ne 'HasActiveTasks'){throw "Active task not blocking: $([int]$resp.StatusCode) $($body.meta.code)"}
    } finally { $client.Dispose() }
    if((Status-Of Get '/shipper-profile/' $null $xToken) -ne 200){throw 'Blocked lock changed the session.'}
    Invoke-TestDelivery $o.id collect $xToken | Out-Null

    Write-Output '3. Lock: sessions revoked, login blocked, not assignable, counted'
    $r=Call-Api Post "/shipper/$($x.id)/lock" @{reason="  Vi pham quy dinh $run  "} $team
    if($r.meta.code -ne 'Locked' -or !$r.data.isLocked -or $r.data.lockReason -ne "Vi pham quy dinh $run" -or !$r.data.lockedAt -or !$r.data.isActive){throw "Lock result wrong: $($r | ConvertTo-Json -Depth 3 -Compress)"}
    if((Status-Of Get '/shipper-profile/' $null $xToken) -ne 401){throw 'Old access token still works.'}
    if((Status-Of Post '/auth/refresh-token' @{refreshToken=$sessions.X.refreshToken} $null) -ne 400){throw 'Refresh token still works.'}
    if((Status-Of Post '/auth/login' @{email=$x.email;password=$password} $null) -ne 400){throw 'Locked shipper can log in.'}
    $sessions.Remove('X')
    if((Call-Api Get '/shipper/summary' $null $team).data.locked -ne 1){throw 'Summary locked count wrong.'}
    $again=Call-Api Post "/shipper/$($x.id)/lock" @{reason='Again'} $team
    if($again.meta.code -ne 'AlreadyLocked'){throw 'Repeated lock not idempotent.'}
    $l2=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o2=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l2.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o2.id deposit $customer | Out-Null
    if(@((Call-Api Get "/wash-order/$($o2.id)/available-shippers" $null $team).data | Where-Object { $_.id -eq $x.id }).Count){throw 'Locked shipper offered for assignment.'}
    Assert-Status 400 Put '/wash-order/assign-shipper' @{orderId=$o2.id;shipperId="$($x.id)"} $team

    Write-Output '4. Unlock keeps working/operational status; audit trail'
    $before=(Call-Api Get "/shipper/$($x.id)" $null $team).data
    $u=(Call-Api Post "/shipper/$($x.id)/unlock" @{reason='Da xu ly'} $team).data
    if($u.isLocked -or $u.lockReason -or $u.isActive -ne $before.isActive -or $u.status -ne $before.status){throw 'Unlock changed other state.'}
    $sessions.X=(Call-Api Post '/auth/login' @{email=$x.email;password=$password} $null).data
    if((Status-Of Get '/shipper-profile/' $null $sessions.X.accessToken) -ne 200){throw 'Unlocked shipper cannot work.'}
    Call-Api Post "/shipper/$($x.id)/lock" @{reason='Admin check'} $admin | Out-Null
    Call-Api Post "/shipper/$($x.id)/unlock" @{} $admin | Out-Null
    $audits=(Call-Api Get "/shipper/$($x.id)/audits" $null $team).data
    $actions=(@($audits) | ForEach-Object { [int]$_.action }) -join ','
    if($actions -ne '2,1,2,1' -or $audits[3].reason -ne "Vi pham quy dinh $run" -or $audits[3].actorRole -ne 'Merchant' -or $audits[1].actorRole -ne 'Admin' -or $audits[2].reason -ne 'Da xu ly'){throw "Audit wrong: $actions"}
    Assert-Status 404 Get "/shipper/$($x.id)/audits" $null $merchant

    Write-Output 'PASS: lock needs a reason (<=500) and the owning Merchant/Admin; active collect/return task gives 409 HasActiveTasks; lock revokes refresh tokens, old access token 401, login blocked, removed from available shippers and assignment, counted as locked; repeated lock idempotent; unlock restores login without changing working/operational status; audit trail with actor, role, time and reason.'
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
