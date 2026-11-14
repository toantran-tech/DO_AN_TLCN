param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
trap { Write-Warning $_.Exception.Message; Write-Warning $_.ScriptStackTrace; break }
# Disposable dev/CI records; no production payment or external SMTP.
$sessions=@{};$orderIds=@();$lockerId=$null;$run=[Guid]::NewGuid().ToString('N');$notes=@(1..4|ForEach-Object{[Guid]::NewGuid()})
function Review($Id) { (Call-Api Get "/rating/$Id" $null $admin).data }
function Publish($Id,$Public) {
    $body=@{expectedVersion=(Review $Id).version;operationId=[Guid]::NewGuid().ToString();publishForCustomers=$Public}
    $first=(Call-Api Post "/rating/$Id/publish" $body $admin).data
    $again=(Call-Api Post "/rating/$Id/publish" $body $admin).data
    if($first.version -ne $again.version){throw 'Publish replay changed version.'}
}
function Visibility($Id,$Hidden) {
    Call-Api Patch "/rating/$Id/visibility" @{expectedVersion=(Review $Id).version;operationId=[Guid]::NewGuid().ToString();isHidden=$Hidden;reason='Fixture moderation'} $admin | Out-Null
}
try {
    foreach($role in @('Admin','Customer','Merchant')) {
        for($attempt=0;$attempt -lt 2;$attempt++) {
            try{$sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data;break}
            catch{if($attempt -gt 0 -or [int]$_.Exception.Response.StatusCode -ne 429){throw};Start-Sleep -Seconds 60}
        }
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$owner=$sessions.Customer.user.id
    $baseline=(Call-Api Get '/customer/reviews/summary' $null $customer).data
    $locker=(Call-Api Post '/locker/' @{merchantId=$sessions.Merchant.user.id;code="PUB_$run";name='Public review fixture';address='Fixture';district='Test';city='Test';latitude=10.7;longitude=106.7;boxes=@(@{boxNumber='1';size=2})} $admin).data
    $lockerId=[Guid]$locker.id
    $slot=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0]
    $service=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0]
    $item=@{serviceId=$service.id;quantity=1}
    if($service.unit -eq 'kg'){$item.weightKg=1}
    $created=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot.id;items=@($item);paymentMethod=2} $customer).data
    $orderIds+=([Guid]$created.id);$ratingIds=@(1..3|ForEach-Object{[Guid]::NewGuid()})
    foreach($n in 0..2) {
        $orderId=if($n -eq 0){$orderIds[0]}else{[Guid]::NewGuid()}
        if($n -gt 0){
            $orderIds+=$orderId
            Sql "INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object('id','$orderId','order_code','PU$($run.Substring(0,14))$n','merchant_order_code',NULL,'receipt_number',NULL))).* FROM washgo.orders o WHERE id='$($created.id)';" | Out-Null
        }
        $stars=5-$n
        Sql @"
UPDATE washgo.orders SET status=12 WHERE id='$orderId';
INSERT INTO washgo.order_ratings (id,order_id,order_code,customer_id,customer_name,overall_stars,comment,image_urls,video_urls,is_hidden,is_deleted,created_on,modified_on)
VALUES ('$($ratingIds[$n])','$orderId','PU$($run.Substring(0,14))$n','$owner','Private fixture name',$stars,'Approved testimonial $run $n',ARRAY[]::text[],ARRAY[]::text[],false,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
"@ | Out-Null
    }
    $boundary='WashGo'+[Guid]::NewGuid().ToString('N')
    $image=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jG1kAAAAASUVORK5CYII=')
    $prefix=[Text.Encoding]::UTF8.GetBytes("--$boundary`r`nContent-Disposition: form-data; name=`"file`"; filename=`"fixture.png`"`r`nContent-Type: image/png`r`n`r`n")
    $suffix=[Text.Encoding]::UTF8.GetBytes("`r`n--$boundary--`r`n")
    $uploaded=(Invoke-RestMethod -Uri "$BaseUrl/files/images" -Method Post -Headers @{Authorization="Bearer $customer"} -ContentType "multipart/form-data; boundary=$boundary" -Body ([byte[]]($prefix+$image+$suffix))).data.url
    if(!$uploaded){throw 'Image upload fixture missing.'}
    Sql "UPDATE washgo.order_ratings SET image_urls=ARRAY['$uploaded'] WHERE id='$($ratingIds[0])';" | Out-Null
    Publish $ratingIds[0] $false
    if((Call-Api Get '/customer/reviews/summary' $null $customer).data.total -ne $baseline.total){throw 'Merchant-only approval leaked to Customer.'}
    Publish $ratingIds[0] $true
    Publish $ratingIds[1] $true
    $page=(Call-Api Get '/customer/reviews?page=1&take=1' $null $customer).data
    if($page.total -ne ($baseline.total+2) -or @($page.items).Count -ne 1 -or !$page.hasMore){throw 'Public paging/count mismatch.'}
    $all=(Call-Api Get '/customer/reviews?page=1&take=20' $null $customer).data.items
    $mine=@($all|Where-Object{$_.comment -like "*$run*"})
    if($mine.Count -ne 2){throw 'Unapproved review released or approved review missing.'}
    foreach($item in $mine){
        if($item.id -in $ratingIds -or $item.id -in $orderIds){throw 'Public ID reuses an internal identity.'}
        if(@($item.PSObject.Properties.Name|Where-Object{$_ -notin @('id','overallStars','comment','imageUrls','publishedAt')}).Count){throw 'Public feedback leaked internal fields.'}
    }
    $five=(Call-Api Get '/customer/reviews?page=1&take=20&stars=5' $null $customer).data
    if(@($five.items|Where-Object{$_.overallStars -ne 5}).Count -or !@($five.items|Where-Object{$_.comment -like "*$run*"}).Count){throw 'Valid stars filter ignored.'}
    $summary=(Call-Api Get '/customer/reviews/summary' $null $customer).data
    if($summary.total -ne ($baseline.total+2) -or @($summary.distribution).Count -ne 5){throw 'Summary not based on the whole released set.'}
    if($baseline.total -eq 0 -and $summary.averageStars -ne 4.5){throw 'Average calculation wrong.'}
    $withImage=@($mine|Where-Object{$_.overallStars -eq 5})[0]
    if(@($withImage.imageUrls).Count -ne 1 -or $withImage.imageUrls[0] -match ($owner -replace '-','') -or $withImage.imageUrls[0] -like '/uploads/*'){throw 'Public media exposes owner/path.'}
    $publicImage=$withImage.imageUrls[0]
    $media=Invoke-WebRequest -Uri "$BaseUrl$publicImage" -Headers @{Authorization="Bearer $customer"} -UseBasicParsing
    if($media.StatusCode -ne 200 -or $media.Headers['Cache-Control'] -ne 'no-store'){throw 'Anonymous media response missing/no-store absent.'}
    Visibility $ratingIds[0] $true
    if((Call-Api Get '/customer/reviews/summary' $null $customer).data.total -ne ($baseline.total+1)){throw 'Hide did not update summary.'}
    Assert-Status 404 Get $publicImage $null $customer
    Visibility $ratingIds[0] $false
    if((Call-Api Get '/customer/reviews/summary' $null $customer).data.total -ne ($baseline.total+2)){throw 'Show did not restore publication.'}
    foreach($path in @('/customer/reviews?stars=6','/customer/reviews?take=21','/customer/reviews?page=0','/customer/reviews?serviceId=invalid')){Assert-Status 400 Get $path $null $customer}
    Assert-Status 401 Get '/customer/reviews' $null $null
    Assert-Status 403 Get '/customer/reviews' $null $admin
    Assert-Status 404 Get "/customer/reviews/$($mine[0].id)/images/0" $null $customer
    $safe="visible$run";$secret="internal$run"
    Sql @"
INSERT INTO washgo.notifications (id,user_id,type,title,content,link_type,message_key,is_read,is_deleted,created_on,modified_on) VALUES
('$($notes[0])','$owner',1,'Fixture','$safe','order',NULL,false,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint),
('$($notes[1])','$owner',11,'$secret','$secret warehouse address','order',NULL,false,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint),
('$($notes[2])','$owner',6,'$secret','$secret warehouse address','order','paymentSucceeded.storage.customer',true,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint),
('$($notes[3])','$($sessions.Admin.user.id)',1,'Fixture','$safe','order',NULL,false,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
"@ | Out-Null
    $found=(Call-Api Post '/notification/filter' @{keyword=$safe;take=1;page=1} $customer).data
    if($found.total -ne 1 -or $found.items[0].id -ne "$($notes[0])"){throw 'Safe order-content search or owner scope wrong.'}
    if((Call-Api Post '/notification/filter' @{keyword=$secret} $customer).data.total -ne 0){throw 'Search/count leaked a protected legacy title/content.'}
    # Exact text returned to the client must also be searchable in SQL, before paging/count.
    $inbox=(Call-Api Get '/notification/?page=1&take=100' $null $customer)
    $safeRow=@($inbox.list|Where-Object{$_.id -eq "$($notes[1])"})[0]
    if(!$safeRow -or $safeRow.content -match $secret){throw 'Legacy content projection unsafe.'}
    $byVisible=(Call-Api Post '/notification/filter' @{keyword=$safeRow.content.Substring(0,[Math]::Min(80,$safeRow.content.Length));types=@(11);take=1} $customer).data
    if($byVisible.total -lt 1 -or !$byVisible.items[0].content.Contains($safeRow.content)){throw 'Query and visible content use different privacy rules.'}
    if((Call-Api Post '/notification/filter' @{keyword=$safe;types=@(11)} $customer).data.total -ne 0){throw 'Type filter lost after keyword projection.'}
    if((Call-Api Post '/notification/filter' @{keyword=$safe;unreadOnly=$true;fromDate=0;toDate=1} $customer).data.total -ne 0){throw 'Date/unread guards lost after safe search.'}
    Write-Output 'PASS: explicit Customer publication, anonymous IDs/DTO, paging/summary/Hide/Show/validation/auth and SQL safe-notification search/count/owner filters.'
}
finally {
    $ids=($notes|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.notifications WHERE id IN ($ids);" | Out-Null
    if($orderIds.Count){$ids=($orderIds|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.order_ratings WHERE order_id IN ($ids); DELETE FROM washgo.orders WHERE id IN ($ids);"|Out-Null}
    if($lockerId){Sql "UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId'; UPDATE washgo.lockers SET available_slots=1,status=2,is_deleted=true WHERE id='$lockerId';"|Out-Null}
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}}
}
