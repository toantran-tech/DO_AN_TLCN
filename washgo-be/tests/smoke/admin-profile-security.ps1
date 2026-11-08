param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Admin profile (whitelisted fields, version), change password, sessions.
# Uses a throw-away Admin cloned from the seed Admin so the shared Admin password never changes.
$sessions=@{}; $adminId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$email="admin-$run@example.com"

function Login($Password){ (Call-Api Post '/auth/login' @{email=$email;password=$Password} $null).data }
function StatusOf($Method,$Path,$Body,$Token){ try{ Call-Api $Method $Path $Body $Token | Out-Null; 200 } catch { [int]$_.Exception.Response.StatusCode } }

try {
    $seedId=Sql "SELECT id FROM washgo.users WHERE email='admin@washgo.vn';"
    $adminId=[Guid]::NewGuid()
    Sql "INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$adminId','email','$email','full_name','Smoke Admin $run','phone',NULL,'auth_provider',1,'provider_id',NULL,'avatar',NULL,'created_at','2026-01-02T03:04:05'))).* FROM washgo.users u WHERE id='$seedId';" | Out-Null
    $a1=Login 'Admin@123'; $a2=Login 'Admin@123'; $a3=Login 'Admin@123'
    $sessions.A1=$a1; $t1=$a1.accessToken

    Write-Output '1. Profile: own data, joinedAt/status/version, whitelist and version check'
    $p=(Call-Api Get '/admin/profile' $null $t1).data
    if($p.id -ne "$adminId" -or $p.accountStatus -ne 'Active' -or ([datetime]$p.joinedAt).ToUniversalTime().Year -ne 2026 -or -not $p.version){throw "Profile wrong: $($p | ConvertTo-Json -Compress)"}
    Assert-Status 400 Put '/admin/profile' @{fullName='Ab Cd'} $t1
    Assert-Status 400 Put '/admin/profile' @{fullName='A';expectedVersion=$p.version} $t1
    Assert-Status 400 Put '/admin/profile' @{phone='12';expectedVersion=$p.version} $t1
    Assert-Status 400 Put '/admin/profile' @{avatar='/uploads/someone-else.jpg';expectedVersion=$p.version} $t1
    $img=Upload-TestImage $t1
    $u=(Call-Api Put '/admin/profile' @{fullName="Smoke Admin Renamed $run";phone='0901234567';avatar=$img;expectedVersion=$p.version;role='Customer';email='hacker@example.com'} $t1).data
    if($u.fullName -ne "Smoke Admin Renamed $run" -or $u.avatar -ne $img -or $u.email -ne $email -or $u.role -ne 'Admin'){throw 'Update wrong.'}
    $again=(Call-Api Get '/admin/profile' $null $t1).data
    if($again.version -ne $u.version){throw "Version not stable: $($again.version) vs $($u.version)"}
    if((StatusOf Put '/admin/profile' @{fullName='Stale write';expectedVersion=$p.version} $t1) -ne 409){throw 'Stale version not rejected.'}
    $same=(Call-Api Put '/admin/profile' @{fullName="Smoke Admin Renamed $run";expectedVersion=$u.version} $t1)
    if($same.data.version -ne $u.version){throw 'No-op save changed the version.'}
    $clr=(Call-Api Put '/admin/profile' @{avatar='';expectedVersion=$u.version} $t1).data
    if($clr.avatar){throw 'Avatar not cleared.'}
    Assert-Status 403 Get '/admin/profile' $null ((Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data.accessToken)

    Write-Output '2. Sessions: list (current first), revoke one, revoke others keeps current'
    $list=(Call-Api Get '/auth/sessions' $null $t1).data
    if(@($list).Count -ne 3 -or -not $list[0].isCurrent -or @($list | Where-Object isCurrent).Count -ne 1){throw "Sessions wrong: $($list | ConvertTo-Json -Compress)"}
    $sec=(Call-Api Get '/auth/security' $null $t1).data
    if(-not $sec.hasPassword -or $sec.twoFactorSupported -or $sec.activeSessions -ne 3){throw 'Security status wrong.'}
    $s2=($list | Where-Object { -not $_.isCurrent })[0].id
    $r=(Call-Api Delete "/auth/sessions/$s2" $null $t1).data
    if($r.revoked -ne 1){throw 'Single revoke failed.'}
    $r=(Call-Api Post '/auth/sessions/revoke-others' $null $t1).data
    if($r.revoked -ne 1){throw "Revoke others count wrong: $($r.revoked)"}
    if((StatusOf Get '/auth/me' $null $a2.accessToken) -ne 401 -or (StatusOf Get '/auth/me' $null $a3.accessToken) -ne 401){throw 'Revoked sessions still work.'}
    if((StatusOf Get '/auth/me' $null $t1) -ne 200){throw 'Current session was revoked.'}
    $cust=(Call-Api Get '/auth/sessions' $null ((Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data.accessToken)).data[0].id
    Assert-Status 404 Delete "/auth/sessions/$cust" $null $t1

    Write-Output '3. Change password: current required, policy, others signed out, wrong attempts limited'
    $a4=Login 'Admin@123'
    Assert-Status 400 Post '/auth/change-password' @{currentPassword='Wrong@123';newPassword='NewPass@123';confirmNewPassword='NewPass@123'} $t1
    Assert-Status 400 Post '/auth/change-password' @{currentPassword='Admin@123';newPassword='short';confirmNewPassword='short'} $t1
    Assert-Status 400 Post '/auth/change-password' @{currentPassword='Admin@123';newPassword='Admin@123';confirmNewPassword='Admin@123'} $t1
    Assert-Status 400 Post '/auth/change-password' @{currentPassword='Admin@123';newPassword='NewPass@123';confirmNewPassword='Other@123'} $t1
    Call-Api Post '/auth/change-password' @{currentPassword='Admin@123';newPassword="NewPass@$run";confirmNewPassword="NewPass@$run"} $t1 | Out-Null
    if((StatusOf Get '/auth/me' $null $a4.accessToken) -ne 401){throw 'Other session survived the password change.'}
    if((StatusOf Get '/auth/me' $null $t1) -ne 200){throw 'Current session lost on password change.'}
    if((StatusOf Post '/auth/login' @{email=$email;password='Admin@123'} $null) -ne 400){throw 'Old password still works.'}
    $sessions.A5=Login "NewPass@$run"
    $sec=(Call-Api Get '/auth/security' $null $t1).data
    if(-not $sec.passwordChangedAt){throw 'passwordChangedAt missing.'}
    Sql "UPDATE washgo.audit_logs SET occurred_at=occurred_at - interval '1 day' WHERE actor_id='$adminId' AND action='ChangePassword';" | Out-Null
    $codes=@(); foreach($i in 1..6){ $codes+=StatusOf Post '/auth/change-password' @{currentPassword="Bad@$i$run";newPassword='Whatever@123';confirmNewPassword='Whatever@123'} $t1 }
    if(($codes[0..4] | Where-Object { $_ -ne 400 }).Count -or $codes[5] -ne 429){throw "Attempt limit wrong: $($codes -join ',')"}

    Write-Output '4. Audit trail: profile/password/session events, no secrets'
    $logs=(Call-Api Post '/admin/audit-logs/filter' @{actorId="$adminId";take=100} $t1).list
    foreach($act in 'Update','RevokeSession','RevokeOtherSessions','ChangePassword'){ if(-not ($logs | Where-Object { $_.action -eq $act -and $_.result -eq 1 })){throw "Missing audit $act"} }
    $raw=Sql "SELECT coalesce(string_agg(coalesce(details::text,''),' '),'') FROM washgo.audit_logs WHERE actor_id='$adminId';"
    if($raw -match "NewPass@$run" -or $raw -match 'Admin@123'){throw 'Password leaked.'}

    Write-Output 'PASS: Admin profile returns own data with joinedAt/accountStatus/version; only fullName/phone/avatar editable (own upload, "" clears, role/email ignored), expectedVersion required, stale version 409, no-op save keeps the version; sessions list current first, revoke one / revoke others keeps the current session and kills other access tokens, other users'' sessions 404; change-password needs the current password, enforces policy, differs from old, signs out other sessions, sets passwordChangedAt, 5 wrong attempts in 15 minutes then 429; all audited without secrets.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}
    if($adminId){ Sql "UPDATE washgo.refresh_tokens SET is_revoked=true WHERE user_id='$adminId'; UPDATE washgo.users SET is_active=false, email='deleted-$run@example.com' WHERE id='$adminId';" | Out-Null }
}
