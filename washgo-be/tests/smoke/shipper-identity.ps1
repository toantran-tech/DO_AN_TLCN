param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: identity card front/back on create and update, legacy unknown-side images, audit of changes.
$sessions=@{}; $teamId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
function Body($Extra){ $b=@{fullName="Id $run";phone=('09'+(Get-Random -Minimum 10000000 -Maximum 99999999));email="id-$run-$(Get-Random)@example.com";vehicleType=1;vehiclePlate='59-X1 123.45'}; foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b }
function Edit($S,$Extra){ $b=@{fullName=$S.fullName;phone=$S.phone;email=$S.email;vehicleType=$S.vehicleType;vehiclePlate=$S.vehiclePlate;isActive=$S.isActive}; foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $merchant=$sessions.Merchant.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $f1=Upload-TestImage $team; $b1=Upload-TestImage $team; $f2=Upload-TestImage $team; $b2=Upload-TestImage $team
    $foreign=Upload-TestImage $merchant

    Write-Output '1. Create: both sides or none, owned uploads, not mixed with the old array'
    Assert-Status 400 Post '/shipper/' (Body @{identityFrontImageUrl=$f1}) $team
    Assert-Status 400 Post '/shipper/' (Body @{identityFrontImageUrl=$f1;identityBackImageUrl=$f1}) $team
    Assert-Status 400 Post '/shipper/' (Body @{identityFrontImageUrl=$f1;identityBackImageUrl=$foreign}) $team
    Assert-Status 400 Post '/shipper/' (Body @{identityFrontImageUrl=$f1;identityBackImageUrl=$b1;identityImageUrls=@($f2)}) $team
    $s=(Call-Api Post '/shipper/' (Body @{identityNumber='079123456789';identityFrontImageUrl=$f1;identityBackImageUrl=$b1}) $team).data
    if($s.identityFrontImageUrl -ne $f1 -or $s.identityBackImageUrl -ne $b1 -or $s.identityImageSides -ne 'FrontBack' -or (@($s.identityImageUrls) -join ',') -ne "$f1,$b1"){throw "Create sides wrong: $($s | ConvertTo-Json -Compress)"}
    $none=(Call-Api Post '/shipper/' (Body @{}) $team).data
    if($none.identityImageSides -ne 'None'){throw 'No images should be None.'}
    $legacy=(Call-Api Post '/shipper/' (Body @{identityImageUrls=@($f2,$b2)}) $team).data
    if($legacy.identityImageSides -ne 'Unspecified' -or $legacy.identityFrontImageUrl -or @($legacy.identityImageUrls).Count -ne 2){throw 'Legacy array not marked Unspecified.'}

    Write-Output '2. Update: null keeps, sides replace legacy, partial rejected, clear both, number edit'
    $u=(Call-Api Put "/shipper/$($s.id)" (Edit $s @{}) $team).data
    if($u.identityFrontImageUrl -ne $f1 -or $u.identityNumber -ne '079123456789'){throw 'Null did not keep identity data.'}
    Assert-Status 400 Put "/shipper/$($s.id)" (Edit $s @{identityFrontImageUrl=''}) $team
    Assert-Status 400 Put "/shipper/$($s.id)" (Edit $s @{identityNumber='123'}) $team
    Assert-Status 400 Put "/shipper/$($s.id)" (Edit $s @{identityBackImageUrl=$foreign}) $team
    $u=(Call-Api Put "/shipper/$($s.id)" (Edit $s @{identityBackImageUrl=$b2;identityNumber='012345678'}) $team).data
    if($u.identityFrontImageUrl -ne $f1 -or $u.identityBackImageUrl -ne $b2 -or $u.identityNumber -ne '012345678'){throw 'Back side/number update wrong.'}
    $l=(Call-Api Put "/shipper/$($legacy.id)" (Edit $legacy @{identityFrontImageUrl=$f2;identityBackImageUrl=$b2}) $team).data
    if($l.identityImageSides -ne 'FrontBack' -or (@($l.identityImageUrls) -join ',') -ne "$f2,$b2"){throw 'Legacy images not replaced by sides.'}
    $c=(Call-Api Put "/shipper/$($s.id)" (Edit $s @{identityFrontImageUrl='';identityBackImageUrl='';identityNumber=''}) $team).data
    if($c.identityImageSides -ne 'None' -or $c.identityFrontImageUrl -or $c.identityNumber){throw 'Clearing identity failed.'}

    Write-Output '3. Audit records which fields changed, never the values'
    $audit=@((Call-Api Get "/shipper/$($s.id)/audits" $null $team).data | Where-Object { [int]$_.action -eq 4 })
    if($audit.Count -ne 2 -or $audit[1].details -ne 'identityNumber,identityBackImage' -or $audit[0].details -ne 'identityNumber,identityFrontImage,identityBackImage'){throw "Identity audit wrong: $(($audit | ForEach-Object { $_.details }) -join ' | ')"}
    if(($audit | ForEach-Object { $_.details }) -match '0123|0791|uploads'){throw 'Audit leaked identity values.'}
    $la=@((Call-Api Get "/shipper/$($legacy.id)/audits" $null $team).data)[0]
    if($la.details -notmatch 'legacyIdentityImagesReplaced'){throw 'Legacy replacement not audited.'}

    Write-Output 'PASS: identity front/back on create (both or none, distinct, owned uploads, not mixed with the old array); None/FrontBack/Unspecified sides; update keeps on null, replaces one side, rejects partial or foreign images and bad numbers, replaces legacy images, clears with empty strings; audit lists changed fields without values.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Remove-FixtureMerchant $teamId
}
