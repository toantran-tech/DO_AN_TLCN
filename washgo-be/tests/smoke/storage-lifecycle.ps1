param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
trap { Write-Warning $_.Exception.Message; Write-Warning $_.ScriptStackTrace; break }
$sessions=@{};$orderIds=@();$lockerId=$null;$serviceId=[Guid]::NewGuid();$run=[Guid]::NewGuid().ToString('N')
function Login-StorageFixture($Role) {
    for($attempt=0;$attempt -lt 2;$attempt++) {
        try{return (Call-Api Post '/auth/login' @{email="$($Role.ToLower())@washgo.vn";password="${Role}@123"} $null).data}
        catch{if($attempt -gt 0 -or [int]$_.Exception.Response.StatusCode -ne 429){throw};Start-Sleep -Seconds 60}
    }
}
function Storage-Body($Id) { @{operationId=[Guid]::NewGuid().ToString();expectedRevision=(Call-Api Get "/wash-order/$Id" $null $admin).data.revision} }
function Assign-Storage($Id) {
    $body=Storage-Body $Id;$body.shipperId="$shipperId"
    (Call-Api Post "/storage/$Id/assign" $body $merchant).data
}
function Storage-Step($Id,$Cycle,$Step,$Expected=200) {
    $codes=(Call-Api Get "/shipper-tasks/$($Cycle.taskId)/credentials" $null $shipper).data
    $body=Storage-Body $Id;$body.taskId=$Cycle.taskId;$body.pinCode=$codes.pinCode
    if($Step -eq 'handover'){$body.receiptReference="RCPT-$run"}
    if($Expected -ne 200){Assert-Status $Expected Post "/storage/$Id/$Step" $body $shipper;return}
    $first=(Call-Api Post "/storage/$Id/$Step" $body $shipper).data
    $replay=(Call-Api Post "/storage/$Id/$Step" $body $shipper).data
    if($first.orderRevision -ne $replay.orderRevision){throw "Replay changed revision for $Step"}
    $first
}
function Set-Work($Status,$Expected=200) {
    $profile=(Call-Api Get '/shipper-profile/' $null $shipper).data
    $body=@{status=$Status;expectedRevision=$profile.revision;operationId=[Guid]::NewGuid().ToString()}
    if($Expected -ne 200){Assert-Status $Expected Put '/shipper-profile/work-status' $body $shipper;return}
    $result=(Call-Api Put '/shipper-profile/work-status' $body $shipper).data
    $replay=(Call-Api Put '/shipper-profile/work-status' $body $shipper).data
    if($result.revision -ne $replay.revision){throw 'Work status replay changed revision.'}
}
function Pay-StorageFee($Id) {
    $pay=(Call-Api Post '/payment/overdue-fee/create-url' @{orderId="$Id";paymentMethod=2} $customer).data
    if($pay.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Isolated Mock provider required; never use this suite against real payment.'}
    Call-Api Post '/payment/callback' @{orderId="$Id";transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
}
try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){$sessions[$role]=Login-StorageFixture $role}
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken;$shipper=$sessions.Shipper.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id
    $initialWorkStatus=(Call-Api Get "/shipper-profile/" $null $shipper).data.status
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)' AND is_active=true AND NOT is_deleted LIMIT 1;"
    Sql @"
INSERT INTO washgo.services
SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
 'id','$serviceId','code','ST_$run','name','Storage test service','unit','item','base_price',20000,'price_per_item',20000,'price_per_kg',NULL))).*
