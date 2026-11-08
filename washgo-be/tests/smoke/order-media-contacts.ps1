param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: order list/detail media and contacts per role, Admin access codes behind a separate audited endpoint.
$sessions=@{}; $lockers=@(); $restore=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$NoAutoAssign=$false

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $merchantId="$($sessions.Merchant.user.id)"; $customerId="$($sessions.Customer.user.id)"
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $lImg=Upload-TestImage $admin; $rImg=Upload-TestImage $admin
    $l=(Call-Api Post '/locker/' @{merchantId=$merchantId;name="Media L $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=1;imageUrls=@($lImg)} $admin).data
    $r=(Call-Api Post '/locker/' @{merchantId=$merchantId;name="Media R $run";address='Private development fixture';district='Test';city='Test';latitude=10.78;longitude=106.70;boxCount=1;imageUrls=@($rImg)} $admin).data
    $lockers+=$l.id,$r.id
    # Temporary media on shared seed rows, restored in finally.
    $oldService=Sql "SELECT COALESCE(images::text,'') FROM washgo.services WHERE id='$standardWash';"
    $oldAvatar=Sql "SELECT COALESCE(avatar::text,'') FROM washgo.users WHERE id='$customerId';"
    $mImgId=[Guid]::NewGuid()
    Sql @"
UPDATE washgo.services SET images='["https://example.com/svc-$run.png"]' WHERE id='$standardWash';
UPDATE washgo.users SET avatar=to_jsonb('https://example.com/cus-$run.png'::text) WHERE id='$customerId';
INSERT INTO washgo.merchant_images(id,merchant_id,image_type,public_id,secure_url,is_primary,display_order,created_at)
VALUES('$mImgId','$merchantId','store','smoke-$run','https://example.com/shop-$run.png',true,0,now());
"@ | Out-Null
    $restore=$true
    $slot=(Call-Api Get "/customer/lockers/$($l.id)" $null $customer).data.availableSlots[0].id
    $o=(Call-Api Post '/wash-order/' @{lockerId="$($l.id)";boxId=$slot;returnLockerId="$($r.id)";paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    $phone=Sql "SELECT phone FROM washgo.merchants WHERE user_id='$merchantId';"

    Write-Output '1. Admin list: services, both lockers, Merchant, customer avatar, Shipper contact; no locker codes'
    $a=(Call-Api Post '/wash-order/filter' @{keyword=$o.orderCode;page=1;take=5} $admin).list[0]
    if($a.services[0].serviceId -ne $standardWash -or $a.services[0].serviceThumbnailUrl -ne "https://example.com/svc-$run.png"){throw 'Service refs wrong.'}
    if($a.lockerThumbnailUrl -ne $lImg -or $a.returnLockerThumbnailUrl -ne $rImg){throw 'Locker images wrong (return locker must use its own images).'}
    if($a.merchantPhoneNumber -ne $phone -or $a.merchantThumbnailUrl -ne "https://example.com/shop-$run.png" -or $a.customerAvatarUrl -ne "https://example.com/cus-$run.png"){throw 'Merchant/customer media wrong.'}
    if(!$a.shipperEmail -or !$a.shipperPhone -or !$a.shipperVehicleType){throw 'Shipper contact missing.'}
    if($a.depositPinCode -or $a.pickupPinCode -or $a.qrCodeString -or $a.shipperTaskPinCode -or $a.shipperTaskQrString){throw 'Admin list exposes locker codes.'}

    Write-Output '2. Other roles'
    $m=(Call-Api Get "/wash-order/$($o.id)" $null $merchant).data
    if($m.customerAvatarUrl -or !$m.shipperEmail -or $m.items[0].serviceThumbnailUrl -ne "https://example.com/svc-$run.png"){throw 'Merchant detail wrong.'}
    $c=(Call-Api Get "/wash-order/$($o.id)" $null $customer).data
    if($c.customerAvatarUrl -or $c.shipperPhone -or $c.shipperEmail -or !$c.depositPinCode){throw 'Customer detail wrong.'}
    $avail=(Call-Api Get "/wash-order/$($o.id)/available-shippers" $null $merchant).data
    if(@($avail).Count -and @($avail | Where-Object { !$_.email }).Count){throw 'Available shippers without email.'}

    Write-Output '3. Admin reads codes only through the access-codes endpoint, recorded on the order'
    $codes=(Call-Api Get "/wash-order/$($o.id)/access-codes" $null $admin).data
    if(!$codes.depositPinCode -or !$codes.shipperTaskPinCode -or $codes.qrCodeString -notmatch $o.orderCode){throw 'Access codes missing.'}
    Assert-Status 403 Get "/wash-order/$($o.id)/access-codes" $null $merchant
    $timeline=(Call-Api Get "/wash-order/$($o.id)" $null $admin).data.timeline
    if(@($timeline | Where-Object { $_.actorRole -eq 'Admin' -and $_.note -like 'Admin xem m*' }).Count -ne 1){throw 'Code reveal not recorded.'}
    Invoke-TestDelivery $o.id collect $admin | Out-Null
    if((Call-Api Get "/wash-order/$($o.id)" $null $merchant).data.status -ne 3){throw 'Admin collect via access codes failed.'}

    Write-Output 'PASS: list/detail carry service refs with thumbnails, deposit and return locker images (return locker by its own id), Merchant phone/images, customer avatar for Admin only, assigned Shipper contact for Admin/Merchant only; available shippers include email; Admin list has no locker/task codes, the AdminOnly access-codes endpoint returns them and records each reveal on the order timeline; Admin support collect still works.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($restore){
        $svc=if($oldService){"'$oldService'"}else{'NULL'}
        $av=if($oldAvatar){"'$oldAvatar'::jsonb"}else{'NULL'}
        Sql "UPDATE washgo.services SET images=$svc WHERE id='$standardWash'; UPDATE washgo.users SET avatar=$av WHERE id='$customerId'; DELETE FROM washgo.merchant_images WHERE id='$mImgId';" | Out-Null
    }
    foreach($id in $lockers){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE (locker_id='$id' OR return_locker_id='$id') AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$id';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$id';
COMMIT;
"@ | Out-Null
    }
}
