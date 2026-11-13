param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV/CI ONLY: dedicated accounts, orders, media metadata and ratings; never changes seed ratings.
$sessions=@{}; $users=@{}; $orders=@(); $jobs=@(); $run=[Guid]::NewGuid().ToString('N'); $orderPrefix='RP'+$run.Substring(0,16)
function New-FixtureSession($Email) {
    # Other suites share the IP's 10/minute auth budget. Keep production rate limits enabled.
    for($attempt=0;$attempt -lt 2;$attempt++) {
        try { return (Call-Api Post '/auth/login' @{email=$Email;password='Admin@123'} $null).data }
        catch {
            if($attempt -gt 0 -or [int]$_.Exception.Response.StatusCode -ne 429) { throw }
            Write-Host 'Fixture login reached the shared auth rate limit; waiting for the next window.'
            Start-Sleep -Seconds 60
        }
    }
}
function View-Rating($Id) { (Call-Api Get "/rating/$Id" $null $sessions.Admin.accessToken).data }
function Merchant-Matches($Keyword) { Call-Api Post '/rating/partner/filter' @{keyword=$Keyword;take=100} $sessions.Merchant.accessToken }
function Assert-NoIdentity($Dto) {
    foreach($field in @('customerId','customerName','customerEmail','customerPhoneNumber','merchantId','merchantName','merchantEmail','orderId','orderCode','contentApprovedBy')) {
        if($Dto.PSObject.Properties.Name -contains $field) { throw "Partner/Customer rating exposes $field" }
    }
}
try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')) {
        $id=[Guid]::NewGuid(); $users[$role]=$id; $email="review-$role-$run@example.com"
        Sql @"
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
 'id','$id','role_id',r.id,'email','$email','full_name','Review fixture $role',
 'phone','0901234567','avatar','https://example.com/$run-$role.png','auth_provider',1,'provider_id',NULL,'is_active',true))).*
FROM washgo.users u CROSS JOIN washgo.roles r WHERE u.email='admin@washgo.vn' AND r.name='$role';
"@ | Out-Null
        if($role -eq 'Merchant') {
            Sql @"
INSERT INTO washgo.merchants
SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
 'user_id','$id','business_name','Review fixture store','tax_code',NULL,'status',1,
 'public_rating_sum',8,'public_rating_count',2,'rating',4,'phone','0907654321'))).*
FROM washgo.merchants m LIMIT 1;
INSERT INTO washgo.merchant_images (id,merchant_id,image_type,public_id,secure_url,is_primary,display_order,created_at)
VALUES (gen_random_uuid(),'$id','store','$run','https://example.com/$run-store.png',true,0,now());
"@ | Out-Null
        }
        if($role -eq 'Customer') {
            Sql "INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$id'))).* FROM washgo.customers c LIMIT 1;" | Out-Null
        }
        if($role -eq 'Shipper') {
            Sql "INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object('id','$id','user_id','$id','merchant_id','$($users.Merchant)','create_request_id',NULL,'is_active',true,'is_deleted',false,'status',1))).* FROM washgo.shippers s LIMIT 1;" | Out-Null
        }
        $sessions[$role]=New-FixtureSession $email
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken; $customer=$sessions.Customer.accessToken
    $ratingIds=@()
    foreach($n in 1..3) {
        $orderId=[Guid]::NewGuid(); $orders+=$orderId; $ratingId=[Guid]::NewGuid(); $ratingIds+=$ratingId
        $hidden=if($n -eq 3){'true'}else{'false'}
        Sql @"
INSERT INTO washgo.orders
SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
 'id','$orderId','order_code','$orderPrefix$n','merchant_order_code',NULL,'receipt_number',NULL,
 'customer_id','$($users.Customer)','merchant_id','$($users.Merchant)','shipper_id','$($users.Shipper)','status',12,'is_deleted',false,
 'customer_name','customer-$run@example.com','customer_phone_number','0901234567',
 'merchant_name','staff-$run@example.com','shipper_name','staff-$run@example.com',
 'note','customer-$run@example.com','shipper_note','staff-$run@example.com',
 'storage_location','staff-$run@example.com','storage_reference','staff-$run@example.com'))).*
