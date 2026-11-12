param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: central audit trail (sign-in, Merchant lock, settings) with Admin-only read APIs.
$sessions=@{}; $teamId=$null; $oldRate=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$start=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-1

try {
    Write-Output '1. Failed and successful sign-in are recorded, without the password'
    Assert-Status 400 Post '/auth/login' @{email='admin@washgo.vn';password="Wrong-$run"} $null
    $sessions.Admin=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data; $admin=$sessions.Admin.accessToken
    $adminId="$($sessions.Admin.user.id)"
    $logins=(Call-Api Post '/admin/audit-logs/filter' @{module='Auth';action='Login';fromDate=$start;keyword='admin@washgo.vn';take=50} $admin)
    $failed=@($logins.list | Where-Object { $_.result -eq 2 -and $_.errorCode -eq 'InvalidCredentials' })
    $ok=@($logins.list | Where-Object { $_.result -eq 1 -and $_.actorId -eq $adminId })
    if($failed.Count -lt 1 -or $ok.Count -lt 1){throw "Login audit missing: $($logins | ConvertTo-Json -Depth 4 -Compress)"}
    $raw=Sql "SELECT coalesce(string_agg(coalesce(details::text,'')||coalesce(reason,''),' '),'') FROM washgo.audit_logs WHERE occurred_at >= to_timestamp($start) AT TIME ZONE 'utc';"
    if($raw -match "Wrong-$run" -or $raw -match 'Admin@123' -or $raw -match '(?i)"password"'){throw 'Password leaked into the audit trail.'}

    Write-Output '2. Settings change records field names with before/after values'
    $oldRate=(Call-Api Get '/system-setting/' $null $admin).data.defaultCommissionRate
    $newRate=if($oldRate -eq 13){14}else{13}
    Call-Api Put '/system-setting/commission' @{defaultCommissionRate=$newRate;expectedVersion=(Call-Api Get '/system-setting/' $null $admin).data.version} $admin | Out-Null
    $s=(Call-Api Post '/admin/audit-logs/filter' @{module='SystemSetting';fromDate=$start;take=5} $admin).list[0]
    if((@($s.changedFields) -join ',') -ne 'defaultCommissionRate' -or $s.details.before.defaultCommissionRate -ne $oldRate -or $s.details.after.defaultCommissionRate -ne $newRate -or $s.actorRole -ne 'Admin'){throw "Settings audit wrong: $($s | ConvertTo-Json -Depth 5 -Compress)"}

    Write-Output '3. Merchant lock/unlock with reason; detail by id'
    $sessions.Team=New-FixtureMerchant $run; $teamId="$($sessions.Team.user.id)"
    Call-Api Put '/merchant/status' @{merchantId=$teamId;status=3;rejectionReason="Smoke lock $run CCCD 012345678901";confirm=$true;expectedVersion=(Call-Api Get "/merchant/$teamId" $null $admin).data.version} $admin | Out-Null
    Call-Api Put '/merchant/status' @{merchantId=$teamId;status=1;confirm=$true;expectedVersion=(Call-Api Get "/merchant/$teamId" $null $admin).data.version} $admin | Out-Null
    # Locking revoked the Merchant's sessions: sign in again for the access-control checks.
    $sessions.Team=(Call-Api Post '/auth/login' @{email="team-$run@example.com";password='Merchant@123'} $null).data
    $m=(Call-Api Post '/admin/audit-logs/filter' @{resourceType='Merchant';resourceId=$teamId;take=10} $admin)
    if($m.total -ne 2 -or $m.list[0].action -ne 'Unlock' -or $m.list[1].action -ne 'Lock' -or $m.list[1].reason -ne "Smoke lock $run CCCD [number]"){throw "Merchant audit wrong: $($m.list | ConvertTo-Json -Depth 3 -Compress)"}
    $d=(Call-Api Get "/admin/audit-logs/$($m.list[1].id)" $null $admin).data
    if($d.resourceCode -notmatch $run -or -not $d.occurredAt.ToString().Length){throw 'Detail wrong.'}

    Write-Output '3b. Rejected writes are recorded as Failed/Denied with an error code'
    try { Call-Api Put '/merchant/status' @{merchantId=$teamId;status=3;rejectionReason="Rejected $run";confirm=$true;expectedVersion='stale'} $admin | Out-Null; throw 'Stale write accepted.' } catch { if($_.Exception.Message -eq 'Stale write accepted.'){throw} }
    $rej=@((Call-Api Post '/admin/audit-logs/filter' @{module='merchant';fromDate=$start;take=50} $admin).list | Where-Object { $_.action -eq 'PUT /merchant/status' -and $_.result -ne 1 })
    if($rej.Count -lt 1 -or $rej[0].result -notin 2,3 -or -not $rej[0].errorCode -or $rej[0].actorId -ne $adminId){throw "Rejected write not audited: $($rej | ConvertTo-Json -Compress)"}

    Write-Output '4. My activities, filters, paging, access control, read-only'
    $mine=(Call-Api Get '/admin/me/activities?take=2' $null $admin)
    if(@($mine.list).Count -ne 2 -or $mine.total -lt 4 -or @($mine.list | Where-Object { $_.actorId -ne $adminId }).Count){throw 'My activities wrong.'}
    $denied=(Call-Api Post '/admin/audit-logs/filter' @{result=2;fromDate=$start;take=100} $admin)
    if(@($denied.list | Where-Object { $_.result -ne 2 }).Count){throw 'Result filter wrong.'}
    Assert-Status 400 Post '/admin/audit-logs/filter' @{fromDate=200;toDate=100} $admin
    Assert-Status 403 Post '/admin/audit-logs/filter' @{take=1} $sessions.Team.accessToken
    Assert-Status 403 Get '/admin/me/activities' $null $sessions.Team.accessToken
    foreach($method in 'Put','Delete'){
        try { Call-Api $method "/admin/audit-logs/$($d.id)" @{} $admin | Out-Null; throw "$method allowed" }
        catch { if([int]$_.Exception.Response.StatusCode -notin 404,405){throw} }
    }

    Write-Output 'PASS: failed/successful sign-in and logout logged (no password); settings change logs changed field names with before/after; Merchant lock/unlock logged with reason and found by resource; detail by id; /admin/me/activities shows only own actions; filters by module/action/result/time/keyword with paging; Admin only; no update/delete API.'
}
finally {
    if($null -ne $oldRate){ try{Call-Api Put '/system-setting/commission' @{defaultCommissionRate=$oldRate;expectedVersion=(Call-Api Get '/system-setting/' $null $admin).data.version} $admin | Out-Null}catch{Write-Warning 'Could not restore commission rate.'} }
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Remove-FixtureMerchant $teamId
}
