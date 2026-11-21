param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: stable meta.code for locked/deleted accounts and ended sessions, and the Account-module lookup of
# Shipper accounts created from the account page. Fixture accounts: status-code-<run>-*@example.com (removed in finally).
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$sessions=@{}
$roles=@{Admin='11111111-1111-1111-1111-111111111111';Customer='22222222-2222-2222-2222-222222222222';Merchant='33333333-3333-3333-3333-333333333333';Shipper='44444444-4444-4444-4444-444444444444'}

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{};TimeoutSec=30}
    if($Token){$request.Headers.Authorization="Bearer $Token"}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json);Text=$r.Content} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $text=$_.ErrorDetails.Message
        if(!$text){ try { $text=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { } }
        $json=if($text){ try { $text | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json;Text=$text}
    }
}
function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Expect($Response,$Status,$Code,$What){
    Check ($Response.Status -eq $Status) "$What`: expected HTTP $Status, got $($Response.Status) $($Response.Json.meta.code) $($Response.Json.message)"
    if($Code){ Check ($Response.Json.meta.code -eq $Code) "$What`: expected code $Code, got $($Response.Json.meta.code)" }
    $Response
}
function Op(){ [Guid]::NewGuid().ToString() }
function Email($Tag){ "status-code-$run-$Tag@example.com" }
function Login($Email,$Password='Customer@123'){ Raw Post '/auth/login' @{email=$Email;password=$Password} $null }
function Fixture($Tag,$Role){
    $id=[Guid]::NewGuid()
    Sql "INSERT INTO washgo.users(id,role_id,email,password_hash,auth_provider,full_name,phone,is_active,created_at,updated_at) VALUES ('$id','$($roles[$Role])','$(Email $Tag)','$customerHash',1,'Status Code $Tag','09$(Get-Random -Minimum 10000000 -Maximum 99999999)',true,now(),now());" | Out-Null
    if($Role -eq 'Customer'){ Sql "INSERT INTO washgo.customers(user_id,loyalty_points,total_orders,total_spent,created_at,updated_at) VALUES ('$id',0,0,0,now(),now());" | Out-Null }
    $id
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $customerHash=Sql "SELECT password_hash FROM washgo.users WHERE email='customer@washgo.vn';"
    $adminHash=Sql "SELECT password_hash FROM washgo.users WHERE email='admin@washgo.vn';"

    Write-Output '1. Sign-in: locked only after valid credentials (all four roles), wrong password stays generic, unlock signs in again'
    foreach($role in @('Customer','Merchant','Shipper','Admin')){
        $id=Fixture $role.ToLower() $role
        Sql "UPDATE washgo.users SET is_active=false WHERE id='$id';" | Out-Null
        $locked=Expect (Login (Email $role.ToLower())) 400 'AccountLocked' "$role locked sign-in"
        Check (($role -eq 'Shipper') -eq ($locked.Json.message -match 'Merchant')) "$role locked message points to the wrong contact."
        Check ($locked.Text -notmatch 'reason|statusReason|lockedBy') "$role locked sign-in leaks lock details."
        Expect (Login (Email $role.ToLower()) 'Wrong-Password1') 400 'InvalidCredentials' "$role locked + wrong password" | Out-Null
        Sql "UPDATE washgo.users SET is_active=true WHERE id='$id';" | Out-Null
        Expect (Login (Email $role.ToLower())) 200 $null "$role sign-in after unlock" | Out-Null
    }
    Expect (Login "nobody-$run@example.com") 400 'InvalidCredentials' 'Unknown email' | Out-Null

    Write-Output '2. Session in use: lock -> AccountLocked, revoked refresh stays generic, unlock -> SessionRevoked (no revival)'
    $c=Fixture 'inuse' 'Customer'
    $cs=(Expect (Login (Email 'inuse')) 200 $null 'Sign in C').Json.data
    Expect (Raw Get '/auth/me' $null $cs.accessToken) 200 $null 'C /auth/me' | Out-Null
    $rev=(Call-Api Get "/admin/accounts/$c" $null $admin).data.revision
    Expect (Raw Post "/admin/accounts/$c/status" @{operationId=(Op);isActive=$false;reason='Status code smoke';expectedRevision=$rev;confirm=$true} $admin) 200 $null 'Lock C' | Out-Null
    $me=Expect (Raw Get '/auth/me' $null $cs.accessToken) 401 'AccountLocked' 'Request after lock'
    Check ($me.Json.message -match 'Admin') 'Locked 401 has no readable message pointing to the Admin.'
    Expect (Raw Post '/auth/refresh-token' @{refreshToken=$cs.refreshToken} $null) 400 'InvalidRefreshToken' 'Revoked refresh after lock' | Out-Null
    $rev=(Call-Api Get "/admin/accounts/$c" $null $admin).data.revision
    Expect (Raw Post "/admin/accounts/$c/status" @{operationId=(Op);isActive=$true;expectedRevision=$rev} $admin) 200 $null 'Unlock C' | Out-Null
    Expect (Raw Get '/auth/me' $null $cs.accessToken) 401 'SessionRevoked' 'Old token after unlock' | Out-Null
    $cs=(Expect (Login (Email 'inuse')) 200 $null 'Sign in C again').Json.data

    Write-Output '3. A still-valid refresh token of a locked account reports the state; random tokens do not'
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$c';" | Out-Null
    Expect (Raw Post '/auth/refresh-token' @{refreshToken=$cs.refreshToken} $null) 400 'AccountLocked' 'Valid refresh of locked account' | Out-Null
    Expect (Raw Post '/auth/refresh-token' @{refreshToken='not-a-real-token'} $null) 400 'InvalidRefreshToken' 'Unknown refresh token' | Out-Null
    Sql "UPDATE washgo.users SET is_active=true WHERE id='$c';" | Out-Null

    Write-Output '4. Deleted: session -> AccountDeleted, sign-in with the right password -> AccountDeleted, wrong one stays generic'
    $d=Fixture 'deleted' 'Customer'
    $ds=(Expect (Login (Email 'deleted')) 200 $null 'Sign in D').Json.data
    $rev=(Call-Api Get "/admin/accounts/$d" $null $admin).data.revision
    Expect (Raw Post "/admin/accounts/$d/soft-delete" @{operationId=(Op);reason='Status code smoke';expectedRevision=$rev} $admin) 200 $null 'Delete D' | Out-Null
    Expect (Raw Get '/auth/me' $null $ds.accessToken) 401 'AccountDeleted' 'Request after delete' | Out-Null
    Expect (Login (Email 'deleted')) 400 'AccountDeleted' 'Deleted sign-in' | Out-Null
    Expect (Login (Email 'deleted') 'Wrong-Password1') 400 'InvalidCredentials' 'Deleted + wrong password' | Out-Null

    Write-Output '5. Other 401/403 reasons are not reported as locked'
    Expect (Raw Get '/auth/me' $null $null) 401 'Unauthenticated' 'No token' | Out-Null
    Expect (Raw Get '/auth/me' $null 'abc.def.ghi') 401 'InvalidToken' 'Malformed token' | Out-Null
    $e=Fixture 'session' 'Customer'
    $es=(Expect (Login (Email 'session')) 200 $null 'Sign in E').Json.data
    Expect (Raw Get '/admin/accounts' $null $es.accessToken) 403 'Forbidden' 'Customer on an Admin endpoint' | Out-Null
    Sql "UPDATE washgo.refresh_tokens SET expiry_date=now() - interval '1 minute' WHERE user_id='$e';" | Out-Null
    Expect (Raw Get '/auth/me' $null $es.accessToken) 401 'SessionExpired' 'Expired session' | Out-Null
    $es=(Expect (Login (Email 'session')) 200 $null 'Sign in E again').Json.data
    Call-Api Post '/auth/logout' @{refreshToken=$es.refreshToken} $es.accessToken | Out-Null
    Expect (Raw Get '/auth/me' $null $es.accessToken) 401 'SessionRevoked' 'Signed-out session' | Out-Null

    Write-Output '6. Shipper created from the account page: Account-module lookup, replay, key reuse, lost response, Processing, scope'
    Expect (Raw Post '/auth/reauthenticate' @{password='Admin@123'} $admin) 200 $null 'Reauthenticate' | Out-Null
    function Shipper-Body($Key,$Tag,$Plate='59X1-12345'){
        @{operationId=$Key;role='Shipper';shipper=@{merchantId="$($sessions.Merchant.user.id)";fullName="Status Shipper $Tag";phone="09$(Get-Random -Minimum 10000000 -Maximum 99999999)";email=(Email $Tag);vehicleType=1;vehiclePlate=$Plate}}
    }
    $k1=Op; $body=Shipper-Body $k1 'ship1'
    $s1=(Expect (Raw Post '/admin/accounts' $body $admin) 201 $null 'Create Shipper').Json.data
    $lookup=(Expect (Raw Get "/admin/accounts/operations/$k1" $null $admin) 200 $null 'Lookup created Shipper')
    Check ($lookup.Json.data.state -eq 'Completed' -and $lookup.Json.data.result.data.id -eq $s1.id) 'Lookup did not return the created account.'
    Check ($lookup.Text -notmatch '"passwordHash"|temporaryPassword|"otp|refreshToken|identityNumber|identityFront') 'Lookup leaks secrets or identity data.'
    $again=Raw Post '/admin/accounts' $body $admin
    Check ($again.Status -in 200,201 -and $again.Json.data.id -eq $s1.id) 'Replay of the Shipper create returned another account.'
    Check ((Sql "SELECT count(*) FROM washgo.users WHERE email='$(Email 'ship1')';") -eq '1') 'Replay created a second account.'
    $changed=Shipper-Body $k1 'ship1' '59X1-54321'
    Expect (Raw Post '/admin/accounts' $changed $admin) 409 'OperationIdReused' 'Same key, different payload' | Out-Null
    # Lost response: the Shipper is committed but the Account operation is still Processing.
    $k2=Op
    $s2=(Expect (Raw Post '/admin/accounts' (Shipper-Body $k2 'ship2') $admin) 201 $null 'Create Shipper 2').Json.data
    $processing='{"succeeded":true,"statusCode":202,"message":"x","meta":{"state":"Processing"}}'
    Sql "UPDATE washgo.administrative_operations SET response_json='$processing' WHERE id='$k2';" | Out-Null
    $resolved=(Expect (Raw Get "/admin/accounts/operations/$k2" $null $admin) 200 $null 'Lookup after lost response').Json.data
    Check ($resolved.state -eq 'Completed' -and $resolved.result.data.id -eq $s2.id) 'Lost response was not resolved to the committed account.'
    # Reserved but nothing created yet: Processing, and 404 for unknown keys or another Admin.
    $k3=Op
    Sql "INSERT INTO washgo.administrative_operations(id,actor_id,module,action,resource_id,payload_hash,response_json,completed_at) VALUES ('$k3','$($sessions.Admin.user.id)','Account','Create','new:Shipper','x','$processing',now());" | Out-Null
    $pending=(Expect (Raw Get "/admin/accounts/operations/$k3" $null $admin) 200 $null 'Lookup while Processing').Json.data
    Check ($pending.state -eq 'Processing' -and $null -eq $pending.completedAt) 'Processing operation reported as completed.'
    Expect (Raw Get "/admin/accounts/operations/$(Op)" $null $admin) 404 $null 'Unknown operation' | Out-Null
    $b=(Expect (Raw Post '/admin/accounts' @{operationId=(Op);role='Admin';fullName="Status Admin $run";email=(Email 'admin2');phone='0912345678'} $admin) 201 $null 'Create second Admin').Json.data
    Sql "UPDATE washgo.users SET password_hash='$adminHash' WHERE id='$($b.id)';" | Out-Null
    $sessions.B=(Expect (Login (Email 'admin2') 'Admin@123') 200 $null 'Sign in second Admin').Json.data
    Expect (Raw Get "/admin/accounts/operations/$k1" $null $sessions.B.accessToken) 404 $null 'Lookup by another Admin' | Out-Null

    Write-Output 'PASS: AccountLocked/AccountDeleted only after valid credentials, a valid session or a valid refresh token (four roles, Shipper -> Merchant contact); unknown/wrong credentials and revoked refresh tokens stay generic; Unauthenticated/InvalidToken/SessionExpired/SessionRevoked/Forbidden kept apart; no session revival after unlock; Shipper creation from the account page is looked up in the Account module (completed, replay, key reuse, lost response resolved, Processing, unknown and other-Admin 404).'
}
finally {
    foreach($session in $sessions.Values){ try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null } catch { } }
    $fixtures="SELECT id FROM washgo.users WHERE email LIKE 'status-code-$run-%'"
    try { Sql @"
BEGIN;
DELETE FROM washgo.notification_deliveries WHERE user_id IN ($fixtures);
DELETE FROM washgo.notifications WHERE user_id IN ($fixtures);
DELETE FROM washgo.refresh_tokens WHERE user_id IN ($fixtures);
DELETE FROM washgo.otp_codes WHERE user_id IN ($fixtures);
DELETE FROM washgo.user_permissions WHERE user_id IN ($fixtures);
DELETE FROM washgo.shipper_audits WHERE shipper_id IN (SELECT id FROM washgo.shippers WHERE user_id IN ($fixtures));
DELETE FROM washgo.shippers WHERE user_id IN ($fixtures);
DELETE FROM washgo.customers WHERE user_id IN ($fixtures);
DELETE FROM washgo.users WHERE id IN ($fixtures);
COMMIT;
"@ | Out-Null } catch { Write-Warning "Status code smoke cleanup failed: $($_.Exception.Message)" }
}
