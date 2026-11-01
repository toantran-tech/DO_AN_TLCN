param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: Admin assigns fixture Merchants to a fixture service area with priorities.
$sessions=@{}; $areaId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$m1=[Guid]::NewGuid();$m2=[Guid]::NewGuid();$m3=[Guid]::NewGuid()

function Merchant-Sql($Id,$Name,$Status) {
@"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$Id','email','area-$Id@example.com','full_name','$Name','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$Id','business_name','$Name','tax_code',NULL,'status',$Status))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@
}
function Area-Merchants { (Call-Api Get "/service-area/$areaId/merchants" $null $admin).data }
function Set-Merchants($Items,[bool]$Confirm=$false) { Call-Api Put "/service-area/$areaId/merchants" @{merchants=@($Items);confirmEmpty=$Confirm} $admin }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    Sql ((Merchant-Sql $m1 "Area One $run" 1) + (Merchant-Sql $m2 "Area Two $run" 1) + (Merchant-Sql $m3 "Area Locked $run" 3)) | Out-Null
    $areaId=(Call-Api Post '/service-area/' @{name="Smoke Area $run";code="SA_$run";district='Area District';city='Test';radiusKm=2;isActive=$true} $admin).data.id

    Write-Output '1. Area Merchant list (UC 3.2.5 steps 1-4)'
    Assert-Status 403 Get "/service-area/$areaId/merchants" $null $merchant
    Assert-Status 404 Get "/service-area/$([Guid]::NewGuid())/merchants" $null $admin
    $a=Area-Merchants
    $ids=@($a.merchants | ForEach-Object { $_.merchantId })
    if($ids -notcontains "$m1" -or $ids -notcontains "$m2" -or $ids -contains "$m3" -or @($a.merchants | Where-Object { $_.assigned }).Count -ne 0 -or $a.area.merchantCount -ne 0){throw 'Area Merchant list wrong.'}

    Write-Output '2. Validation and empty warning (5a)'
    Assert-Status 409 Put "/service-area/$areaId/merchants" @{merchants=@()} $admin
    Assert-Status 400 Put "/service-area/$areaId/merchants" @{merchants=@(@{merchantId="$m1";priority=1},@{merchantId="$m1";priority=2})} $admin
    Assert-Status 400 Put "/service-area/$areaId/merchants" @{merchants=@(@{merchantId="$m1";priority=0})} $admin
    Assert-Status 400 Put "/service-area/$areaId/merchants" @{merchants=@(@{merchantId="$m3";priority=1})} $admin
    Assert-Status 400 Put "/service-area/$areaId/merchants" @{merchants=@(@{merchantId="$([Guid]::NewGuid())";priority=1})} $admin
    Assert-Status 404 Put "/service-area/$([Guid]::NewGuid())/merchants" @{merchants=@(@{merchantId="$m1";priority=1})} $admin

    Write-Output '3. Assign with priorities (steps 5-9)'
    $r=Set-Merchants @(@{merchantId="$m2";priority=2},@{merchantId="$m1";priority=1})
    if(!$r.message -or $r.data.merchants[0].merchantId -ne "$m1" -or $r.data.merchants[0].priority -ne 1 -or $r.data.merchants[1].merchantId -ne "$m2" -or $r.data.merchants[1].priority -ne 2){throw 'Assignment wrong.'}
    if((Call-Api Get "/service-area/$areaId" $null $admin).data.merchantCount -ne 2){throw 'Merchant count wrong.'}
    if((Call-Api Post '/merchant/filter' @{serviceAreaId="$areaId"} $admin).total -ne 2){throw 'Merchant area filter wrong.'}
    if(@((Call-Api Get "/merchant/$m1" $null $admin).data.serviceAreaIds) -notcontains $areaId){throw 'Merchant detail missing area.'}
    Set-Merchants @(@{merchantId="$m2";priority=1}) | Out-Null
    $a=Area-Merchants
    if(@($a.merchants | Where-Object { $_.assigned }).Count -ne 1 -or ($a.merchants | Where-Object { $_.merchantId -eq "$m1" }).assigned){throw 'Removed Merchant still assigned.'}
    $e=Set-Merchants @() $true
    if($e.data.area.merchantCount -ne 0){throw 'Confirmed empty save failed.'}

    Write-Output 'PASS: area Merchant list (active only, assignment flags), 403/404, empty-area warning and confirm, duplicate/priority/inactive/unknown Merchant checks, priorities saved and ordered, counts and Merchant area filter, replace removes Merchants.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Sql "UPDATE washgo.users SET is_active=false WHERE id IN('$m1','$m2','$m3'); UPDATE washgo.merchants SET status=3 WHERE user_id IN('$m1','$m2','$m3');" | Out-Null
    if($areaId){ Sql "UPDATE washgo.merchant_service_areas SET is_active=false WHERE service_area_id='$areaId'; UPDATE washgo.service_areas SET is_active=false WHERE id='$areaId';" | Out-Null }
}
