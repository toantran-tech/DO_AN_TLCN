param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: a fixture Shipper of the seed Merchant views and edits their own profile (UC 4.1.3-4.1.4).
$sessions=@{}
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$shipperUser=[Guid]::NewGuid(); $shipperId=[Guid]::NewGuid()
$email="shipper-profile-$run@example.com"

function Upload-Image($Token) {
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)
        $bytes=New-Object byte[] 64
        [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content,'file','avatar.png')
        return ($client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result|ConvertFrom-Json).data.url
    } finally {$client.Dispose()}
}
function Profile { (Call-Api Get '/shipper-profile/' $null $shipper).data }
function Body([hashtable]$Extra=@{}) {
    $p=Profile
    $b=@{expectedRevision=$p.revision;operationId=[Guid]::NewGuid().ToString();fullName="Profile Shipper $run";phone="09$((Get-Random -Minimum 10000000 -Maximum 99999999))";vehicleType=1;vehiclePlate='59X1-123.45'}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    $seedPhone=Sql "SELECT phone FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','$email','full_name','Old Shipper Name','phone',NULL,'avatar',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperId','user_id','$shipperUser','merchant_id','$seedMerchantId','status',1,'is_active',true,'vehicle_type',3,'vehicle_plate',NULL,'total_deliveries',7,'rating',4.5))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.S=(Call-Api Post '/auth/login' @{email=$email;password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken
    $merchantName=(Call-Api Get "/merchant/$seedMerchantId" $null $admin).data.businessName

    Write-Output '1. View profile (UC 4.1.3)'
    foreach($t in @($admin,$merchant,$customer)){ Assert-Status 403 Get '/shipper-profile/' $null $t }
    $p=Profile
    if($p.id -ne "$shipperId" -or $p.email -ne $email -or $p.fullName -ne 'Old Shipper Name' -or $p.vehicleType -ne 3 -or $p.status -ne 1 -or $p.totalDeliveries -ne 7 -or $p.rating -ne 4.5 -or $p.merchantId -ne "$seedMerchantId" -or $p.merchantName -ne $merchantName){throw 'Profile fields wrong.'}

    Write-Output '2. Validation (UC 4.1.4 7a, 7b)'
    Assert-Status 400 Put '/shipper-profile/' (Body @{phone='12ab'}) $shipper
    Assert-Status 400 Put '/shipper-profile/' (Body @{fullName=' '}) $shipper
    Assert-Status 400 Put '/shipper-profile/' (Body @{vehiclePlate='?'}) $shipper
    Assert-Status 400 Put '/shipper-profile/' (Body @{vehicleType=9}) $shipper
    if($seedPhone){ Assert-Status 400 Put '/shipper-profile/' (Body @{phone=$seedPhone}) $shipper }
    Assert-Status 400 Put '/shipper-profile/' (Body @{avatarUrl=(Upload-Image $customer)}) $shipper
    Assert-Status 400 Put '/shipper-profile/' (Body @{avatarUrl='/uploads/../appsettings.json'}) $shipper

    Write-Output '3. Update (UC 4.1.4 steps 6-10)'
    $avatar=Upload-Image $shipper
    $body=Body @{avatarUrl=$avatar;phone='0912 000 111'}
    $body.phone="09$((Get-Random -Minimum 10000000 -Maximum 99999999))".Insert(4,' ')
    $u=Call-Api Put '/shipper-profile/' $body $shipper
    $p=Profile
    if(!$u.message -or $p.fullName -ne "Profile Shipper $run" -or $p.phone -ne $body.phone.Replace(' ','') -or $p.avatarUrl -ne $avatar -or $p.vehicleType -ne 1 -or $p.vehiclePlate -ne '59X1-123.45'){throw 'Update not applied.'}
    if($p.email -ne $email -or $p.merchantId -ne "$seedMerchantId"){throw 'Read-only fields changed.'}
    $keep=Body @{phone=$p.phone}; $keep.Remove('avatarUrl')
    Call-Api Put '/shipper-profile/' $keep $shipper | Out-Null
    if((Profile).avatarUrl -ne $avatar){throw 'Omitted avatar was cleared.'}
    Call-Api Put '/shipper-profile/' (Body @{phone=$p.phone;avatarUrl='';vehicleType=3;vehiclePlate=$null}) $shipper | Out-Null
    $p=Profile
    if($p.avatarUrl -or $p.vehicleType -ne 3 -or $p.vehiclePlate){throw 'Avatar clear / bicycle without plate failed.'}
    if((Call-Api Get "/shipper/$shipperId" $null $merchant).data.fullName -ne "Profile Shipper $run"){throw 'Merchant view not updated.'}

    Write-Output 'PASS: profile view with Merchant name/deliveries/rating, 403 for other roles, phone/name/plate/vehicle/duplicate phone/foreign avatar checks, update fields and avatar, read-only email/Merchant, omitted avatar kept, empty avatar clears, bicycle needs no plate.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$shipperId'; UPDATE washgo.users SET is_active=false WHERE id='$shipperUser';" | Out-Null
}
