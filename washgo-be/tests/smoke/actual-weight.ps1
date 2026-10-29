param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Uses a dedicated locker and cloned services; never changes seed prices or user orders.
$sessions=@{}; $orders=@(); $jobs=@(); $lockerId=$null
$weightService=[Guid]::NewGuid(); $secondWeightService=[Guid]::NewGuid(); $itemService=[Guid]::NewGuid()
$completed=$false
function Create-TestOrder($Size, $Mixed=$false, $CountOnly=$false) {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $slot=$locker.availableSlots | Where-Object {$_.size -eq $Size} | Select-Object -First 1
    if(!$slot){throw "No free fixture slot of size $Size"}
    $items=@(@{serviceId="$weightService";quantity=1;weightKg=9})
    if($Mixed){$items+=@{serviceId="$secondWeightService";quantity=1};$items+=@{serviceId="$itemService";quantity=2}}
    if($CountOnly){$items=@(@{serviceId="$itemService";quantity=2})}
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot.id;items=$items;paymentMethod=2;customerNote='Actual weight smoke'} $customer).data
}
function Finish-TestOrder($Id) {
    Call-Api Put '/wash-order/status' @{orderId="$Id";newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$Id";newStatus=8} $merchant | Out-Null
    $payment=(Call-Api Post '/payment/create-url' @{orderId="$Id";paymentMethod=2} $customer).data
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Local mock payment provider required.'}
    Call-Api Post '/payment/callback' @{orderId="$Id";transactionReference=$payment.transactionReference;success=$true} $admin | Out-Null
    Assert-Status 400 Put "/wash-order/$Id/actual-weight" @{weightKg=1} $merchant
    foreach($state in @(10,11,12)){Call-Api Put '/wash-order/status' @{orderId="$Id";newStatus=$state} $admin | Out-Null}
}
try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken; $customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id
    Sql @"
INSERT INTO washgo.services
SELECT (jsonb_populate_record(NULL::washgo.services, to_jsonb(s) || jsonb_build_object(
 'id','$weightService','code','W1_$($weightService.ToString('N'))','name','Weight Smoke Package',
 'unit','kg','base_price',100000,'price_per_kg',30000,'price_per_item',NULL,'estimated_time',24))).*
FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.services
SELECT (jsonb_populate_record(NULL::washgo.services, to_jsonb(s) || jsonb_build_object(
 'id','$secondWeightService','code','W2_$($secondWeightService.ToString('N'))','name','Weight Smoke Extra Package',
 'base_price',50000,'estimated_time',12))).* FROM washgo.services s WHERE id='$weightService';
INSERT INTO washgo.services
SELECT (jsonb_populate_record(NULL::washgo.services, to_jsonb(s) || jsonb_build_object(
 'id','$itemService','code','WI_$($itemService.ToString('N'))','name','Weight Smoke Count Service',
 'unit','item','base_price',20000,'price_per_kg',NULL,'price_per_item',20000,'estimated_time',36))).*
FROM washgo.services s WHERE id='$weightService';
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="WT_$([Guid]::NewGuid().ToString('N'))";name='Actual Weight Smoke Locker'
        address='Development test fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=1},@{boxNumber='2';size=2},@{boxNumber='3';size=3})
    } $admin).data
    $lockerId=[Guid]$locker.id
    $cases=@(
        @{size=1;included=3;kg=2.5},@{size=1;included=3;kg=3},@{size=1;included=3;kg=3.25},
        @{size=2;included=5;kg=4.5},@{size=2;included=5;kg=5},@{size=2;included=5;kg=6.5},
        @{size=3;included=10;kg=9.5},@{size=3;included=10;kg=10},@{size=3;included=10;kg=11.01},
        @{size=2;included=5;kg=6.5;mixed=$true}
    )
    foreach($case in $cases){
        Sql "UPDATE washgo.services SET base_price=100000, estimated_time=24 WHERE id='$weightService';" | Out-Null
        $created=Create-TestOrder $case.size ([bool]$case.mixed)
        $id=[Guid]$created.id; $orders+=$id
        $base=if($case.mixed){190000}else{100000}
        $hours=if($case.mixed){36}else{24}
        if($created.totalAmount -ne $base -or $created.finalAmount -ne $base -or $created.includedWeightKg -ne $case.included){throw 'New-order package pricing multiplied estimated kg or used wrong capacity.'}
        Assert-Status 400 Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $merchant
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=2} $admin | Out-Null
        Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=3} $admin | Out-Null
        Assert-Status 400 Put '/wash-order/status' @{orderId="$id";newStatus=4} $merchant
        Assert-Status 400 Put '/wash-order/status' @{orderId="$id";newStatus=8} $merchant
        foreach($token in @($admin,$customer,$sessions.Shipper.accessToken)){
            Assert-Status 403 Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $token
        }
        Assert-Status 401 Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $null
        foreach($invalid in @(0,-1,1000.01,1.001)){
            Assert-Status 400 Put "/wash-order/$id/actual-weight" @{weightKg=$invalid} $merchant
        }
        $before=(Call-Api Get "/wash-order/$id" $null $merchant).data
        if($before.actualWeightKg -or $before.weightSurchargeAmount -ne 0 -or $before.finalAmount -ne $base){throw 'Rejected request changed order pricing.'}

        # Only this fixture order temporarily changes owner to exercise scope checks.
        Sql "UPDATE washgo.orders SET merchant_id=NULL WHERE id='$id';" | Out-Null
        Assert-Status 404 Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $merchant
        Sql "UPDATE washgo.orders SET merchant_id='$merchantId' WHERE id='$id'; UPDATE washgo.services SET base_price=999000, estimated_time=72 WHERE id='$weightService';" | Out-Null
        if($case.size -eq 2 -and $case.kg -eq 6.5 -and !$case.mixed){
            foreach($i in 1..2){
                $jobs+=Start-Job -ScriptBlock {
                    param($Url,$Token,$Id,$Kg)
                    try{Invoke-RestMethod -Method Put -Uri "$Url/wash-order/$Id/actual-weight" -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body (@{weightKg=$Kg}|ConvertTo-Json)|Out-Null;200}
                    catch{[int]$_.Exception.Response.StatusCode}
                } -ArgumentList $BaseUrl,$merchant,"$id",$case.kg
            }
            $codes=@($jobs|Receive-Job -Wait -AutoRemoveJob);$jobs=@()
            if((($codes|Sort-Object)-join ',') -ne '200,200'){throw "Concurrent weight confirmation returned $codes"}
        }
        else{Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg;finalAmount=1;includedWeightKg=999} $merchant | Out-Null}
        $weighed=(Call-Api Get "/wash-order/$id" $null $merchant).data
        Assert-Private $weighed
        $excess=[Math]::Max([decimal]0, [decimal]$case.kg-[decimal]$case.included)
        $fee=$excess*15000; $duration=$hours+2*[Math]::Ceiling($excess)
        $actualDuration=(([DateTime]$weighed.estimatedFinishAt)-([DateTime]$weighed.etaUpdatedAt)).TotalHours
        if($weighed.status -ne 3 -or $weighed.actualWeightKg -ne $case.kg -or
            $weighed.totalAmount -ne $base -or $weighed.weightSurchargeAmount -ne $fee -or
            $weighed.finalAmount -ne ($base+$fee) -or $weighed.excessWeightKg -ne $excess -or
            [Math]::Abs($actualDuration-$duration) -gt 0.000001 -or $weighed.baseProcessingHours -ne $hours -or
            $weighed.timeline.Count -ne ($before.timeline.Count+1)){
            [PSCustomObject]@{case=$case;expectedBase=$base;actualBase=$weighed.totalAmount;expectedFee=$fee;actualFee=$weighed.weightSurchargeAmount;actualFinal=$weighed.finalAmount;actualWeight=$weighed.actualWeightKg;excess=$weighed.excessWeightKg;expectedDuration=$duration;actualDuration=$actualDuration;baseHours=$weighed.baseProcessingHours;status=$weighed.status;beforeEvents=$before.timeline.Count;afterEvents=$weighed.timeline.Count}|ConvertTo-Json -Depth 5|Write-Output
            throw 'Weight, package snapshot, surcharge, ETA, status, or timeline mismatch.'
        }
        $replayed=(Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $merchant).data
        if($replayed.etaUpdatedAt -ne $weighed.etaUpdatedAt -or $replayed.timeline.Count -ne $weighed.timeline.Count){throw 'Replay extended ETA or duplicated timeline.'}
        $corrected=(Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=([decimal]$case.kg+0.01)} $merchant).data
        $correctedFee=[Math]::Max([decimal]0, [decimal]$case.kg+0.01-[decimal]$case.included)*15000
        if($corrected.finalAmount -ne ($base+$correctedFee)){throw 'Correcting weight accumulated the previous surcharge.'}
        Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=$case.kg} $merchant | Out-Null
        Assert-Status 400 Post '/payment/create-url' @{orderId="$id";paymentMethod=2} $customer
        $customerView=(Call-Api Get "/wash-order/$id" $null $customer).data
        if($customerView.finalAmount -ne ($base+$fee) -or $customerView.actualWeightKg -ne $case.kg){throw 'Customer cannot see final quote.'}
        Finish-TestOrder $id
    }
    $countOrder=Create-TestOrder 2 $false $true
    $id=[Guid]$countOrder.id;$orders+=$id
    Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=2} $admin | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=3} $admin | Out-Null
    Assert-Status 400 Put "/wash-order/$id/actual-weight" @{weightKg=6.5} $merchant
    if($countOrder.totalAmount -ne 40000){throw 'Count-based quantity pricing changed.'}
    Finish-TestOrder $id
    $after=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    if($after.availableSlotCount -ne 3){throw 'Smoke test did not release all fixture slots.'}
    $completed=$true
    Write-Output 'PASS: 3/5/10 kg below/equal/above boundaries, fractional surcharge, mixed services once, snapshot prices/durations, ETA, Merchant scope, invalid payloads, concurrent replay, no payment before washing, paid lock and completed lifecycle.'
}
finally{
    foreach($job in $jobs){Stop-Job $job -ErrorAction SilentlyContinue;Remove-Job $job -Force -ErrorAction SilentlyContinue}
    if($orders.Count -gt 0){
        $ids=($orders|ForEach-Object{"'$_'"}) -join ','
        Sql "UPDATE washgo.orders SET merchant_id='$merchantId' WHERE id IN ($ids);" | Out-Null
        if(!$completed){
            Sql @"
BEGIN;
WITH slots AS (
 SELECT locker_id,locker_slot_id AS id FROM washgo.orders WHERE id IN ($ids) AND status IN (1,2)
 UNION SELECT COALESCE(return_locker_id,locker_id),return_box_id FROM washgo.orders WHERE id IN ($ids) AND status=10
), freed AS (
 UPDATE washgo.locker_slots b SET status=1 FROM slots s WHERE b.id=s.id AND b.locker_id=s.locker_id AND b.status IN (2,3) RETURNING b.locker_id
)
UPDATE washgo.lockers l SET available_slots=available_slots+f.count FROM (SELECT locker_id,COUNT(*)::int AS count FROM freed GROUP BY locker_id) f WHERE l.id=f.locker_id;
UPDATE washgo.orders SET status=99,cancellation_reason='Actual weight smoke cleanup' WHERE id IN ($ids) AND status<>12;
COMMIT;
"@ | Out-Null
        }
    }
    if($lockerId){Sql "UPDATE washgo.lockers SET is_deleted=true,status=2 WHERE id='$lockerId'; UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$lockerId';" | Out-Null}
    Sql "UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id IN ('$weightService','$secondWeightService','$itemService');" | Out-Null
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Could not revoke a smoke session.'}}}
}
