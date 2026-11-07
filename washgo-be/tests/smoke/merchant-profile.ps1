param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: a fixture Merchant B requests profile updates; Admin rejects/approves them.
$sessions=@{}
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid()

function Upload-Image($Token) {
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)
        $bytes=New-Object byte[] 64
        [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content,'file','store.png')
        $body=$client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result
        return ($body|ConvertFrom-Json).data.url
    } finally {$client.Dispose()}
}
function Notes($Token,$Type) { @((Call-Api Get '/notification?take=100' $null $Token).list | Where-Object { $_.type -eq $Type }) }
function Profile { (Call-Api Get '/merchant-profile/' $null $merchant).data }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$seedMerchant=$sessions.Merchant.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','profile-b-$run@example.com','full_name','Profile Smoke Merchant B','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Profile Smoke Laundry B','tax_code',NULL,'status',1,'phone','0281234567','address','1 Old Street','city','Ho Chi Minh','latitude',10.762622,'longitude',106.660172,'opening_hours','08:00-20:00'))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="profile-b-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.B.accessToken

    Write-Output '1. Merchant views store profile'
    $p=Profile
    if($p.businessName -ne 'Profile Smoke Laundry B' -or $p.email -ne "profile-b-$run@example.com" -or $p.phone -ne '0281234567' -or $p.city -ne 'Ho Chi Minh' -or $p.latitude -ne 10.762622 -or $p.longitude -ne 106.660172 -or $null -eq $p.joinedAt -or $null -eq $p.publicRatingCount -or $p.pendingRequest -or $p.imageUrls.Count -ne 0){throw 'Profile view wrong.'}
    Assert-Status 403 Get '/merchant-profile/' $null $admin

    Write-Output '2. Validation (UC 2.1.2 6a, 6b)'
    $req=@{businessName="Profile Laundry $run";address='99 New Street';city='Can Tho';latitude=10.045162;longitude=105.746857;phone='0901 234 567';openingHours='07:00-22:00'}
    $bad=$req.Clone();$bad.phone='12ab'
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.phone='123'
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.businessName=' '
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.address=''
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.city=''
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.latitude=95
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.longitude=190
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $foreign=Upload-Image $seedMerchant
    $bad=$req.Clone();$bad.imageUrls=@($foreign)
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    $bad=$req.Clone();$bad.imageUrls=@('/uploads/../appsettings.json')
    Assert-Status 400 Post '/merchant-profile/update-requests' $bad $merchant
    Assert-Status 400 Post '/merchant-profile/update-requests' @{businessName='Profile Smoke Laundry B';address='1 Old Street';city='Ho Chi Minh';latitude=10.762622;longitude=106.660172;phone='028 1234 567';openingHours='08:00-20:00'} $merchant

    Write-Output '3. Submit request: pending, profile unchanged, Admin notified (UC 2.1.2 7-9)'
    $img=Upload-Image $merchant
    $req.imageUrls=@($img)
    $r1=Call-Api Post '/merchant-profile/update-requests' $req $merchant
    if($r1.data.status -ne 1 -or $r1.data.proposed.phone -ne '0901234567' -or $r1.data.proposed.city -ne 'Can Tho' -or $r1.data.proposed.latitude -ne 10.045162 -or $r1.data.proposed.longitude -ne 105.746857 -or $r1.data.current.businessName -ne 'Profile Smoke Laundry B'){throw 'Request data wrong.'}
    if((@($r1.data.changedFields) -join ',') -ne 'businessName,address,city,latitude,longitude,phone,openingHours,imageUrls'){throw "Changed fields wrong: $($r1.data.changedFields)"}
    $p=Profile
    if($p.businessName -ne 'Profile Smoke Laundry B' -or $p.pendingRequest.id -ne $r1.data.id){throw 'Profile changed before approval.'}
    Assert-Status 400 Post '/merchant-profile/update-requests' $req $merchant
    if(@(Notes $admin 47 | Where-Object { $_.linkId -eq $r1.data.id }).Count -ne 1){throw 'Admin not notified.'}

    Write-Output '4. Lists and scope'
    $list=Call-Api Post '/merchant-profile-request/filter' @{status=1;merchantId="$merchantB"} $admin
    if($list.total -ne 1 -or $list.list[0].merchantName -ne 'Profile Smoke Laundry B'){throw 'Admin list wrong.'}
    $mine=Call-Api Post '/merchant-profile-request/filter' @{merchantId="$seedMerchantId"} $merchant
    if($mine.total -ne 1 -or $mine.list[0].id -ne $r1.data.id){throw 'Merchant scope wrong.'}
    Assert-Status 404 Get "/merchant-profile-request/$($r1.data.id)" $null $seedMerchant
    Assert-Status 403 Post "/merchant-profile-request/$($r1.data.id)/approve" $null $merchant

    Write-Output '5. Reject with reason'
    Assert-Status 400 Post "/merchant-profile-request/$($r1.data.id)/reject" @{reason=' '} $admin
    $rej=(Call-Api Post "/merchant-profile-request/$($r1.data.id)/reject" @{reason='Anh chua ro'} $admin).data
    if($rej.status -ne 3 -or $rej.rejectionReason -ne 'Anh chua ro'){throw 'Reject failed.'}
    Assert-Status 400 Post "/merchant-profile-request/$($r1.data.id)/approve" $null $admin
    if((Profile).businessName -ne 'Profile Smoke Laundry B'){throw 'Rejected request changed the profile.'}
    if(@(Notes $merchant 27 | Where-Object { $_.content -match 'Anh chua ro' }).Count -ne 1){throw 'Merchant not told the reason.'}

    Write-Output '6. Approve applies the update'
    $r2=(Call-Api Post '/merchant-profile/update-requests' $req $merchant).data
    $ok=Call-Api Post "/merchant-profile-request/$($r2.id)/approve" $null $admin
    if($ok.data.status -ne 2){throw 'Approve failed.'}
    $p=Profile
    if($p.businessName -ne "Profile Laundry $run" -or $p.address -ne '99 New Street' -or $p.city -ne 'Can Tho' -or $p.latitude -ne 10.045162 -or $p.longitude -ne 105.746857 -or $p.phone -ne '0901234567' -or $p.openingHours -ne '"07:00-22:00"' -or (@($p.imageUrls) -join ',') -ne $img -or $p.pendingRequest){throw 'Approved update not applied.'}
    if((Call-Api Get "/merchant/$merchantB" $null $admin).data.businessName -ne "Profile Laundry $run"){throw 'Admin view not updated.'}
    if(@(Notes $merchant 27).Count -ne 2){throw 'Merchant not notified of approval.'}

    Write-Output '7. Omitted images are kept; removing images works'
    $r3=(Call-Api Post '/merchant-profile/update-requests' @{businessName="Profile Laundry $run";address='99 New Street';city='Can Tho';latitude=10.045162;longitude=105.746857;phone='0901234567';openingHours='{"mon":"06:00-23:00"}'} $merchant).data
    if((@($r3.changedFields) -join ',') -ne 'openingHours' -or (@($r3.proposed.imageUrls) -join ',') -ne $img){throw "Omitted images not kept: $(@($r3.changedFields) -join ',')"}
    Call-Api Post "/merchant-profile-request/$($r3.id)/approve" $null $admin | Out-Null
    $p=Profile
    if(($p.openingHours | ConvertFrom-Json).mon -ne '06:00-23:00' -or (@($p.imageUrls) -join ',') -ne $img){throw 'Hours-only update wrong.'}
    $r4=(Call-Api Post '/merchant-profile/update-requests' @{businessName="Profile Laundry $run";address='99 New Street';city='Can Tho';latitude=10.045162;longitude=105.746857;phone='0901234567';openingHours='{"mon":"06:00-23:00"}';imageUrls=@()} $merchant).data
    Call-Api Post "/merchant-profile-request/$($r4.id)/approve" $null $admin | Out-Null
    if(@((Profile).imageUrls).Count -ne 0){throw 'Images not removed.'}

    Write-Output 'PASS: profile view (with City, Lat/Long, JoinedAt, PublicRatingCount), phone/name/address/city/coordinates/image validation, no-change guard, pending request + Admin notification, one pending, scope, reject with reason, approve applies fields and images, omitted images kept, images removable.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$merchantB'; UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB';"|Out-Null
}