FROM washgo.orders o LIMIT 1;
INSERT INTO washgo.order_status_history (id,order_id,to_status,changed_by,actor_name,changed_by_type,created_at,note)
VALUES (gen_random_uuid(),'$orderId',12,'$($users.Merchant)','staff-$run@example.com','Merchant',extract(epoch from now())::bigint,'customer-$run@example.com');
INSERT INTO washgo.order_ratings
 (id,order_id,order_code,customer_id,customer_name,merchant_id,merchant_name,overall_stars,comment,image_urls,video_urls,is_hidden,is_deleted,created_on,modified_on)
VALUES ('$ratingId','$orderId','$orderPrefix$n','$($users.Customer)','Snapshot customer','$($users.Merchant)','Snapshot store',4,
 'private$run$n',ARRAY['https://example.com/$run-rating.png'],ARRAY[]::text[],$hidden,false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
"@ | Out-Null
    }
    # Final HTTP projections must run after enrich, not just in the mapper.
    $orderView=(Call-Api Get "/wash-order/$($orders[0])" $null $customer).data
    $orderList=Call-Api Post '/wash-order/mine' @{take=100;keyword=$orderPrefix} $customer
    if(($orderView|ConvertTo-Json -Depth 15) -match "staff-$run" -or
        ($orderList|ConvertTo-Json -Depth 15) -match "staff-$run" -or $orderView.merchantId -or
        $orderView.merchantImageUrls.Count -or $orderView.merchantPhoneNumber -or $orderView.shipperId -or
        !$orderView.depositPinCode -or $orderList.total -ne 3) { throw 'Customer order detail/list leaked staff identity after enrichment or lost own locker code.' }
    $staffOrder=(Call-Api Get "/wash-order/$($orders[0])" $null $merchant).data
    $staffList=Call-Api Post '/wash-order/filter' @{take=100;keyword=$orderPrefix} $merchant
    if(($staffOrder|ConvertTo-Json -Depth 15) -match "customer-$run" -or
        ($staffList|ConvertTo-Json -Depth 15) -match "customer-$run" -or $staffOrder.customerAvatarUrl) { throw 'Merchant order projection leaked Customer identity.' }
    $shipperOrder=(Call-Api Get "/wash-order/$($orders[0])" $null $sessions.Shipper.accessToken).data
    if(($shipperOrder|ConvertTo-Json -Depth 15) -match "customer-$run" -or $shipperOrder.customerAvatarUrl -or $shipperOrder.pickupPinCode) { throw 'Shipper order detail leaked Customer data.' }
    $filtered=Call-Api Post '/wash-order/mine' @{page=1;take=100;keyword=$orderPrefix;merchantId=[Guid]::NewGuid().ToString();shipperId=[Guid]::NewGuid().ToString();columnFilters=@(@{field='MerchantName';selectedValues=@('not the store')});sortBy=@(@{field='ShipperName';isDescending=$false})} $customer
    if($filtered.total -ne 3) { throw 'Customer inferred withheld staff data via filters/count.' }
    $adminOrder=(Call-Api Get "/wash-order/$($orders[0])" $null $admin).data
    if($adminOrder.customerName -ne "customer-$run@example.com" -or !$adminOrder.merchantId) { throw 'Admin lost internal order data.' }
    $foreignOrder=Sql "SELECT id FROM washgo.orders WHERE customer_id<>'$($users.Customer)' AND merchant_id IS DISTINCT FROM '$($users.Merchant)'::uuid AND shipper_id IS DISTINCT FROM '$($users.Shipper)'::uuid LIMIT 1;"
    if(!$foreignOrder){throw 'Cross-owner privacy fixture missing.'}
    foreach($token in @($customer,$merchant,$sessions.Shipper.accessToken)) {
        Assert-Status 404 Get "/wash-order/$foreignOrder" $null $token
    }
    # Guarded conflict includes a DTO and must use the same boundary.
    try { Call-Api Put "/wash-order/$($orders[0])/actual-weight" @{weightKg=2;expectedRevision=-1} $merchant|Out-Null; throw 'Expected version conflict.' }
    catch {
        if([int]$_.Exception.Response.StatusCode -ne 409) { throw }
        $errorBody=$_.ErrorDetails.Message
        if(!$errorBody) {
            $stream=$_.Exception.Response.GetResponseStream()
            if($stream.CanSeek){$stream.Position=0}
            $reader=New-Object System.IO.StreamReader($stream)
            try{$errorBody=$reader.ReadToEnd()}finally{$reader.Dispose()}
        }
        $conflict=$errorBody | ConvertFrom-Json
        if(!$conflict.data -or ($conflict.data|ConvertTo-Json -Depth 15) -match "customer-$run") { throw 'Version conflict leaked Customer identity.' }
    }
    Sql @"
