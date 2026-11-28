param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$fixture=[Guid]::NewGuid()
$sessions=@{}
function Check($Condition,$Message) { if(!$Condition){throw $Message} }
function Profile { (Call-Api Get '/merchant-profile/' $null $merchant).data }
function Submit($Schedule) {
    $p=Profile
    (Call-Api Post '/merchant-profile/update-requests' @{
        businessName=$p.businessName;address=$p.address;city=$p.city;phone='0901234567'
        openingHours=$p.openingHours;openingSchedule=$Schedule;expectedMerchantVersion=$p.version
    } $merchant).data
}
function Approve($Request) {
    (Call-Api Post "/merchant-profile-request/$($Request.id)/approve?expectedVersion=$($Request.version)" $null $admin).data
}
function Reject($Request) {
    Call-Api Post "/merchant-profile-request/$($Request.id)/reject" @{
        expectedVersion=$Request.version;reason='Please submit against the current profile'
    } $admin | Out-Null
}
function Expect-Conflict($Request) {
    try {
        Call-Api Post "/merchant-profile-request/$($Request.id)/approve?expectedVersion=$($Request.version)" $null $admin | Out-Null
        throw 'Approval unexpectedly succeeded'
    } catch {
        if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 409){throw}
        $body=$_.ErrorDetails.Message
        if(!$body){$body=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd()}
        Check (($body|ConvertFrom-Json).meta.code -eq 'MerchantProfileChanged') 'Wrong conflict code'
    }
    Check ((Call-Api Get "/merchant-profile-request/$($Request.id)" $null $merchant).data.status -eq 1) 'Failed approval changed request'
}
$always=@{timeZone='Asia/Ho_Chi_Minh';alwaysOpen=$true;weekly=@();closedDates=@()}
$holiday=@{timeZone='Asia/Ho_Chi_Minh';alwaysOpen=$true;weekly=@();closedDates=@('2026-12-15')}
$shifts=@{timeZone='Asia/Ho_Chi_Minh';alwaysOpen=$false;weekly=@(
    @{dayOfWeek=1;shifts=@(@{open='08:00';close='12:00'},@{open='13:00';close='18:00'})}
);closedDates=@('2026-12-15')}
try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password=($role+'@123')} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $source=$sessions.Merchant.user.id
    # Clone only this run's Merchant, with no locker/order references.
    Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixture','email','snapshot-$fixture@example.test','provider_id',NULL,'is_active',true))).*
FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants
SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$fixture','business_name','Schedule Snapshot Fixture','tax_code',NULL,'status',1,'phone','0901234567',
'opening_hours','Mở cửa 24/7','opening_schedule',jsonb_build_object(
'timeZone','Asia/Ho_Chi_Minh','alwaysOpen',true,'weekly','[]'::jsonb,'closedDates','[]'::jsonb)))).*
FROM washgo.merchants m WHERE user_id='$source';
"@ | Out-Null
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email="snapshot-$fixture@example.test";password='Merchant@123'} $null).data
    $merchant=$sessions.Fixture.accessToken
    $request=Submit $holiday
    Check ($request.current.openingScheduleSnapshotIncluded -and $request.current.openingSchedule.alwaysOpen) 'Current snapshot missing'
    Check ($request.current.openingSchedule.closedDates.Count -eq 0) 'Original closures lost'
    Check ('openingSchedule' -in $request.changedFields) 'Closure-only change missing'
    Check ((Profile).pendingRequest.id -eq $request.id) 'Pending request missing'
    Assert-Status 404 Get "/merchant-profile-request/$($request.id)" $null $sessions.Merchant.accessToken
    foreach($role in @('Customer','Shipper')){
        Assert-Status 403 Post "/merchant-profile-request/$($request.id)/approve?expectedVersion=$($request.version)" $null $sessions[$role].accessToken
    }
    $operation=[Guid]::NewGuid()
    $path="/merchant-profile-request/$($request.id)/approve?expectedVersion=$($request.version)&operationId=$operation"
    $approved=(Call-Api Post $path $null $admin).data
    $replay=(Call-Api Post $path $null $admin).data
    Check ($approved.status -eq 2 -and $approved.version -eq $replay.version) 'Approval/replay failed'
    Check ((Profile).openingSchedule.closedDates[0] -eq '2026-12-15') 'Approved closure lost'

    # Keep display text, change structured data only.
    Sql "UPDATE washgo.merchants SET opening_schedule=opening_schedule || '{""closedDates"":[]}'::jsonb WHERE user_id='$fixture';" | Out-Null
    $request=Submit $holiday
    Check ('openingSchedule' -in $request.changedFields -and 'openingHours' -notin $request.changedFields) 'Same-text schedule change missed'
    Approve $request | Out-Null

    $request=Submit $shifts
    Check ('openingSchedule' -in $request.changedFields) 'Shift-only change missed'
    Sql "UPDATE washgo.merchants SET opening_schedule=opening_schedule || '{""closedDates"":[]}'::jsonb WHERE user_id='$fixture';" | Out-Null
    Expect-Conflict $request
    Check ((Profile).openingSchedule.closedDates.Count -eq 0) 'Conflict overwrote schedule'
    Reject $request

    $request=Submit $shifts
    Sql "UPDATE washgo.merchant_profile_requests SET current_data=current_data - 'openingSchedule' - 'openingScheduleSnapshotIncluded' WHERE id='$($request.id)';" | Out-Null
    $legacy=(Call-Api Get "/merchant-profile-request/$($request.id)" $null $merchant).data
    Check ($null -eq $legacy.current.openingSchedule) 'Legacy schedule fabricated'
    Approve $request | Out-Null

    $request=Submit $always
    Sql "UPDATE washgo.merchant_profile_requests SET current_data=current_data - 'openingSchedule' - 'openingScheduleSnapshotIncluded' WHERE id='$($request.id)'; UPDATE washgo.merchants SET updated_at=updated_at + interval '1 second' WHERE user_id='$fixture';" | Out-Null
    Expect-Conflict $request
    Reject $request

    $request=Submit $always
    Sql "UPDATE washgo.merchant_profile_requests SET current_data=current_data - 'openingSchedule' - 'openingScheduleSnapshotIncluded' - 'baseMerchantVersion' WHERE id='$($request.id)';" | Out-Null
    Expect-Conflict $request
    Reject $request
    $p=Profile
    Assert-Status 400 Post '/merchant-profile/update-requests' @{
        businessName=$p.businessName;address=$p.address;city=$p.city;phone=$p.phone
        openingHours=$p.openingHours;openingSchedule=$p.openingSchedule;expectedMerchantVersion=$p.version
    } $merchant
    Write-Output 'PASS: schedule snapshots, closure/shift and same-text changes, ownership, replay, profile conflict, legacy version checks and rejection recovery.'
}
finally {
    foreach($session in $sessions.Values){
        try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}
        catch{Write-Warning 'Fixture session logout failed'}
    }
    Sql "UPDATE washgo.merchant_profile_requests SET status=3 WHERE merchant_id='$fixture' AND status=1; UPDATE washgo.merchants SET status=3 WHERE user_id='$fixture'; UPDATE washgo.users SET is_active=false WHERE id='$fixture';" | Out-Null
}
