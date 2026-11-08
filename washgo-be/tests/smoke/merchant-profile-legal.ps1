param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Merchant profile update requests with tax code / business licence and a coordinate pair.
$sessions=@{}; $teamId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$tax='0'+((Get-Random -Minimum 100000000 -Maximum 999999999).ToString())

try {
    $sessions.Admin=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data; $admin=$sessions.Admin.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId="$($sessions.Team.user.id)"
    $p=(Call-Api Get '/merchant-profile/' $null $team).data
    function Req($Extra){ $b=@{businessName=$p.businessName;address=$p.address;city=$p.city;phone=$p.phone;openingHours='08:00-21:00'}; foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b }
    $licImg=Upload-TestImage $team; $foreign=Upload-TestImage $admin

    Write-Output '1. Coordinates come in pairs; a new address needs a new location'
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{latitude=10.5}) $team
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{longitude=106.5}) $team
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{address="12 New Road $run"}) $team

    Write-Output '2. Tax code and licence validation'
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{taxCode='12345'}) $team
    $used=Sql "SELECT tax_code FROM washgo.merchants WHERE tax_code IS NOT NULL AND user_id<>'$teamId' LIMIT 1;"
    if($used){ Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{taxCode=$used}) $team }
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{businessLicense=$foreign}) $team
    Assert-Status 400 Post '/merchant-profile/update-requests' (Req @{businessLicense=('x'*101)}) $team

    Write-Output '3. Request carries current/proposed legal fields; applied only on approval'
    $r=(Call-Api Post '/merchant-profile/update-requests' (Req @{taxCode=$tax;businessLicense=$licImg;address="12 New Road $run";latitude=10.81;longitude=106.71}) $team).data
    if((@($r.changedFields) -join ',') -notmatch 'taxCode' -or (@($r.changedFields) -join ',') -notmatch 'businessLicense' -or $r.proposed.taxCode -ne $tax -or $r.proposed.businessLicense -ne $licImg){throw "Request wrong: $(@($r.changedFields) -join ',')"}
    if((Call-Api Get '/merchant-profile/' $null $team).data.taxCode -eq $tax){throw 'Tax code applied before approval.'}
    Call-Api Post "/merchant-profile-request/$($r.id)/approve" $null $admin | Out-Null
    $after=(Call-Api Get '/merchant-profile/' $null $team).data
    if($after.taxCode -ne $tax -or $after.businessLicense -ne $licImg -or $after.latitude -ne 10.81 -or $after.address -ne "12 New Road $run"){throw "Approval not applied: $($after.taxCode) $($after.businessLicense)"}

    Write-Output '4. Licence number text, clearing the tax code, older requests keep values'
    $p=(Call-Api Get '/merchant-profile/' $null $team).data
    $r2=(Call-Api Post '/merchant-profile/update-requests' (Req @{taxCode='';businessLicense="GPKD-$run"}) $team).data
    Call-Api Post "/merchant-profile-request/$($r2.id)/approve" $null $admin | Out-Null
    $after=(Call-Api Get '/merchant-profile/' $null $team).data
    if($after.taxCode -or $after.businessLicense -ne "GPKD-$run"){throw 'Clear/licence number not applied.'}
    $r3=(Call-Api Post '/merchant-profile/update-requests' (Req @{openingHours='07:00-22:00'}) $team).data
    Sql "UPDATE washgo.merchant_profile_requests SET proposed_data = proposed_data - 'taxCode' - 'businessLicense' WHERE id='$($r3.id)';" | Out-Null
    Call-Api Post "/merchant-profile-request/$($r3.id)/approve" $null $admin | Out-Null
    if((Call-Api Get '/merchant-profile/' $null $team).data.businessLicense -ne "GPKD-$run"){throw 'Old request without the fields changed the licence.'}

    Write-Output 'PASS: latitude/longitude required together and with any address/city change; tax code format and uniqueness; licence image must be the Merchant''s own upload, licence number length; request shows current/proposed/changed taxCode and businessLicense, applied only when the Admin approves; empty string clears; requests without the fields keep current values.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($teamId){ Sql "UPDATE washgo.merchant_profile_requests SET status=3 WHERE merchant_id='$teamId' AND status=1; UPDATE washgo.merchants SET tax_code=NULL WHERE user_id='$teamId';" | Out-Null }
    Remove-FixtureMerchant $teamId
}