FROM washgo.services s WHERE is_active=true AND NOT is_deleted LIMIT 1;
"@ | Out-Null
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantId";code="ST_$run";name='Storage fixture';address='Fixture';district='Test';city='Test';latitude=10.7;longitude=106.7;boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2})} $admin).data
    $lockerId=[Guid]$locker.id
    $slot=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0]
    $created=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot.id;items=@(@{serviceId="$serviceId";quantity=1});paymentMethod=2} $customer).data
    $id=[Guid]$created.id;$orderIds+=$id
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$id";shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $id collect $admin | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=4} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId="$id";newStatus=8} $merchant | Out-Null
    $payment=(Call-Api Post '/payment/create-url' @{orderId="$id";paymentMethod=2} $customer).data
    if($payment.paymentUrl -notmatch '/payment/mock-checkout'){throw 'Isolated Mock provider required.'}
    Call-Api Post '/payment/callback' @{orderId="$id";transactionReference=$payment.transactionReference;success=$true} $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId="$id";shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $id return $admin | Out-Null
    foreach($round in 1..2) {
        $old=(Call-Api Get "/wash-order/$id" $null $customer).data
        Sql "UPDATE washgo.orders SET pickup_deadline=now()-interval '73 hours' WHERE id='$id';" | Out-Null
        $assigned=Assign-Storage $id
        if($round -eq 1) {
            $cancel=Storage-Body $id
            Call-Api Post "/storage/$id/cancel-assignment" $cancel $merchant | Out-Null
            $replacement=Assign-Storage $id
            if($replacement.taskId -eq $assigned.taskId -or [Math]::Abs((([DateTime]$replacement.transferDeadline)-([DateTime]$assigned.transferDeadline)).TotalMilliseconds) -gt 1){throw "Reassignment reused task or reset SLA: old=$($assigned.taskId)/$($assigned.transferDeadline), new=$($replacement.taskId)/$($replacement.transferDeadline)"}
            Assert-Status 409 Get "/shipper-tasks/$($assigned.taskId)/credentials" $null $shipper
            $assigned=$replacement
            foreach($attempt in 1..5) {
                $bad=Storage-Body $id;$bad.taskId=$assigned.taskId;$bad.pinCode='000000'
                Assert-Status 400 Post "/storage/$id/collect" $bad $shipper
            }
            if((Sql "SELECT task_pin_attempt_count FROM washgo.orders WHERE id='$id';") -ne '5'){throw 'Wrong PIN attempts rolled back.'}
            Storage-Step $id $assigned collect 400
            Sql "UPDATE washgo.orders SET task_pin_lockout_until=now()-interval '1 second' WHERE id='$id';" | Out-Null
        }
        Set-Work 3 409
        Assert-Status 403 Post "/storage/$id/collect" (Storage-Body $id) $admin
        $before=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlotCount
        $collected=Storage-Step $id $assigned collect
        $after=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlotCount
        if($collected.state -ne 2 -or $after -ne ($before+1)){throw 'Storage collection did not free exactly one box.'}
        $missing=Storage-Body $id;$missing.taskId=$assigned.taskId;$missing.pinCode='000000'
        Assert-Status 400 Post "/storage/$id/handover" $missing $shipper
        $stored=Storage-Step $id $assigned handover
        if($stored.state -ne 3 -or !$stored.intakeAt){throw 'Handover did not begin storage.'}
        Set-Work 3
        Set-Work 1
        Assert-Status 400 Post "/wash-order/$id/pickup-storage" @{pinCode=$old.pickupPinCode} $customer
        $quote=(Call-Api Get "/storage/$id/quote?lockerId=$lockerId" $null $customer).data
        $request=Storage-Body $id;$request.quoteToken=$quote.quoteToken
        $workers=@()
        try {
            foreach($i in 1..2){$workers+=Start-Job -ScriptBlock {
                param($Url,$Token,$Id,$Body)
                (Invoke-RestMethod -Method Post -Uri "$Url/storage/$Id/return-request" -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body $Body -TimeoutSec 30).data
            } -ArgumentList $BaseUrl,$customer,"$id",($request|ConvertTo-Json)}
            $responses=@($workers|Receive-Job -Wait -ErrorAction Stop)
            if($responses.Count -ne 2 -or $responses[0].returnRequestId -ne $responses[1].returnRequestId){throw 'Concurrent operation replay created different requests.'}
            $requested=$responses[0]
        } finally {foreach($worker in $workers){Stop-Job $worker -ErrorAction SilentlyContinue;Remove-Job $worker -Force -ErrorAction SilentlyContinue}}
        $replayed=(Call-Api Post "/storage/$id/return-request" $request $customer).data
        if($requested.returnRequestId -ne $replayed.returnRequestId){throw 'Duplicate storage request.'}
        if($requested.shopAddress -or $requested.receiptReference -or $requested.taskId){throw 'Customer storage DTO leaked internal data.'}
        if($round -eq 1) {
            Call-Api Post "/storage/$id/cancel-return" (Storage-Body $id) $customer | Out-Null
            $newQuote=(Call-Api Get "/storage/$id/quote?lockerId=$lockerId" $null $customer).data
            $renew=Storage-Body $id;$renew.quoteToken=$newQuote.quoteToken
            $newRequest=(Call-Api Post "/storage/$id/return-request" $renew $customer).data
            if($newRequest.returnRequestId -eq $requested.returnRequestId){throw 'Cancelled request was silently reactivated.'}
        }
        Pay-StorageFee $id
        $returning=Assign-Storage $id
        $departure=Storage-Step $id $returning depart
        if($departure.state -ne 4 -or $departure.feeTotal -ne (280000*$round)){throw 'Storage fee accumulation or dispatch freeze failed.'}
        if($round -eq 1) {
            Sql "UPDATE washgo.locker_slots SET status=4 WHERE locker_id='$lockerId' AND status=1; UPDATE washgo.lockers SET available_slots=0 WHERE id='$lockerId';" | Out-Null
            Storage-Step $id $returning place 409
            $status=Sql "SELECT status FROM washgo.orders WHERE id='$id';"
            if($status -ne '15'){throw 'No-slot failure changed custody/order status.'}
            Sql "UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId' AND status=4; UPDATE washgo.lockers SET available_slots=(SELECT count(*) FROM washgo.locker_slots WHERE locker_id='$lockerId' AND status=1) WHERE id='$lockerId';" | Out-Null
        }
        $placed=Storage-Step $id $returning place
        if($placed.state -ne 5){throw 'Return cycle did not close.'}
        $fresh=(Call-Api Get "/wash-order/$id" $null $customer).data
        if($fresh.status -ne 10 -or $fresh.pickupPinCode -eq $old.pickupPinCode -or $fresh.accumulatedStorageFee -ne (280000*$round) -or
            ([DateTime]$fresh.pickupDeadline) -lt [DateTime]::UtcNow.AddHours(23.9)){throw 'Redelivery did not reset credentials/deadline or lost old fees.'}
        Set-Work 3
        Set-Work 1
    }
    $history=Call-Api Get "/storage/$id/cycles?page=1&take=1" $null $customer
    if($history.total -ne 2 -or @($history.list).Count -ne 1){throw 'Cycle history pagination missing.'}
    Invoke-TestDelivery $id pickup $customer | Out-Null
    $finished=(Call-Api Get "/wash-order/$id" $null $customer).data
    if($finished.status -ne 12 -or $finished.storageFeeTotal -ne 560000){throw 'Storage redelivery did not finish through the normal two-step pickup.'}
    # Overdue unpaid goods already at the shop: worker creates a cycle, no fabricated delivery/receipt.
    $unpaid=[Guid]::NewGuid();$orderIds+=$unpaid
    Sql @"