INSERT INTO washgo.payment_transactions (id,order_id,transaction_reference,status,amount,payment_method,purpose,completed_at,is_deleted,created_on,modified_on)
VALUES (gen_random_uuid(),'$($orders[0])','REVIEW-$run',2,100000,2,1,now(),false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
INSERT INTO washgo.notifications (id,user_id,type,title,content,link_type,link_id,is_read,message_key,message_args,is_deleted,created_on,modified_on)
VALUES (gen_random_uuid(),'$($users.Customer)',11,'Storage notice','staff-$run@example.com','order','$($orders[0])',false,
 'unpaidMovedToStorage.customer','{"storageLocation":"staff-$run@example.com","storageReference":"staff-$run@example.com"}',false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
"@ | Out-Null
    Sql @"
INSERT INTO washgo.notifications (id,user_id,type,title,content,link_type,link_id,is_read,message_key,message_args,is_deleted,created_on,modified_on)
VALUES (gen_random_uuid(),'$($users.Merchant)',10,'Cancelled order','Order cancelled','order','$($orders[0])',false,
 'cancelled.merchant','{"reason":"customer-$run@example.com","customerName":"customer-$run@example.com"}',false,extract(epoch from now())::bigint,extract(epoch from now())::bigint);
"@ | Out-Null
    $partnerInbox=Call-Api Get '/notification/?page=1&take=100' $null $merchant
    if(($partnerInbox|ConvertTo-Json -Depth 15) -match "customer-$run") { throw 'Partner inbox leaked Customer free text from legacy arguments.' }
    $receipt=(Call-Api Get "/wash-order/$($orders[0])/receipt" $null $customer).data
    if($receipt.seller.name -ne 'WashGo' -or $receipt.merchantOrderCode -or $receipt.seller.phone -or $receipt.seller.address) { throw 'Customer receipt leaked store identity.' }
    $staffReceipt=(Call-Api Get "/wash-order/$($orders[0])/receipt" $null $merchant).data
    if($staffReceipt.buyer.phone -or $staffReceipt.buyer.email -or $staffReceipt.buyer.name -eq "customer-$run@example.com") { throw 'Merchant receipt leaked Customer contacts.' }
    $inbox=Call-Api Get '/notification/?page=1&take=100' $null $customer
    if(($inbox|ConvertTo-Json -Depth 15) -match "staff-$run") { throw 'Legacy inbox leaked storage address.' }
    $id=$ratingIds[0]; $key="private${run}1"; $view=View-Rating $id
    if(!$view.canPublish -or $view.contentReleased -or $view.customerEmail -ne "review-Customer-$run@example.com" -or
        $view.customerName -ne 'Snapshot customer' -or $view.merchantName -ne 'Snapshot store' -or
        $view.customerPhoneNumber -ne '0901234567' -or $view.merchantPhone -ne '0907654321' -or
        @($view.merchantImageUrls).Count -ne 1 -or !$view.contactRetrievedAt) { throw 'Admin contacts/capability missing or historical names overwritten.' }
    $list=Call-Api Post '/rating/filter' @{merchantId="$($users.Merchant)";take=100} $admin
    if($list.total -ne 3 -or @($list.list | Where-Object {!$_.customerEmail}).Count) { throw 'Admin list did not enrich contacts.' }
    if((Merchant-Matches $key).total -ne 0) { throw 'Unpublished comment leaks through keyword.' }
    $own=(Call-Api Get "/rating/order/$($orders[0])" $null $customer).data
    # Customer retains its own order identity; partner/contact and moderation data must be absent.
    if($own.PSObject.Properties.Name -contains 'merchantId' -or $own.PSObject.Properties.Name -contains 'customerEmail') { throw 'Customer rating exposes internal contacts.' }
    foreach($token in @($merchant,$customer,$sessions.Shipper.accessToken)) {
        Assert-Status 403 Post "/rating/$id/publish" @{expectedVersion=$view.version} $token
        Assert-Status 403 Get "/rating/$id" $null $token
    }
    Assert-Status 401 Post "/rating/$id/publish" @{expectedVersion=$view.version} $null
    Assert-Status 400 Post "/rating/$id/publish" @{} $admin
    Assert-Status 409 Post "/rating/$id/publish" @{expectedVersion='stale'} $admin
    $beforeAggregate=Sql "SELECT public_rating_sum||':'||public_rating_count FROM washgo.merchants WHERE user_id='$($users.Merchant)';"
    $op=[Guid]::NewGuid().ToString(); $body=@{expectedVersion=$view.version;operationId=$op;reason='Reviewed original text and media'}
    $published=(Call-Api Post "/rating/$id/publish" $body $admin).data
    if(!$published.contentReleased -or $published.canPublish -or $published.contentApprovedBy -ne "$($users.Admin)" -or
        $published.moderatedAt -or $published.version -eq $view.version) { throw 'Publish incorrectly changed visibility metadata or version.' }
    $replay=(Call-Api Post "/rating/$id/publish" $body $admin).data
    if($replay.contentApprovedAt -ne $published.contentApprovedAt -or $replay.version -ne $published.version) { throw 'Replay changed approval.' }
    Assert-Status 409 Post "/rating/$id/publish" @{expectedVersion=$view.version;operationId=$op;reason='different'} $admin
    $lookup=(Call-Api Get "/admin-operations/Rating/$op" $null $admin).data
    if(!$lookup) { throw 'Publish operation lookup missing.' }
    $matched=Merchant-Matches $key
    if($matched.total -ne 1 -or !$matched.list[0].contentReleased -or @($matched.list[0].imageUrls).Count -ne 1) { throw 'Publish did not release text/media.' }
    Assert-NoIdentity $matched.list[0]
    if((Sql "SELECT public_rating_sum||':'||public_rating_count FROM washgo.merchants WHERE user_id='$($users.Merchant)';") -ne $beforeAggregate) { throw 'Publish/replay changed stars.' }
    $hidden=(Call-Api Patch "/rating/$id/visibility" @{isHidden=$true;reason='test hide';expectedVersion=$published.version} $admin).data
    Assert-Status 400 Post "/rating/$id/publish" @{expectedVersion=$hidden.version} $admin
    if((Merchant-Matches $key).total -ne 0) { throw 'Hidden approved content searchable.' }
    $shown=(Call-Api Patch "/rating/$id/visibility" @{isHidden=$false;expectedVersion=$hidden.version} $admin).data
    if(!$shown.contentReleased -or $shown.contentApprovedAt -ne $published.contentApprovedAt) { throw 'Show lost an existing approval.' }
    # Showing a never-approved rating must not approve it.
    $unapproved=View-Rating $ratingIds[2]
    $shownPending=(Call-Api Patch "/rating/$($ratingIds[2])/visibility" @{isHidden=$false;expectedVersion=$unapproved.version} $admin).data
    if($shownPending.contentReleased -or !$shownPending.canPublish -or (Merchant-Matches "private${run}3").total -ne 0) { throw 'Show silently approved content.' }
    $batchBody=@{operationId=[Guid]::NewGuid().ToString();action='Publish';items=@(
        @{id="$($ratingIds[2])";expectedVersion=$shownPending.version},@{id="$id";expectedVersion=$shown.version})}
    $batch=(Call-Api Post '/rating/batch' $batchBody $admin).data
    if($batch.totals.changed -ne 1 -or $batch.totals.unchanged -ne 1) { throw 'Batch Publish outcomes incorrect.' }
    $batchReplay=(Call-Api Post '/rating/batch' $batchBody $admin).data
    if(($batchReplay | ConvertTo-Json -Depth 12 -Compress) -ne ($batch | ConvertTo-Json -Depth 12 -Compress)) { throw 'Batch replay changed result.' }
    # Two different commands with the same version must serialize; only one may win.
    $raceId=$ratingIds[1]; $race=View-Rating $raceId
    foreach($action in @('publish','visibility')) {
        $jobs+=Start-Job -ScriptBlock {
            param($Url,$Token,$Id,$Version,$Action)
            $body=@{expectedVersion=$Version;operationId=[Guid]::NewGuid().ToString()}
            $method='Post'; if($Action -eq 'visibility'){$method='Patch';$body.isHidden=$true;$body.reason='race hide'}
            try{Invoke-RestMethod -Uri "$Url/rating/$Id/$Action" -Method $method -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body ($body|ConvertTo-Json)|Out-Null;200}
            catch{[int]$_.Exception.Response.StatusCode}
        } -ArgumentList $BaseUrl,$admin,"$raceId",$race.version,$action
    }
    $codes=@($jobs|Receive-Job -Wait -AutoRemoveJob); $jobs=@()
    if((($codes|Sort-Object)-join ',') -ne '200,409') { throw "Publish/Hide race returned $codes" }
    $aggregate=Sql "SELECT public_rating_sum||':'||public_rating_count FROM washgo.merchants WHERE user_id='$($users.Merchant)';"
    $actual=Sql "SELECT COALESCE(sum(overall_stars),0)||':'||count(*) FROM washgo.order_ratings WHERE merchant_id='$($users.Merchant)' AND NOT is_deleted AND NOT is_hidden;"
    if($aggregate -ne $actual) { throw 'Concurrent moderation corrupted Merchant rating aggregates.' }
    # Missing current profiles are represented explicitly, without contacts copied from another account.
    $missing=[Guid]::NewGuid()
    Sql "UPDATE washgo.order_ratings SET customer_id='$missing',merchant_id=NULL WHERE id='$raceId';" | Out-Null
    $absent=View-Rating $raceId
    if($absent.customerEmail -or $absent.customerAvatarUrl -or $absent.customerPhoneNumber -or $absent.merchantEmail -or @($absent.merchantImageUrls).Count) { throw 'Missing profiles produced invented contacts.' }
    $oversize=@{operationId=[Guid]::NewGuid().ToString();action='Publish';items=@(1..51 | ForEach-Object { @{id=[Guid]::NewGuid().ToString();expectedVersion='unused'} })}
    Assert-Status 400 Post '/rating/batch' $oversize $admin
    $audit=Sql "SELECT count(*) FROM washgo.audit_logs WHERE action='Publish' AND resource_id='$id';"
    if($audit -ne '1') { throw 'Publish replay duplicated audit.' }
    Write-Output 'PASS: Admin-only current contacts, snapshots, Publish/Hide/Show separation, private search, version/replay/lookup, batch, concurrent Hide/Publish, missing profiles and unchanged publication aggregates.'
}
finally {
    foreach($job in $jobs){Stop-Job $job -ErrorAction SilentlyContinue;Remove-Job $job -Force -ErrorAction SilentlyContinue}
    if($orders.Count){$ids=($orders|ForEach-Object{"'$_'"})-join ',';Sql "DELETE FROM washgo.order_ratings WHERE order_id IN ($ids); DELETE FROM washgo.order_status_history WHERE order_id IN ($ids); DELETE FROM washgo.orders WHERE id IN ($ids);"|Out-Null}
    if($users.Count){
        $ids=($users.Values|ForEach-Object{"'$_'"})-join ','
        Sql "DELETE FROM washgo.administrative_operations WHERE actor_id IN ($ids); DELETE FROM washgo.audit_logs WHERE actor_id IN ($ids); DELETE FROM washgo.notifications WHERE user_id IN ($ids); DELETE FROM washgo.refresh_tokens WHERE user_id IN ($ids); DELETE FROM washgo.merchant_images WHERE merchant_id IN ($ids); DELETE FROM washgo.shippers WHERE user_id IN ($ids); DELETE FROM washgo.customers WHERE user_id IN ($ids); DELETE FROM washgo.merchants WHERE user_id IN ($ids); DELETE FROM washgo.users WHERE id IN ($ids);"|Out-Null
    }
}
