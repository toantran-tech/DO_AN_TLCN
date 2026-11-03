param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: a fixture Merchant B proposes services; Admin approves/rejects; a private locker
# is used to check that a service with an unfinished order cannot be deleted.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid()
$name="Giat hap $run"

function Proposal([hashtable]$Extra=@{}) {
    $b=@{name=$name;description='Giat hap cao cap';unitPrice=100000;estimatedDurationHours=24;serviceType=1;unit='kg';reason='Mo rong dich vu'}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}
# The catalog is shared, so this run's service is found by its unique name.
function Mine($Token) { Call-Api Post '/merchant-service/filter' @{keyword=$run;page=1;take=20} $Token }
function Notes($Token,$Type) { @((Call-Api Get '/notification?take=100' $null $Token).list | Where-Object { $_.type -eq $Type }) }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$seedMerchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','svc-b-$run@example.com','full_name','Service Smoke Merchant B','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Service Smoke Laundry B','tax_code',NULL,'status',1))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="svc-b-$run@example.com";password='Merchant@123'} $null).data;$merchant=$sessions.B.accessToken

    Write-Output '1. Shared service list and direct writes blocked (UC 2.6.1)'
    $all=Call-Api Post '/merchant-service/filter' @{page=1;take=100} $merchant
    if($all.total -lt 1){throw 'Shared catalog not visible to a new Merchant.'}
    $empty=Mine $merchant
    if($empty.total -ne 0 -or !$empty.message){throw 'Empty service message missing.'}
    Assert-Status 403 Post '/merchant-service/filter' @{} $admin
    Assert-Status 403 Post '/wash-service/filter' @{} $merchant
    Assert-Status 403 Post '/wash-service/' @{merchantId="$merchantB";code="X_$run";name='x';unitPrice=1000;unit='kg'} $merchant

    Write-Output '2. Propose new service: validation, pending, Admin notified (UC 2.6.2)'
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal @{unitPrice=0}) $merchant
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal @{name='  '}) $merchant
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal @{estimatedDurationHours=0}) $merchant
    $p1=Call-Api Post '/merchant-service/proposals' (Proposal) $merchant
    if($p1.data.status -ne 1 -or $p1.data.requestType -ne 1 -or $p1.data.proposed.name -ne $name){throw 'Proposal not pending.'}
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal @{name=$name.ToUpper()}) $merchant
    # Names are unique across the shared catalog, including other Merchants' pending proposals.
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal) $seedMerchant
    if(@(Notes $admin 46 | Where-Object { $_.linkId -eq $p1.data.id }).Count -ne 1){throw 'Admin not notified.'}
    if((Mine $merchant).total -ne 0){throw 'Pending proposal already listed as a service.'}

    Write-Output '3. Proposal lists and scope'
    $adminList=Call-Api Post '/service-proposal/filter' @{status=1;merchantId="$merchantB"} $admin
    if($adminList.total -ne 1 -or $adminList.list[0].merchantName -ne 'Service Smoke Laundry B'){throw 'Admin proposal list wrong.'}
    $mine=Call-Api Post '/service-proposal/filter' @{merchantId="$seedMerchantId"} $merchant
    if($mine.total -ne 1 -or $mine.list[0].id -ne $p1.data.id){throw 'Merchant proposal scope wrong.'}
    Assert-Status 404 Get "/service-proposal/$($p1.data.id)" $null $seedMerchant
    Assert-Status 403 Post "/service-proposal/$($p1.data.id)/approve" $null $merchant

    Write-Output '4. Approve creates the service (UC 3.5.5)'
    $ok=Call-Api Post "/service-proposal/$($p1.data.id)/approve" $null $admin
    if($ok.data.status -ne 2 -or !$ok.data.serviceId){throw 'Approve failed.'}
    $serviceId=$ok.data.serviceId
    Assert-Status 400 Post "/service-proposal/$($p1.data.id)/approve" $null $admin
    $services=Mine $merchant
    if($services.total -ne 1 -or $services.list[0].id -ne $serviceId -or $services.list[0].unitPrice -ne 100000 -or $services.list[0].approvalStatus -ne 2){throw 'Approved service not listed.'}
    if((Mine $seedMerchant).list[0].id -ne $serviceId){throw 'Approved service not shared with other Merchants.'}
    if(@(Notes $merchant 26).Count -ne 1){throw 'Merchant not notified of approval.'}
    Assert-Status 400 Post '/merchant-service/proposals' (Proposal) $merchant

    Write-Output '5. Propose edit, reject with reason (UC 2.6.3, 3.5.6)'
    $edit=@{name=$name;description='Giat hap moi';unitPrice=120000;estimatedDurationHours=20;reason='Tang gia nguyen lieu'}
    Assert-Status 400 Post "/merchant-service/$serviceId/proposals" (@{name=$name;unitPrice=-1;estimatedDurationHours=20}) $merchant
    Assert-Status 404 Post "/merchant-service/$([Guid]::NewGuid())/proposals" $edit $merchant
    $p2=(Call-Api Post "/merchant-service/$serviceId/proposals" $edit $merchant).data
    if($p2.requestType -ne 2 -or $p2.current.unitPrice -ne 100000 -or $p2.proposed.unitPrice -ne 120000){throw 'Edit proposal snapshot wrong.'}
    Assert-Status 400 Post "/merchant-service/$serviceId/proposals" $edit $merchant
    # One pending proposal per shared service, whichever Merchant sent it.
    Assert-Status 400 Post "/merchant-service/$serviceId/proposals" $edit $seedMerchant
    Assert-Status 400 Post "/service-proposal/$($p2.id)/reject" @{reason=' '} $admin
    $rej=(Call-Api Post "/service-proposal/$($p2.id)/reject" @{reason='Gia chua hop ly'} $admin).data
    if($rej.status -ne 3 -or $rej.rejectionReason -ne 'Gia chua hop ly'){throw 'Reject failed.'}
    if((Mine $merchant).list[0].unitPrice -ne 100000){throw 'Rejected edit changed the service.'}
    $rejNote=@(Notes $merchant 26 | Where-Object { $_.content -match 'Gia chua hop ly' })
    if($rejNote.Count -ne 1){throw 'Merchant not told the rejection reason.'}

    Write-Output '6. Approve an edit (UC 3.5.5 update)'
    $p3=(Call-Api Post "/merchant-service/$serviceId/proposals" $edit $merchant).data
    Call-Api Post "/service-proposal/$($p3.id)/approve" $null $admin | Out-Null
    $updated=(Call-Api Get "/wash-service/$serviceId" $null $admin).data
    if($updated.unitPrice -ne 120000 -or $updated.estimatedDurationHours -ne 20 -or $updated.description -ne 'Giat hap moi'){throw 'Approved edit not applied.'}
    if((Call-Api Get "/customer/services/$serviceId" $null $customer).data.unitPrice -ne 120000){throw 'Customers do not see the shared new price.'}

    Write-Output '7. Admin service management (UC 3.5.1-3.5.4)'
    $list=Call-Api Post '/wash-service/filter' @{merchantId="$merchantB";approvalStatus=2} $admin
    if($list.total -ne 1){throw 'Admin filter by Merchant/approval wrong.'}
    Assert-Status 400 Post '/wash-service/' @{merchantId="$merchantB";code="DUP_$run";name=$name.ToLower();unitPrice=50000;unit='kg';estimatedDurationHours=24} $admin
    Assert-Status 400 Post '/wash-service/' @{merchantId="$([Guid]::NewGuid())";code="NOM_$run";name="Other $run";unitPrice=50000;unit='kg';estimatedDurationHours=24} $admin
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantB";code="SV_$run";name='Service Smoke Locker';address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=@(@{boxNumber='1';size=2})} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
    Assert-Status 400 Delete "/wash-service/$serviceId" $null $admin
    Call-Api Post "/wash-order/$($o.id)/cancel" @{reason='Service smoke'} $customer | Out-Null
    Call-Api Delete "/wash-service/$serviceId" $null $admin | Out-Null
    Assert-Status 404 Get "/wash-service/$serviceId" $null $admin
    if((Mine $merchant).total -ne 0){throw 'Deleted service still listed.'}
    Assert-Status 404 Get "/customer/services/$serviceId" $null $customer

    Write-Output 'PASS: shared service list for every Merchant, global name/pending uniqueness, direct writes blocked, propose/validate/duplicate, admin notified, scope, approve creates/updates service, reject with reason, admin filters/duplicate name/merchant check, delete blocked while order unfinished.'
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
    Sql "UPDATE washgo.services SET is_deleted=true,is_active=false WHERE merchant_id='$merchantB'; UPDATE washgo.users SET is_active=false WHERE id='$merchantB'; UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB';"|Out-Null
}