INSERT INTO washgo.orders
SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
 'id','$unpaid','order_code','UP$($run.Substring(0,14))','merchant_order_code',NULL,'receipt_number',NULL,
 'status',8,'payment_status',1,'payment_deadline',now()-interval '1 minute','storage_workflow_version',1,
 'active_storage_cycle_id',NULL,'storage_fee_total',0,'accumulated_storage_fee',0,'overdue_fee_total',0,
 'locker_overdue_fee_at_storage_transfer',0,'moved_to_storage_at',NULL))).*
FROM washgo.orders o WHERE id='$id';
"@ | Out-Null
    Call-Api Post '/notification/reminders/run' $null $admin | Out-Null
    $systemCycle=(Call-Api Get "/storage/$unpaid/cycles?page=1&take=10" $null $admin).list[0]
    if($systemCycle.state -ne 3 -or $systemCycle.basis -ne 'SystemUnpaidAtShop' -or $systemCycle.taskId -or $systemCycle.receiptReference){throw 'Unpaid storage worker invented a delivery or missed the cycle.'}
    # Legacy verification preserves existing money/timestamps, without a fabricated Shipper handover.
    $legacy=[Guid]::NewGuid();$orderIds+=$legacy
    Sql @"
INSERT INTO washgo.orders
SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
 'id','$legacy','order_code','LG$($run.Substring(0,14))','merchant_order_code',NULL,'receipt_number',NULL,
 'status',13,'storage_workflow_version',0,'active_storage_cycle_id',NULL,'storage_fee_total',0,
 'accumulated_storage_fee',0,'locker_overdue_fee_at_storage_transfer',80000,'moved_to_storage_at',now()-interval '1 hour'))).*
