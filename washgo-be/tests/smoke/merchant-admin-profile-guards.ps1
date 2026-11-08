param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Dedicated DB fixtures avoid the account-creation endpoint and never send credential emails.
$fixture = [Guid]::NewGuid(); $area = [Guid]::NewGuid(); $link = [Guid]::NewGuid()
$session = $null; $staff = $null
try {
    $session = (Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data
    $admin = $session.accessToken
    $source = Sql "SELECT id FROM washgo.users WHERE email='merchant@washgo.vn';"
    if (!$source) { throw 'A development Merchant seed is required.' }
    Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users, to_jsonb(u) || jsonb_build_object(
 'id','$fixture','email','merchant-guard-$fixture@example.test','provider_id',NULL))).*
FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants
SELECT (jsonb_populate_record(NULL::washgo.merchants, to_jsonb(m) || jsonb_build_object(
 'user_id','$fixture','business_name','Merchant Guard Fixture','tax_code',NULL,'status',1))).*
FROM washgo.merchants m WHERE user_id='$source';
INSERT INTO washgo.service_areas(id,name,code,district,city,radius_km,is_active,created_at,updated_at)
VALUES('$area','Guard Area','GUARD_$area','Test','Test',2,true,now(),now());
INSERT INTO washgo.merchant_service_areas(id,merchant_id,service_area_id,priority,is_active,assigned_by,assigned_at,created_at,updated_at)
VALUES('$link','$fixture','$area',7,true,NULL,'2026-01-01T00:00:00Z','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
"@ | Out-Null
    $view = (Call-Api Get "/merchant/$fixture" $null $admin).data
    $body = @{}
    foreach($key in @('email','businessName','taxCode','businessLicense','address','district','city','latitude','longitude',
        'phone','openingHours','depositAmount','commissionRate','serviceRadiusKm','maxCapacity','contractStartDate','contractEndDate','slaDocUrl')) {
        $body[$key] = $view.$key
    }
    $body.id = "$fixture"
    $body.expectedVersion = $view.version
    $body.serviceAreaIds = @("$area")
    $before = Sql "SELECT row_to_json(x) FROM washgo.merchant_service_areas x WHERE id='$link';"
    Assert-Status 401 Put '/merchant/' $body $null
    $staff = (Call-Api Post '/auth/login' @{email='merchant@washgo.vn';password='Merchant@123'} $null).data
    Assert-Status 403 Put '/merchant/' $body $staff.accessToken
    Assert-Status 401 Get '/merchant/summary' $null $null
    Assert-Status 403 Get '/merchant/summary' $null $staff.accessToken
    $summary=(Call-Api Get '/merchant/summary' $null $admin).data
    if($summary.total -ne ($summary.active+$summary.inactive+$summary.suspended) -or !$summary.generatedAt) {
        throw 'Merchant summary counts or generatedAt invalid.'
    }
    $activeBefore=$summary.active; $suspendedBefore=$summary.suspended

    # All legal status changes must use the guarded status endpoint, including unlocks.
    $bad = $body.Clone(); $bad.status = 3
    Assert-Status 400 Put '/merchant/' $bad $admin
    $unchanged = (Call-Api Get "/merchant/$fixture" $null $admin).data
    if($unchanged.status -ne 1) { throw 'Profile edit changed status.' }
    $lockBody=@{merchantId="$fixture";status=3;rejectionReason='Guard fixture lock';expectedVersion=$view.version;operationId=[Guid]::NewGuid().ToString()}
    $locked=Call-Api Put '/merchant/status' $lockBody $admin
    $summary=(Call-Api Get '/merchant/summary' $null $admin).data
    if($summary.active -ne ($activeBefore-1) -or $summary.suspended -ne ($suspendedBefore+1)) {
        throw 'Summary did not reflect the fixture lock.'
    }
    $bad.status = 1
    $bad.expectedVersion = $locked.data.version
    Assert-Status 400 Put '/merchant/' $bad $admin
    if((Sql "SELECT is_active FROM washgo.users WHERE id='$fixture';") -ne 'f') { throw 'Profile edit unlocked the login.' }
    $unlock=Call-Api Put '/merchant/status' @{merchantId="$fixture";status=1;expectedVersion=$locked.data.version} $admin
    $body.expectedVersion=$unlock.data.version

    foreach($case in @(
        @{city=('x'*101)}, @{district=('x'*101)}, @{commissionRate=12.345},
        @{depositAmount=1.001}, @{depositAmount=10000000000000000}, @{depositAmount=-1}
    )) {
        $bad=$body.Clone(); foreach($k in $case.Keys){$bad[$k]=$case[$k]}
        Assert-Status 400 Put '/merchant/' $bad $admin
    }
    $body.businessName = 'Merchant Guard Edited'
    $body.status = 1 # Old clients may echo the current status, but cannot change it.
    $body.commissionRate = 0
    $body.depositAmount = 1.25
    $write=$body.Clone();$write.operationId=[Guid]::NewGuid().ToString()
    $written=Call-Api Put '/merchant/' $write $admin
    if($written.data.version -isnot [string]) { throw 'Version must be an opaque JSON string.' }
    $auditCount=Sql "SELECT count(*) FROM washgo.audit_logs WHERE resource_id='$fixture' AND module='Merchant' AND action='Update';"
    $replay=Call-Api Put '/merchant/' $write $admin
    if($written.data.version -ne $replay.data.version) { throw 'Replay changed version.' }
    if((Sql "SELECT count(*) FROM washgo.audit_logs WHERE resource_id='$fixture' AND module='Merchant' AND action='Update';") -ne $auditCount){throw 'Replay duplicated the audit event.'}
    if((Sql "SELECT count(*) FROM washgo.merchant_write_operations WHERE id='$($write.operationId)';") -ne '1'){throw 'Replay duplicated the operation.'}
    $changed=$write.Clone();$changed.phone='0900000009'
    Assert-Status 409 Put '/merchant/' $changed $admin
    Assert-Status 409 Put '/merchant/' $body $admin # stale expectedVersion without operation
    $missing=$body.Clone();$missing.Remove('expectedVersion')
    Assert-Status 400 Put '/merchant/' $missing $admin
    $op=(Call-Api Get "/merchant/operations/$($write.operationId)" $null $admin).data
    if($op.result.data.version -ne $written.data.version) { throw 'Operation lookup did not return original result.' }
    $statusReplay=Call-Api Put '/merchant/status' $lockBody $admin
    if($statusReplay.data.version -ne $locked.data.version){throw 'Status replay did not return original snapshot.'}
    if((Call-Api Get "/merchant/$fixture" $null $admin).data.status -ne 1){throw 'Status replay locked the Merchant again.'}
    Assert-Status 403 Get "/merchant/operations/$($write.operationId)" $null $staff.accessToken
    $after = Sql "SELECT row_to_json(x) FROM washgo.merchant_service_areas x WHERE id='$link';"
    if($before -ne $after) { throw 'Saving the same area IDs changed link priority/assignment metadata.' }
    $saved=(Call-Api Get "/merchant/$fixture" $null $admin).data
    if($saved.businessName -ne $body.businessName -or $saved.commissionRate -ne 0 -or $saved.depositAmount -ne 1.25) {
        throw 'Valid profile values were not saved.'
    }
    $body.serviceAreaIds = @()
    $body.expectedVersion=$saved.version
    Call-Api Put '/merchant/' $body $admin | Out-Null
    if((Sql "SELECT count(*) FROM washgo.merchant_service_areas WHERE merchant_id='$fixture';") -ne '0') {
        throw 'Explicit empty area list did not remove the links.'
    }
    # Two saves from the same version: exactly one may win.
    $fresh=(Call-Api Get "/merchant/$fixture" $null $admin).data
    $body.expectedVersion=$fresh.version
    $jobs=@()
    try {
        foreach($i in 1..2) {
            $concurrent=$body.Clone();$concurrent.businessName="Concurrent Merchant $i";$concurrent.Remove('status')
            $jobs+=Start-Job -ScriptBlock {
                param($url,$token,$payload)
                try {Invoke-RestMethod -Method Put -Uri "$url/merchant/" -Headers @{Authorization="Bearer $token"} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload)) | Out-Null;200}
                catch{
                    $code=[int]$_.Exception.Response.StatusCode
                    if($code -ne 409){
                        $detail=$_.ErrorDetails.Message
                        if(!$detail -and $_.Exception.Response){$reader=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());$detail=$reader.ReadToEnd();$reader.Dispose()}
                        Write-Warning "Concurrent request: HTTP $code $detail"
                    }
                    $code
                }
            } -ArgumentList $BaseUrl,$admin,($concurrent|ConvertTo-Json -Depth 8)
        }
        $codes=@($jobs|Receive-Job -Wait)
        if((($codes|Sort-Object)-join ',') -ne '200,409'){throw "Concurrent saves returned $codes"}
    } finally {foreach($j in $jobs){Remove-Job $j -Force -ErrorAction SilentlyContinue}}

    # Merchant requests a change; an intervening Admin edit must block approval.
    $fixtureSession=(Call-Api Post '/auth/login' @{email="merchant-guard-$fixture@example.test";password='Merchant@123'} $null).data
    try {
        $profile=(Call-Api Get '/merchant-profile/' $null $fixtureSession.accessToken).data
        $proposal=@{businessName='Proposed Merchant';address=$profile.address;city=$profile.city;phone=$profile.phone;openingHours=$profile.openingHours}
        $req=(Call-Api Post '/merchant-profile/update-requests' $proposal $fixtureSession.accessToken).data
        $body.expectedVersion=(Call-Api Get "/merchant/$fixture" $null $admin).data.version
        $body.businessName='Admin changed after request';$body.Remove('status')
        Call-Api Put '/merchant/' $body $admin | Out-Null
        Assert-Status 409 Post "/merchant-profile-request/$($req.id)/approve?expectedVersion=$($req.version)" $null $admin
        $reject=@{reason='Please submit against current profile';expectedVersion=$req.version;operationId=[Guid]::NewGuid().ToString()}
        $rejected=(Call-Api Post "/merchant-profile-request/$($req.id)/reject" $reject $admin).data
        $again=(Call-Api Post "/merchant-profile-request/$($req.id)/reject" $reject $admin).data
        if($rejected.version -ne $again.version){throw 'Review replay changed result.'}
        $reject.reason='Different reason';Assert-Status 409 Post "/merchant-profile-request/$($req.id)/reject" $reject $admin
        $req2=(Call-Api Post '/merchant-profile/update-requests' $proposal $fixtureSession.accessToken).data
        $approvalId=[Guid]::NewGuid().ToString()
        $path="/merchant-profile-request/$($req2.id)/approve?expectedVersion=$($req2.version)&operationId=$approvalId"
        $approved=(Call-Api Post $path $null $admin).data
        $approvedAgain=(Call-Api Post $path $null $admin).data
        if($approved.status -ne 2 -or $approvedAgain.version -ne $approved.version){throw 'Approval/replay failed.'}
        if((Call-Api Get "/merchant/$fixture" $null $admin).data.businessName -ne $proposal.businessName){throw 'Approval did not persist.'}
        $proposal.businessName='Concurrent review proposal'
        $req3=(Call-Api Post '/merchant-profile/update-requests' $proposal $fixtureSession.accessToken).data
        $jobs=@()
        try {
            $approveUrl="$BaseUrl/merchant-profile-request/$($req3.id)/approve?expectedVersion=$($req3.version)"
            $rejectUrl="$BaseUrl/merchant-profile-request/$($req3.id)/reject"
            foreach($kind in @('approve','reject')){
                $url=if($kind -eq 'approve'){$approveUrl}else{$rejectUrl}
                $json=if($kind -eq 'reject'){@{reason='Concurrent rejection';expectedVersion=$req3.version}|ConvertTo-Json}else{'{}'}
                $jobs+=Start-Job -ScriptBlock {
                    param($url,$token,$json)
                    try{Invoke-RestMethod -Method Post -Uri $url -Headers @{Authorization="Bearer $token"} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json))|Out-Null;200}
                    catch{[int]$_.Exception.Response.StatusCode}
                } -ArgumentList $url,$admin,$json
            }
            $codes=@($jobs|Receive-Job -Wait)
            if((($codes|Sort-Object)-join ',') -ne '200,409'){throw "Concurrent review returned $codes"}
            if((Sql "SELECT count(*) FROM washgo.audit_logs WHERE resource_id='$($req3.id)' AND module='MerchantProfile' AND action IN ('Approve','Reject');") -ne '1'){
                throw 'Concurrent review created more than one audit result.'
            }
        }finally{foreach($j in $jobs){Remove-Job $j -Force -ErrorAction SilentlyContinue}}
    } finally {if($fixtureSession.accessToken){Call-Api Post '/auth/logout' @{refreshToken=$fixtureSession.refreshToken} $fixtureSession.accessToken | Out-Null}}
    Write-Output 'PASS: profile/status versions, concurrent save/review 200/409, operation replay/conflict/lookup without duplicate audit, stale approval blocked, reject/approve replay, 401/403, validation and preserved area metadata.'
}
finally {
    Sql "DELETE FROM washgo.merchant_write_operations WHERE merchant_id='$fixture'; DELETE FROM washgo.merchant_profile_requests WHERE merchant_id='$fixture'; DELETE FROM washgo.merchant_service_areas WHERE merchant_id='$fixture'; DELETE FROM washgo.merchants WHERE user_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture'; DELETE FROM washgo.service_areas WHERE id='$area';" | Out-Null
    foreach($s in @($session,$staff)) {
        if($s.accessToken) { try { Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken | Out-Null } catch { Write-Warning 'Could not revoke a smoke session.' } }
    }
}
