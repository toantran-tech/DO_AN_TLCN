param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: changes the global settings row and restores the original values in
# finally; uses a private locker/service and one fixture Merchant account.
$sessions=@{}; $lockerId=$null; $original=$null
$kgServiceId=[Guid]::NewGuid(); $merchantUser=[Guid]::NewGuid()

function Sla($s) { @{depositWindowMinutes=$s.depositWindowMinutes;merchantCollectSlaHours=$s.merchantCollectSlaHours
    merchantReturnSlaHours=$s.merchantReturnSlaHours;freePickupHours=$s.freePickupHours} }
function Penalty($s) {
    $p=@{}
    foreach($k in 'overdueFeeDay1','overdueFeeDay2','overdueFeeDay3','storageFeePerDay','cancelLatePendingPercent',
        'cancelDepositedPercent','cancelCollectedPercent','compensationLightPercent','compensationMediumPercent',
        'compensationSeverePercent','compensationLostPercent'){ $p[$k]=$s.$k }
    return $p
}
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2
        items=@(@{serviceId="$kgServiceId";quantity=1})} $customer).data
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','SET_KG_$($kgServiceId.ToString('N'))','name','Settings Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="SX_$([Guid]::NewGuid().ToString('N'))";name='Settings Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Authorization and current values'
    Assert-Status 401 Get '/system-setting' $null $null
    Assert-Status 403 Get '/system-setting' $null $merchant
    Assert-Status 403 Put '/system-setting/commission' @{defaultCommissionRate=10} $customer
    $original=(Call-Api Get '/system-setting' $null $admin).data
    if(!$original.depositWindowMinutes -or $original.storageTransferAfterHours -ne 72){throw 'Settings not readable.'}

    Write-Output '2. Validation (UC 3.9.1, 3.9.2, 3.9.4)'
    $bad=Sla $original; $bad.depositWindowMinutes=0
    Assert-Status 400 Put '/system-setting/sla' $bad $admin
    $bad=Sla $original; $bad.freePickupHours=-1
    Assert-Status 400 Put '/system-setting/sla' $bad $admin
    Assert-Status 400 Put '/system-setting/commission' @{defaultCommissionRate=101} $admin
    Assert-Status 400 Put '/system-setting/commission' @{defaultCommissionRate=-1} $admin
    $bad=Penalty $original; $bad.cancelDepositedPercent=120
    Assert-Status 400 Put '/system-setting/penalty' $bad $admin
    $bad=Penalty $original; $bad.compensationLightPercent=80; $bad.compensationMediumPercent=70
    Assert-Status 400 Put '/system-setting/penalty' $bad $admin
    $bad=Penalty $original; $bad.overdueFeeDay1=-5
    Assert-Status 400 Put '/system-setting/penalty' $bad $admin
    if((Call-Api Get '/system-setting' $null $admin).data.depositWindowMinutes -ne $original.depositWindowMinutes){throw 'Invalid update changed settings.'}

    Write-Output '3. Deposit window and late-cancel percentage follow the settings'
    $sla=Sla $original; $sla.depositWindowMinutes=10
    $saved=Call-Api Put '/system-setting/sla' $sla $admin
    if($saved.data.depositWindowMinutes -ne 10 -or !$saved.data.updatedAt){throw 'SLA not saved.'}
    $pen=Penalty $original; $pen.cancelLatePendingPercent=40
    Call-Api Put '/system-setting/penalty' $pen $admin | Out-Null
    $o=New-Order
    Sql "UPDATE washgo.orders SET created_on=EXTRACT(EPOCH FROM NOW())::bigint-11*60 WHERE id='$($o.id)';" | Out-Null
    Assert-Status 400 Post "/wash-order/$($o.id)/deposit" @{pinCode=$o.depositPinCode} $customer
    $quote=(Call-Api Get "/wash-order/$($o.id)/cancellation-quote" $null $customer).data
    if($quote.feePercent -ne 40 -or $quote.feeAmount -ne 40000){throw "Late-cancel fee ignored settings: $($quote.feePercent)% $($quote.feeAmount)."}
    $cancelled=(Call-Api Post "/wash-order/$($o.id)/cancel" @{reason='Settings smoke'} $customer).data
    if($cancelled.cancelFeeAmount -ne 40000){throw 'Cancel did not charge the configured fee.'}
    $pen.cancelLatePendingPercent=$original.cancelLatePendingPercent
    Call-Api Put '/system-setting/penalty' $pen $admin | Out-Null
    $after=(Call-Api Get "/wash-order/$($o.id)/cancellation-quote" $null $customer).data
    if($after.feePercent -ne 40){throw 'Cancelled order percentage changed with later settings.'}
    $o2=New-Order
    Sql "UPDATE washgo.orders SET created_on=EXTRACT(EPOCH FROM NOW())::bigint-5*60 WHERE id='$($o2.id)';" | Out-Null
    Invoke-TestDelivery $o2.id deposit $customer | Out-Null

    Write-Output '4. Default commission applies to new Merchants only when omitted'
    Call-Api Put '/system-setting/commission' @{defaultCommissionRate=27.5} $admin | Out-Null
    $m=(Call-Api Post '/merchant/' @{email="settings-$($merchantUser.ToString('N'))@example.com";businessName='Settings Smoke Laundry';address='Test';district='Test';city='Test';phone='0900000000';latitude=10.7;longitude=106.6} $admin).data
    $merchantUser=[Guid]$m.id
    if($m.commissionRate -ne 27.5){throw "Default commission not applied: $($m.commissionRate)."}
    $upd=@{id="$merchantUser";businessName='Settings Smoke Laundry 2';address='Test';district='Test';city='Test';phone='0900000000';latitude=10.7;longitude=106.6}
    $kept=(Call-Api Put '/merchant/' $upd $admin).data
    if($kept.commissionRate -ne 27.5 -or $kept.businessName -ne 'Settings Smoke Laundry 2'){throw 'Update without commission changed it.'}
    $upd.commissionRate=15
    if((Call-Api Put '/merchant/' $upd $admin).data.commissionRate -ne 15){throw 'Explicit commission not saved.'}
    $upd.commissionRate=150
    Assert-Status 400 Put '/merchant/' $upd $admin

    Write-Output 'PASS: settings authorization/validation, deposit window, configured late-cancel fee kept on cancelled orders, default commission for new Merchants.'
}
finally {
    if($original){
        try {
            Call-Api Put '/system-setting/sla' (Sla $original) $sessions.Admin.accessToken | Out-Null
            Call-Api Put '/system-setting/commission' @{defaultCommissionRate=$original.defaultCommissionRate} $sessions.Admin.accessToken | Out-Null
            Call-Api Put '/system-setting/penalty' (Penalty $original) $sessions.Admin.accessToken | Out-Null
        } catch { Write-Warning 'Could not restore system settings.' }
    }
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
    Sql "UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantUser'; UPDATE washgo.users SET is_active=false WHERE id='$merchantUser'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$kgServiceId';"|Out-Null
}