FROM washgo.orders o WHERE id='$id';
"@ | Out-Null
    Assert-Status 409 Get "/storage/$legacy/quote?lockerId=$lockerId" $null $customer
    $verify=Storage-Body $legacy;$verify.reason='Fixture evidence checked';$verify.confirmedAtCurrentShop=$true;$verify.verificationReference="CHECK-$run"
    $verified=(Call-Api Post "/storage/$legacy/verify-legacy" $verify $admin).data
    if($verified.feeTotal -ne 180000 -or $verified.receiptReference -or $verified.basis -ne 'LegacyAdminVerifiedCurrentShop'){throw 'Legacy verification invented money/handover evidence.'}
    Call-Api Get "/storage/$legacy/quote?lockerId=$lockerId" $null $customer | Out-Null
    $work=(Call-Api Get '/shipper-profile/' $null $shipper).data
    Assert-Status 409 Put '/shipper-profile/work-status' @{status=3;expectedRevision='stale';operationId=[Guid]::NewGuid().ToString()} $shipper
    Assert-Status 403 Put '/shipper-profile/work-status' @{status=3;expectedRevision=$work.revision;operationId=[Guid]::NewGuid().ToString()} $customer
    Call-Api Get '/admin/alerts?page=1&take=10' $null $admin | Out-Null
    Call-Api Get '/admin/dashboard' $null $admin | Out-Null
    Call-Api Post '/storage/filter' @{page=1;take=1} $merchant | Out-Null
    Call-Api Get '/storage/summary' $null $merchant | Out-Null
    $summary=(Call-Api Post '/shipper-delivery/summary' @{fromDate=0;toDate=0} $shipper).data
    if($summary.storageTransferCount -lt 2 -or $summary.storageReturnCount -lt 2){throw 'Storage tasks missing from delivery history summary.'}
    Write-Output 'PASS: self-status guards/replay, two complete storage/redelivery cycles, anonymous requests, fees, no-slot rollback, rotated PIN/new deadline, history and normal final pickup.'
}
finally {
    # Dedicated fixture records only. The surrounding CI database is disposable; never run on production.
    if($orderIds.Count){
        $ids=($orderIds|ForEach-Object{"'$_'"})-join ','
        Sql "DELETE FROM washgo.storage_return_requests WHERE order_id IN ($ids); DELETE FROM washgo.delivery_tasks WHERE order_id IN ($ids); DELETE FROM washgo.order_storage_cycles WHERE order_id IN ($ids); DELETE FROM washgo.order_operations WHERE order_id IN ($ids); DELETE FROM washgo.order_status_history WHERE order_id IN ($ids); DELETE FROM washgo.orders WHERE id IN ($ids);" | Out-Null
    }
    if($shipper -and $initialWorkStatus -in @(1,3)) {
        try { if((Call-Api Get '/shipper-profile/' $null $shipper).data.status -ne $initialWorkStatus){Set-Work $initialWorkStatus} }
        catch { Write-Warning 'Could not restore fixture Shipper work status.' }
    }
    if($lockerId){Sql "UPDATE washgo.lockers SET is_deleted=true,status=2 WHERE id='$lockerId'; UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$lockerId';" | Out-Null}
    Sql "UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';" | Out-Null
    foreach($session in $sessions.Values){if($session.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}}
}
