param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: private locker/service/customer/shipper; orders are driven to Completed
# through the real action endpoints. Only fixture timeline rows are edited by SQL.
$sessions=@{}; $lockerId=$null
$kgServiceId=[Guid]::NewGuid(); $customerB=[Guid]::NewGuid()
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid()

function Upload-Video($Token,[byte[]]$Bytes,$Name='evidence.mp4') {
    $client=New-Object System.Net.Http.HttpClient
    try {
        if($Token){$client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$Bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('video/mp4')
        $form.Add($content,'file',$Name)
        $response=$client.PostAsync("$BaseUrl/files/videos",$form).Result
        $body=$response.Content.ReadAsStringAsync().Result
        [pscustomobject]@{status=[int]$response.StatusCode;data=if($body){($body|ConvertFrom-Json).data}else{$null}}
    } finally {$client.Dispose()}
}
function Mp4-Bytes([int]$Size=256) {
    # ISO base media header: box size 0x18, 'ftyp', brand 'mp42'.
    $bytes=New-Object byte[] $Size
    [byte[]](0,0,0,0x18,0x66,0x74,0x79,0x70,0x6D,0x70,0x34,0x32) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
    return ,$bytes
}
function Upload-Image($Token,[byte[]]$Bytes,$Name='evidence.png') {
    $client=New-Object System.Net.Http.HttpClient
    try {
        if($Token){$client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$Bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content,'file',$Name)
        $response=$client.PostAsync("$BaseUrl/files/images",$form).Result
        $body=$response.Content.ReadAsStringAsync().Result
        [pscustomobject]@{status=[int]$response.StatusCode;data=if($body){($body|ConvertFrom-Json).data}else{$null}}
    } finally {$client.Dispose()}
}
function Png-Bytes([int]$Size=64) {
    $bytes=New-Object byte[] $Size
    [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
    return ,$bytes
}
function New-CompletedOrder {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $order=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$locker.availableSlots[0].id;paymentMethod=2
        items=@(@{serviceId="$kgServiceId";quantity=1});customerNote='Feedback smoke'} $customer).data
    $id=$order.id
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=8} $merchant | Out-Null
    $payment=(Call-Api Post '/payment/create-url' @{orderId=$id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$id;transactionReference=$payment.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $id return $shipper | Out-Null
    $done=(Invoke-TestDelivery $id pickup $customer).data
    if($done.status -ne 12 -or !$done.merchantName){throw 'Fixture order did not complete or lost the Merchant name.'}
    return $done
}
function Assert-MerchantRating {
    $expected=Sql "SELECT coalesce(round(avg(overall_stars)::numeric,1),5.0) FROM washgo.order_ratings WHERE merchant_id='$merchantId' AND is_hidden=false AND is_deleted=false;"
    $actual=Sql "SELECT rating FROM washgo.merchants WHERE user_id='$merchantId';"
    if([decimal]$expected -ne [decimal]$actual){throw "Merchant rating $actual != visible average $expected."}
}
function Parallel-Post($Requests) {
    $jobs=@()
    try {
        foreach($request in $Requests){
            $jobs+=Start-Job -ScriptBlock {
                param($Url,$Token,$Body)
                try {
                    $r=Invoke-WebRequest $Url -UseBasicParsing -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $Token"} -Body $Body
                    [int]$r.StatusCode
                } catch {if(!$_.Exception.Response){throw};[int]$_.Exception.Response.StatusCode}
            } -ArgumentList $request.url,$request.token,$request.body
        }
        @($jobs|Wait-Job|Receive-Job)
    } finally {$jobs|Stop-Job -ErrorAction SilentlyContinue;$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id;$seedCustomer=[Guid]$sessions.Customer.user.id;$seedShipper=[Guid]$sessions.Shipper.user.id
    $emailB="feedback-$($customerB.ToString('N'))@example.com";$shipperEmail="feedback-$($fixtureUser.ToString('N'))@example.com"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','$emailB','full_name','Feedback Smoke Customer B','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureUser','email','$shipperEmail','full_name','Feedback Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipper';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipper';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','FEEDBACK_KG_$($kgServiceId.ToString('N'))','name','Feedback Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email=$emailB;password='Customer@123'} $null).data;$tokenB=$sessions.B.accessToken
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$shipperEmail;password='Shipper@123'} $null).data;$shipper=$sessions.Fixture.accessToken
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="FX_$([Guid]::NewGuid().ToString('N'))";name='Feedback Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2},@{boxNumber='3';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Image upload: auth, signature, size, ownership'
    if((Upload-Image $null (Png-Bytes)).status -ne 401){throw 'Anonymous upload allowed.'}
    if((Upload-Image $customer ([Text.Encoding]::ASCII.GetBytes('not an image at all')) 'x.png').status -ne 400){throw 'Non-image accepted.'}
    if((Upload-Image $customer (Png-Bytes (5MB+1))).status -ne 400){throw 'Oversized image accepted.'}
    $img=Upload-Image $customer (Png-Bytes)
    if($img.status -ne 201 -or $img.data.url -notmatch "^/uploads/$($seedCustomer.ToString('N'))/[0-9a-f]{32}\.png$"){throw 'Valid upload failed.'}
    $served=Invoke-WebRequest "$BaseUrl$($img.data.url)" -UseBasicParsing
    if($served.StatusCode -ne 200 -or $served.Headers['Content-Type'] -ne 'image/png'){throw 'Uploaded image not served.'}
    $imgB=(Upload-Image $tokenB (Png-Bytes)).data.url
    $imgUrl=$img.data.url
    if((Upload-Video $null (Mp4-Bytes)).status -ne 401){throw 'Anonymous video upload allowed.'}
    if((Upload-Video $customer (Png-Bytes)).status -ne 400){throw 'Image accepted as a video.'}
    $vid=Upload-Video $customer (Mp4-Bytes (35MB))
    if($vid.status -ne 201 -or $vid.data.url -notmatch "^/uploads/$($seedCustomer.ToString('N'))/[0-9a-f]{32}\.mp4$" -or $vid.data.contentType -ne 'video/mp4'){throw "Video upload failed: $($vid.status)"}
    $servedVideo=Invoke-WebRequest "$BaseUrl$($vid.data.url)" -UseBasicParsing -Method Head
    if($servedVideo.StatusCode -ne 200 -or $servedVideo.Headers['Content-Type'] -ne 'video/mp4'){throw 'Uploaded video not served.'}
    $vidUrl=$vid.data.url
    $vidB=(Upload-Video $tokenB (Mp4-Bytes)).data.url

    Write-Output '2. Prepare orders: two Completed, one Pending'
    $o1=New-CompletedOrder; $o2=New-CompletedOrder
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o3=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data

    Write-Output '3. Rating validation (UC 1.4.1)'
    Assert-Status 401 Post '/rating/' @{orderId=$o1.id;overallStars=5} $null
    Assert-Status 403 Post '/rating/' @{orderId=$o1.id;overallStars=5} $admin
    Assert-Status 400 Post '/rating/' @{orderId=$o3.id;overallStars=5} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=6} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5;qualityStars=0} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5;imageUrls=@($imgB)} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5;imageUrls=@('/uploads/../appsettings.json')} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5;videoUrls=@($vidB)} $customer
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5;videoUrls=@($imgUrl)} $customer
    Assert-Status 404 Post '/rating/' @{orderId=$o1.id;overallStars=5} $tokenB
    $none=(Call-Api Get "/rating/order/$($o1.id)" $null $customer)
    if($null -ne $none.data){throw 'Unrated order returned a rating.'}

    Write-Output '4. Submit rating, duplicate guard and merchant average'
    $rating=(Call-Api Post '/rating/' @{orderId=$o1.id;overallStars=4;qualityStars=5;speedStars=3;attitudeStars=4;packagingStars=5
        comment='  Giat sach, giao hoi cham  ';imageUrls=@($imgUrl);videoUrls=@($vidUrl)} $customer).data
    if(!$rating.merchantName -or $rating.overallStars -ne 4 -or $rating.comment -ne 'Giat sach, giao hoi cham' -or $rating.imageUrls[0] -ne $imgUrl -or $rating.videoUrls[0] -ne $vidUrl){throw 'Rating not stored.'}
    Assert-Status 400 Post '/rating/' @{orderId=$o1.id;overallStars=5} $customer
    if((Call-Api Get "/rating/order/$($o1.id)" $null $customer).data.id -ne $rating.id){throw 'Rating lookup failed.'}
    Assert-MerchantRating
    $race=Parallel-Post @(
        @{url="$BaseUrl/rating/";token=$customer;body=(@{orderId=$o2.id;overallStars=2}|ConvertTo-Json -Compress)},
        @{url="$BaseUrl/rating/";token=$customer;body=(@{orderId=$o2.id;overallStars=3}|ConvertTo-Json -Compress)}
    )
    if(@($race|Where-Object{$_ -eq 201}).Count -ne 1 -or @($race|Where-Object{$_ -eq 400}).Count -ne 1){throw "Double submit race wrong: $race"}
    Assert-MerchantRating

    Write-Output '5. Admin/Merchant rating list and hide (UC 3.7.3)'
    Assert-Status 403 Post '/rating/filter' @{} $customer
    $list=Call-Api Post '/rating/filter' @{keyword=$o1.orderCode;stars=4} $admin
    if($list.total -ne 1 -or $list.list[0].customerName -eq '***'){throw 'Admin rating filter wrong.'}
    $mine=Call-Api Post '/rating/filter' @{keyword=$o1.orderCode;merchantId="$([Guid]::NewGuid())"} $merchant
    if($mine.total -ne 1 -or $mine.list[0].customerName -ne '***'){throw 'Merchant scope/privacy wrong.'}
    Assert-Status 403 Patch "/rating/$($rating.id)/visibility" @{isHidden=$true;reason='x'} $merchant
    Assert-Status 400 Patch "/rating/$($rating.id)/visibility" @{isHidden=$true} $admin
    $hidden=(Call-Api Patch "/rating/$($rating.id)/visibility" @{isHidden=$true;reason='Noi dung khong phu hop'} $admin).data
    if(!$hidden.isHidden){throw 'Hide failed.'}
    if((Call-Api Post '/rating/filter' @{keyword=$o1.orderCode} $merchant).total -ne 0){throw 'Merchant sees hidden rating.'}
    Assert-MerchantRating
    Call-Api Patch "/rating/$($rating.id)/visibility" @{isHidden=$false} $admin | Out-Null
    Assert-MerchantRating

    Write-Output '6. Incident validation (UC 1.4.2)'
    # Video-only evidence is enough (UC 1.4.2 with video).
    $incBody=@{orderId=$o1.id;type=1;severity=2;description='Ao bi rach o tay';videoUrls=@($vidUrl)}
    Assert-Status 403 Post '/incident/' $incBody $admin
    Assert-Status 400 Post '/incident/' @{orderId=$o3.id;type=1;severity=2;description='x';imageUrls=@($imgUrl)} $customer
    Assert-Status 400 Post '/incident/' @{orderId=$o1.id;type=1;severity=2;description='x'} $customer
    Assert-Status 400 Post '/incident/' @{orderId=$o1.id;type=9;severity=2;description='x';imageUrls=@($imgUrl)} $customer
    Assert-Status 400 Post '/incident/' @{orderId=$o1.id;type=1;severity=2;description='  ';imageUrls=@($imgUrl)} $customer
    Assert-Status 404 Post '/incident/' @{orderId=$o1.id;type=1;severity=2;description='x';imageUrls=@($imgB)} $tokenB
    Sql "UPDATE washgo.order_status_history SET created_at=created_at-4*86400 WHERE order_id='$($o2.id)' AND to_status=12;" | Out-Null
    Assert-Status 400 Post '/incident/' @{orderId=$o2.id;type=2;severity=3;description='Mat 1 ao';imageUrls=@($imgUrl)} $customer

    Write-Output '7. Submit incident and customer views (UC 1.4.3)'
    $inc=(Call-Api Post '/incident/' $incBody $customer).data
    if(!$inc.merchantName -or $inc.status -ne 1 -or $inc.orderCode -ne $o1.orderCode -or $inc.imageUrls.Count -ne 0 -or $inc.videoUrls[0] -ne $vidUrl){throw 'Incident not stored as Pending.'}
    Assert-Status 400 Post '/incident/' $incBody $customer
    $mineInc=Call-Api Get '/incident/mine?page=1&take=50' $null $customer
    if(!($mineInc.list|Where-Object{$_.id -eq $inc.id})){throw 'Customer list missing incident.'}
    $emptyB=Call-Api Get '/incident/mine' $null $tokenB
    if($emptyB.total -ne 0 -or !$emptyB.message){throw 'Empty incident list message missing.'}
    Assert-Status 404 Get "/incident/$($inc.id)" $null $tokenB
    Assert-Status 403 Post '/incident/filter' @{} $customer

    Write-Output '8. Admin resolves with compensation (UC 3.7.1, 3.7.2)'
    $pending=Call-Api Post '/incident/filter' @{status=1;keyword=$o1.orderCode} $admin
    if($pending.total -ne 1){throw 'Admin incident filter wrong.'}
    Assert-Status 403 Post "/incident/$($inc.id)/resolve" @{resolutionNote='x';compensationLevel=2;itemValue=1} $customer
    Assert-Status 400 Post "/incident/$($inc.id)/resolve" @{resolutionNote='Loi tiem';compensationLevel=3} $admin
    Assert-Status 400 Post "/incident/$($inc.id)/resolve" @{compensationLevel=0} $admin
    $resolved=(Call-Api Post "/incident/$($inc.id)/resolve" @{resolutionNote='Loi do tiem giat';responsibleParty=1;compensationLevel=2;itemValue=200000;compensationAmount=1} $admin).data
    if($resolved.status -ne 2 -or $resolved.compensationAmount -ne 140000 -or $resolved.compensationStatus -ne 1){throw 'Compensation not computed by server.'}
    Assert-Status 400 Post "/incident/$($inc.id)/resolve" @{resolutionNote='Again';compensationLevel=4;itemValue=200000} $admin
    Assert-Status 400 Post "/incident/$($inc.id)/reject" @{reason='Late'} $admin
    Assert-Status 400 Post "/incident/$($inc.id)/confirm-compensation" @{reference=' '} $admin
    $paid=(Call-Api Post "/incident/$($inc.id)/confirm-compensation" @{reference='BANK-123'} $admin).data
    if($paid.compensationStatus -ne 2 -or $paid.compensationReference -ne 'BANK-123'){throw 'Compensation payment not recorded.'}
    $replay=(Call-Api Post "/incident/$($inc.id)/confirm-compensation" @{reference='BANK-999'} $admin).data
    if($replay.compensationReference -ne 'BANK-123'){throw 'Compensation replay overwrote reference.'}
    if((Call-Api Get "/incident/$($inc.id)" $null $customer).data.compensationAmount -ne 140000){throw 'Customer cannot see result.'}

    Write-Output '9. Reject a second incident; lost-goods compensation is 200%'
    $inc2=(Call-Api Post '/incident/' @{orderId=$o1.id;type=3;severity=1;description='Thieu moc ao';imageUrls=@($imgUrl)} $customer).data
    Assert-Status 400 Post "/incident/$($inc2.id)/reject" @{reason=''} $admin
    $rejected=(Call-Api Post "/incident/$($inc2.id)/reject" @{reason='Khong du bang chung'} $admin).data
    if($rejected.status -ne 3 -or $rejected.compensationAmount -ne 0){throw 'Reject wrong.'}
    Assert-Status 400 Post "/incident/$($inc2.id)/confirm-compensation" @{reference='X'} $admin
    $inc3=(Call-Api Post '/incident/' @{orderId=$o1.id;type=2;severity=3;description='Mat ao khoac';imageUrls=@($imgUrl)} $customer).data
    $lost=(Call-Api Post "/incident/$($inc3.id)/resolve" @{resolutionNote='Mat do tai tiem';responsibleParty=1;compensationLevel=4;itemValue=150000} $admin).data
    if($lost.compensationAmount -ne 300000){throw 'Lost-goods compensation not 200%.'}

    Write-Output 'PASS: image upload checks, video upload (35MB over the default body limit, signature, ownership) on ratings and video-only incident evidence, rating validation/ownership/once-per-order/race, merchant average, admin hide, merchant privacy, incident window/images/ownership, customer list, admin resolve/reject, server-side compensation and payment record.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id IN('$customerB','$fixtureUser'); UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$kgServiceId';"|Out-Null
}
