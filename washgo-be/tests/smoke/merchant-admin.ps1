param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: Admin creates, edits, locks and unlocks a fixture Merchant.
$sessions=@{}; $merchantId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$area=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid()
$email="merchant-admin-$run@example.com"

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
        return ($client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result|ConvertFrom-Json).data.url
    } finally {$client.Dispose()}
}
function Body([hashtable]$Extra=@{}) {
    $b=@{email=$email;businessName="Admin Merchant $run";taxCode="TX$run";businessLicense="GP-$run";address='1 Fixture Street';district='Merchant District'
         city='Test';latitude=10.75;longitude=106.65;phone='0912 345 678';openingHours='07:00-21:00';serviceRadiusKm=4;maxCapacity=30
         contractStartDate='2026-01-01T00:00:00Z';contractEndDate='2027-01-01T00:00:00Z';slaDocUrl='/uploads/sla.pdf';serviceAreaIds=@("$area")}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}
function Get-Merchant { (Call-Api Get "/merchant/$merchantId" $null $admin).data }
function Status-Body($Status,[hashtable]$Extra=@{}) { $b=@{merchantId="$merchantId";status=$Status};foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]};$b }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$seedMerchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $now=[DateTime]::UtcNow.ToString('o')
    Sql "INSERT INTO washgo.service_areas(id,name,code,district,city,radius_km,is_active,created_at,updated_at) VALUES('$area','Merchant Area $run','MA_$run','Test','Test',2,true,'$now','$now');" | Out-Null

    Write-Output '1. List (UC 3.2.1)'
    Assert-Status 403 Post '/merchant/filter' @{} $seedMerchant
    $none=Call-Api Post '/merchant/filter' @{keyword="nobody-$run"} $admin
    if($none.total -ne 0 -or !$none.message){throw 'Empty merchant message missing.'}

    Write-Output '2. Create with account (UC 3.2.2)'
    Assert-Status 400 Post '/merchant/' (Body @{email='not-an-email'}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{email='admin@washgo.vn'}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{latitude=0;longitude=0}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{latitude=95}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{phone='12ab'}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{commissionRate=150}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{serviceAreaIds=@("$([Guid]::NewGuid())")}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{contractEndDate='2025-01-01T00:00:00Z'}) $admin
    $seedTax=Sql "SELECT tax_code FROM washgo.merchants WHERE tax_code IS NOT NULL LIMIT 1;"
    if($seedTax){ Assert-Status 400 Post '/merchant/' (Body @{taxCode=$seedTax}) $admin }
    $img=Upload-Image $admin
    $c=Call-Api Post '/merchant/' (Body @{imageUrls=@($img)}) $admin
    $merchantId=[Guid]$c.data.id
    $m=Get-Merchant
    if(!$c.message -or $m.email -ne $email -or $m.status -ne 1 -or $m.phone -ne '0912345678' -or $m.taxCode -ne "TX$run" -or $m.businessLicense -ne ('"GP-'+$run+'"')){throw 'Create fields wrong.'}
    if((@($m.imageUrls) -join ',') -ne $img -or (@($m.serviceAreaIds) -join ',') -ne "$area" -or $m.maxCapacity -ne 30 -or !$m.contractEndDate -or $m.slaDocUrl -ne '/uploads/sla.pdf'){throw 'Create details wrong.'}
    if((Sql "SELECT r.name FROM washgo.users u JOIN washgo.roles r ON r.id=u.role_id WHERE u.id='$merchantId';") -ne 'Merchant'){throw 'Account role wrong.'}
    Assert-Status 400 Post '/merchant/' (Body @{businessName="Dup $run"}) $admin
    Assert-Status 400 Post '/merchant/' (Body @{email="other-$run@example.com"}) $admin
    # Fixture shortcut: give the new account a known password instead of reading the email.
    Sql "UPDATE washgo.users SET password_hash=(SELECT password_hash FROM washgo.users WHERE id='$seedMerchantId') WHERE id='$merchantId';" | Out-Null
    $sessions.N=(Call-Api Post '/auth/login' @{email=$email;password='Merchant@123'} $null).data
    if(!$sessions.N.accessToken){throw 'New Merchant cannot log in.'}

    Write-Output '3. Edit (UC 3.2.3)'
    $upd=Body @{id="$merchantId";businessName="Admin Merchant Edited $run";phone='0987 654 321';commissionRate=12;serviceRadiusKm=3;maxCapacity=10}
    $upd.Remove('serviceAreaIds')
    $bad=$upd.Clone();$bad.phone='123'
    Assert-Status 400 Put '/merchant/' $bad $admin
    $bad=$upd.Clone();$bad.commissionRate=101
    Assert-Status 400 Put '/merchant/' $bad $admin
    $bad=$upd.Clone();$bad.email='admin@washgo.vn'
    Assert-Status 400 Put '/merchant/' $bad $admin
    $u=Call-Api Put '/merchant/' $upd $admin
    $m=Get-Merchant
    if(!$u.message -or $m.businessName -ne "Admin Merchant Edited $run" -or $m.phone -ne '0987654321' -or $m.commissionRate -ne 12 -or $m.maxCapacity -ne 10 -or (@($m.serviceAreaIds) -join ',') -ne "$area" -or (@($m.imageUrls) -join ',') -ne $img){throw 'Edit not applied.'}
    $upd.commissionRate=$null
    Call-Api Put '/merchant/' $upd $admin | Out-Null
    if((Get-Merchant).commissionRate -ne 12){throw 'Omitted commission changed.'}
    if((Call-Api Post '/merchant/filter' @{serviceAreaId="$area"} $admin).total -ne 1){throw 'Area filter wrong.'}

    Write-Output '4. Lock / unlock (UC 3.2.4)'
    Sql @"
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantId','code','MA_$run','name','Merchant Admin Package','is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND is_deleted=false AND status=2 LIMIT 1;
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantId";code="MAL_$run";name="Merchant Admin Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.76;longitude=106.66;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer | Out-Null
    $m=Get-Merchant
    if($m.totalOrders -ne 1 -or $m.unfinishedOrders -ne 1){throw 'Order stats wrong.'}
    Assert-Status 400 Put '/merchant/status' (Status-Body 3) $admin
    Assert-Status 409 Put '/merchant/status' (Status-Body 3 @{rejectionReason='Vi pham SLA'}) $admin
    if((Get-Merchant).status -ne 1){throw 'Merchant locked without confirmation.'}
    $lock=Call-Api Put '/merchant/status' (Status-Body 3 @{rejectionReason='Vi pham SLA';confirm=$true}) $admin
    if($lock.data.status -ne 3 -or $lock.data.rejectionReason -ne 'Vi pham SLA'){throw 'Lock failed.'}
    Assert-Status 400 Post '/auth/login' @{email=$email;password='Merchant@123'} $null
    Assert-Status 400 Post '/auth/refresh-token' @{refreshToken=$sessions.N.refreshToken} $null
    $sessions.Remove('N')
    Assert-Status 404 Get "/customer/lockers/$lockerId" $null $customer
    Assert-Status 400 Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[1].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer
    if((Sql "SELECT count(*) FROM washgo.notifications WHERE user_id='$merchantId' AND type=29;") -ne '1'){throw 'Lock notification missing.'}
    $unlock=Call-Api Put '/merchant/status' (Status-Body 1) $admin
    if($unlock.data.status -ne 1 -or $unlock.data.rejectionReason){throw 'Unlock failed.'}
    $sessions.N=(Call-Api Post '/auth/login' @{email=$email;password='Merchant@123'} $null).data
    if((Sql "SELECT count(*) FROM washgo.notifications WHERE user_id='$merchantId' AND type=29;") -ne '2'){throw 'Unlock notification missing.'}

    Write-Output 'PASS: list empty message/403, create account + profile with validation (email, coordinates, phone, commission, area, contract dates, tax code, duplicates), role and login, edit validation and fields, commission kept when omitted, area filter, order stats, lock needs reason and confirmation with unfinished orders, lock hides lockers, blocks login/refresh/new orders and notifies, unlock restores.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    if($merchantId){ Sql "UPDATE washgo.services SET is_deleted=true,is_active=false WHERE merchant_id='$merchantId'; UPDATE washgo.users SET is_active=false WHERE id='$merchantId'; UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantId';" | Out-Null }
    Sql "UPDATE washgo.service_areas SET is_active=false WHERE id='$area';" | Out-Null
}
