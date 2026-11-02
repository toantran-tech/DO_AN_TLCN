param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: Admin creates, edits, maintains and deletes a private locker owned by the seed Merchant.
$sessions=@{}; $lockerId=$null; $orderId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$area=[Guid]::NewGuid()

function Upload-Image($Token) {
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)
        $bytes=New-Object byte[] 64
        [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content,'file','locker.png')
        return ($client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result|ConvertFrom-Json).data.url
    } finally {$client.Dispose()}
}
function Locker-Body([hashtable]$Extra=@{}) {
    $b=@{merchantId="$seedMerchantId";serviceAreaId="$area";code="LA_$run";name="Admin Locker $run";address='Private development fixture'
         district='Locker District';city='Test';latitude=10.78;longitude=106.70;lockerType=1;description='Smoke';boxCount=3;defaultBoxSize=3}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}
function Detail { (Call-Api Get "/locker/$lockerId" $null $admin).data }
function Box-Status($BoxId,$Status) { Call-Api Put '/locker/box-status' @{lockerId="$lockerId";boxId="$BoxId";newStatus=$Status} $admin }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $now=[DateTime]::UtcNow.ToString('o')
    Sql "INSERT INTO washgo.service_areas(id,name,code,district,city,radius_km,is_active,created_at,updated_at) VALUES('$area','Locker Area $run','LA_$run','Test','Test',2,true,'$now','$now');" | Out-Null
    $serviceId=Sql "SELECT id FROM washgo.services WHERE merchant_id='$seedMerchantId' AND is_active AND NOT is_deleted AND status=2 ORDER BY code LIMIT 1;"
    $merchantName=(Call-Api Get "/merchant/$seedMerchantId" $null $admin).data.businessName
    $existing=(Call-Api Post '/locker/filter' @{page=1;take=1} $admin).list[0]

    Write-Output '1. List, filters, empty message (UC 3.4.1)'
    $empty=Call-Api Post '/locker/filter' @{serviceAreaId="$area"} $admin
    if($empty.total -ne 0 -or !$empty.message){throw 'Empty locker message missing.'}
    if(!$existing.merchantName){throw 'Merchant name missing in list.'}

    Write-Output '2. Create with validation (UC 3.4.2)'
    Assert-Status 403 Post '/locker/' (Locker-Body) $merchant
    Assert-Status 400 Post '/locker/' (Locker-Body @{name=$existing.name.ToUpper()}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{latitude=0;longitude=0}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{latitude=91}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{serviceAreaId="$([Guid]::NewGuid())"}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{boxCount=0}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{boxes=@(@{boxNumber='1';size=2},@{boxNumber='1';size=2})}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{merchantId="$([Guid]::NewGuid())"}) $admin
    Assert-Status 400 Post '/locker/' (Locker-Body @{imageUrls=@((Upload-Image $customer))}) $admin
    $img=Upload-Image $admin
    $created=Call-Api Post '/locker/' (Locker-Body @{imageUrls=@($img)}) $admin
    $lockerId=[Guid]$created.data.id
    if(!$created.message -or $created.data.totalBoxes -ne 3 -or $created.data.status -ne 1){throw 'Create failed.'}
    $d=Detail
    if($d.boxes.Count -ne 3 -or @($d.boxes | Where-Object { $_.size -ne 3 }).Count -ne 0 -or (@($d.boxes | ForEach-Object { $_.boxNumber }) -join ',') -ne '1,2,3'){throw 'Generated boxes wrong.'}
    if((@($d.imageUrls) -join ',') -ne $img -or $d.merchantName -ne $merchantName -or $d.serviceAreaName -ne "Locker Area $run"){throw 'Detail fields wrong.'}
    Assert-Status 400 Post '/locker/' (Locker-Body @{name="Other $run"}) $admin
    $list=Call-Api Post '/locker/filter' @{serviceAreaId="$area"} $admin
    if($list.total -ne 1 -or $list.list[0].serviceAreaName -ne "Locker Area $run" -or $list.list[0].emptyBoxesCount -ne 3){throw "Area filter wrong: $($list.total) $($list.list[0].serviceAreaName) $($list.list[0].emptyBoxesCount)"}

    Write-Output '3. Edit (UC 3.4.3)'
    $upd=Locker-Body @{id="$lockerId";status=1;name="Admin Locker Edited $run";description='Edited';latitude=10.79}
    $upd.Remove('imageUrls')
    $bad=$upd.Clone();$bad.name=$existing.name
    Assert-Status 400 Put '/locker/' $bad $admin
    $bad=$upd.Clone();$bad.longitude=181
    Assert-Status 400 Put '/locker/' $bad $admin
    $u=Call-Api Put '/locker/' $upd $admin
    if(!$u.message){throw 'Update message missing.'}
    $d=Detail
    if($d.name -ne "Admin Locker Edited $run" -or $d.description -ne 'Edited' -or $d.latitude -ne 10.79 -or (@($d.imageUrls) -join ',') -ne $img){throw 'Update not applied or images lost.'}
    $upd.serviceAreaId=$null;$upd.imageUrls=@()
    Call-Api Put '/locker/' $upd $admin | Out-Null
    $d=Detail
    if($d.serviceAreaId -or @($d.imageUrls).Count -ne 0){throw 'Area/images not cleared.'}
    $upd.serviceAreaId="$area";$upd.Remove('imageUrls')
    Call-Api Put '/locker/' $upd $admin | Out-Null

    Write-Output '4. Box maintenance (UC 3.4.5)'
    $b1=$d.boxes[0].id;$b2=$d.boxes[1].id
    Assert-Status 400 Put '/locker/box-status' @{lockerId="$lockerId";boxId="$b1";newStatus=2} $admin
    if(!(Box-Status $b1 4).message){throw 'Maintenance failed.'}
    if((Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots.Count -ne 2){throw 'Maintenance box still offered.'}
    Box-Status $b1 1 | Out-Null
    Box-Status $b1 4 | Out-Null
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId="$b2";paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    $orderId=$o.id
    Assert-Status 400 Put '/locker/box-status' @{lockerId="$lockerId";boxId="$b2";newStatus=4} $admin
    Assert-Status 400 Delete "/locker/$lockerId" $null $admin
    Invoke-TestDelivery $orderId deposit $customer | Out-Null
    Assert-Status 400 Put '/locker/box-status' @{lockerId="$lockerId";boxId="$b2";newStatus=4} $admin
    $h=Call-Api Get "/locker/$lockerId/usage-history?boxId=$b2" $null $admin
    if($h.total -ne 1 -or $h.list[0].orderCode -ne $o.orderCode){throw 'Box usage history wrong.'}
    if((Call-Api Get "/locker/$lockerId/usage-history?boxId=$b1" $null $admin).total -ne 0){throw 'Unused box has history.'}

    Write-Output '5. Delete (UC 3.4.4)'
    Assert-Status 403 Delete "/locker/$lockerId" $null $merchant
    Assert-Status 400 Delete "/locker/$lockerId" $null $admin
    # Fixture shortcut: finish the order directly so the locker is no longer in use.
    Sql "UPDATE washgo.orders SET status=12 WHERE id='$orderId'; UPDATE washgo.locker_slots SET status=1 WHERE id='$b2';" | Out-Null
    if(!(Call-Api Delete "/locker/$lockerId" $null $admin).message){throw 'Delete failed.'}
    Assert-Status 404 Get "/locker/$lockerId" $null $admin
    Assert-Status 404 Get "/customer/lockers/$lockerId" $null $customer
    Assert-Status 404 Delete "/locker/$lockerId" $null $admin
    if((Call-Api Post '/locker/filter' @{serviceAreaId="$area"} $admin).total -ne 0){throw 'Deleted locker still listed.'}
    if((Sql "SELECT count(*) FROM washgo.locker_slots WHERE locker_id='$lockerId' AND NOT is_deleted;") -ne '0'){throw 'Boxes not deleted.'}
    $reuse=(Call-Api Post '/locker/' (Locker-Body @{code="LB_$run";name="Admin Locker Edited $run"}) $admin).data
    Sql "UPDATE washgo.lockers SET is_deleted=true,status=3 WHERE id='$($reuse.id)'; UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$($reuse.id)';" | Out-Null

    Write-Output 'PASS: list names/filters/empty message, create validation (name, coordinates, area, merchant, boxes, images), generated boxes, edit keeps/clears images and area, box maintenance rules, per-box history, delete blocked while in use then soft delete, name reusable after delete.'
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
    Sql "UPDATE washgo.service_areas SET is_active=false WHERE id='$area';" | Out-Null
}
