param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: creates Shipper accounts through the API (credentials read from the local
# Mailpit inbox), a second fixture Merchant and a private locker/service; all deactivated in finally.
$sessions=@{}; $lockerId=$null; $created=@()
$kgServiceId=[Guid]::NewGuid(); $merchantB=[Guid]::NewGuid()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)

function New-Phone { '09' + (Get-Random -Minimum 10000000 -Maximum 99999999) }
function Body([hashtable]$Extra=@{}) {
    $b=@{fullName='Shipper Smoke';phone=(New-Phone);email="shipper-$run-$(Get-Random)@example.com";vehicleType=1;vehiclePlate='59-X1 678.90'}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}
function Read-TempPassword($Email) {
    for($i=0;$i -lt 20;$i++){
        $found=Invoke-RestMethod -Method Get -Uri "$MailpitUrl/api/v1/search?query=$([uri]::EscapeDataString("to:$Email"))"
        if($found.messages.Count){
            $msg=Invoke-RestMethod -Method Get -Uri "$MailpitUrl/api/v1/message/$($found.messages[0].ID)"
            if($msg.Text -match '(?m):\s*\*?([A-Za-z0-9]{12})\*?\s*$'){return $Matches[1]}
        }
        Start-Sleep -Milliseconds 300
    }
    throw "No credentials email for $Email."
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','merchant-b-$run@example.com','full_name','Shipper Smoke Merchant B','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$merchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Shipper Smoke Laundry B','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$merchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','SHIPMGMT_KG_$run','name','Shipper Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="SM_$run";name='Shipper Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Authorization and Merchant scope of the list (UC 2.4.1)'
    Assert-Status 401 Post '/shipper/filter' @{} $null
    Assert-Status 403 Post '/shipper/filter' @{} $customer
    $own=Call-Api Post '/shipper/filter' @{take=100;merchantId="$merchantB"} $merchant
    if(@($own.list | Where-Object { $_.merchantId -ne "$merchantId" }).Count){throw 'Merchant sees other shops.'}
    $seedRow=$own.list | Where-Object { $_.email -eq 'shipper@washgo.vn' }
    if(!$seedRow -or !$seedRow.vehiclePlate){throw 'Seed Shipper missing from own list.'}

    Write-Output '2. Create validation (UC 2.4.2)'
    Assert-Status 400 Post '/shipper/' (Body @{fullName='  '}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{phone='12ab'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{email='not-an-email'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{email='SHIPPER@washgo.vn'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{phone='0904 000 004'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{vehiclePlate='?'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{identityNumber='12345'}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{identityImageUrls=@('/uploads/../x.png')}) $merchant
    Assert-Status 400 Post '/shipper/' (Body) $admin

    Write-Output '3. Create account, credentials by email, new Shipper can sign in'
    $body=Body @{fullName='  Le Van Giao  ';identityNumber='079123456789';vehiclePlate='59-x1 123.45'}
    $res=Call-Api Post '/shipper/' $body $merchant
    $s1=$res.data; $created+=$s1
    if(($res | ConvertTo-Json -Depth 6) -match '"password"'){throw 'Password leaked in response.'}
    if($s1.fullName -ne 'Le Van Giao' -or $s1.merchantId -ne "$merchantId" -or !$s1.isActive -or $s1.vehiclePlate -ne '59-X1 123.45' -or $s1.totalDeliveries -ne 0){throw 'Created Shipper wrong.'}
    $temp=Read-TempPassword $body.email
    $sessions.S1=(Call-Api Post '/auth/login' @{email=$body.email;password=$temp} $null).data
    if($sessions.S1.user.role -ne 'Shipper'){throw 'New account is not a Shipper.'}
    Assert-Status 400 Post '/shipper/' (Body @{email=$body.email}) $merchant
    Assert-Status 400 Post '/shipper/' (Body @{phone=$body.phone}) $merchant

    Write-Output '4. Admin creates for another Merchant; Merchant cannot read or edit it'
    $bodyB=Body @{merchantId="$merchantB";vehicleType=3;vehiclePlate=$null}
    $sB=(Call-Api Post '/shipper/' $bodyB $admin).data; $created+=$sB
    if($sB.merchantId -ne "$merchantB" -or $sB.vehicleType -ne 3){throw 'Admin create for Merchant B failed.'}
    Assert-Status 404 Get "/shipper/$($sB.id)" $null $merchant
    Assert-Status 404 Put "/shipper/$($sB.id)" @{fullName='Hijack';phone=(New-Phone);email="x-$run@example.com";vehicleType=3;isActive=$true} $merchant
    if((Call-Api Post '/shipper/filter' @{keyword=$bodyB.email} $merchant).total -ne 0){throw 'Merchant sees Merchant B Shipper.'}
    if((Call-Api Post '/shipper/filter' @{keyword=$bodyB.email} $admin).total -ne 1){throw 'Admin cannot see Merchant B Shipper.'}

    Write-Output '5. Edit validation and update (UC 2.4.3)'
    $upd=@{fullName='Le Van Giao 2';phone=(New-Phone);email="shipper-$run-new@example.com";vehicleType=2;vehiclePlate='51A-999.99';isActive=$true}
    $bad=$upd.Clone(); $bad.fullName=''; Assert-Status 400 Put "/shipper/$($s1.id)" $bad $merchant
    $bad=$upd.Clone(); $bad.phone='abc'; Assert-Status 400 Put "/shipper/$($s1.id)" $bad $merchant
    $bad=$upd.Clone(); $bad.email='shipper@washgo.vn'; Assert-Status 400 Put "/shipper/$($s1.id)" $bad $merchant
    $bad=$upd.Clone(); $bad.phone=$bodyB.phone; Assert-Status 400 Put "/shipper/$($s1.id)" $bad $merchant
    $same=$upd.Clone(); $same.phone=$body.phone; $same.email=$body.email
    if((Call-Api Put "/shipper/$($s1.id)" $same $merchant).data.email -ne $body.email){throw 'Keeping own phone/email rejected.'}
    $u=(Call-Api Put "/shipper/$($s1.id)" $upd $merchant).data
    if($u.fullName -ne 'Le Van Giao 2' -or $u.vehicleType -ne 2 -or $u.email -ne $upd.email){throw 'Update not saved.'}
    Call-Api Post '/auth/login' @{email=$upd.email;password=$temp} $null | ForEach-Object { $sessions.S1b=$_.data }
    if(!$sessions.S1b.accessToken){throw 'Login with the new email failed.'}

    Write-Output '6. Active task blocks Tam nghi; finished tasks count as deliveries; inactive Shipper cannot be assigned'
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId=$s1.id} $merchant | Out-Null
    if((Call-Api Get "/shipper/$($s1.id)" $null $merchant).data.activeTaskCount -ne 1){throw 'Active task not counted.'}
    $off=$upd.Clone(); $off.isActive=$false
    Assert-Status 400 Put "/shipper/$($s1.id)" $off $merchant
    Invoke-TestDelivery $o.id collect $sessions.S1b.accessToken | Out-Null
    $after=(Call-Api Get "/shipper/$($s1.id)" $null $merchant).data
    if($after.totalDeliveries -ne 1 -or $after.activeTaskCount -ne 0){throw 'Delivery not counted after collect.'}
    if((Call-Api Put "/shipper/$($s1.id)" $off $merchant).data.isActive){throw 'Could not set Tam nghi.'}
    if((Call-Api Post '/shipper/filter' @{keyword=$upd.email;isActive=$false} $merchant).total -ne 1){throw 'Inactive filter wrong.'}
    $o2=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=((Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0].id);paymentMethod=2;items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
    Invoke-TestDelivery $o2.id deposit $customer | Out-Null
    Assert-Status 400 Put '/wash-order/assign-shipper' @{orderId=$o2.id;shipperId=$s1.id} $merchant

    Write-Output 'PASS: Shipper list scope/filters, create validation and duplicates, emailed credentials and login, admin cross-merchant create, edit validation, active-task guard, delivery count, inactive not assignable.'
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
    foreach($s in $created){ Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$($s.id)'; UPDATE washgo.users SET is_active=false WHERE id='$($s.userId)';" | Out-Null }
    Sql "UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB'; UPDATE washgo.users SET is_active=false WHERE id='$merchantB'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$kgServiceId';"|Out-Null
}
