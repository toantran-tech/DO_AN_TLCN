param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: shop/collect assignment statuses (16, 17), strictly sequential processing, ready-for-return (18) and
# return assignment (19) with replay/stale/unassign rules, counts, notifications and the legacy-data migration rules.
$global:StrictTransitions=$true; $global:NoAutoReturnReady=$true; $NoAutoAssign=$true
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid(); $fixtureEmail="status-smoke-$($fixtureUser.ToString('N'))@example.com"

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{Authorization="Bearer $Token"};TimeoutSec=30}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $json=if($_.ErrorDetails.Message){ try { $_.ErrorDetails.Message | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
}
function Detail($Id,$Token){ (Call-Api Get "/wash-order/$Id" $null $Token).data }
function Events($Id,$Type){ @((Detail $Id $admin).timeline | Where-Object { $_.eventType -eq $Type }) }
function Step($Order,$Status){ $Order.workflowSteps | Where-Object { $_.status -eq $Status } }
function Inbox($Token,$Id,$Key){ @((Call-Api Get '/notification?page=1&take=100' $null $Token).list | Where-Object { $_.linkId -eq "$Id" -and $_.messageKey -eq $Key }) }
function Expect-Status($Id,$Status){ $d=Detail $Id $admin; if([int]$d.status -ne $Status){throw "Order status $($d.status), expected $Status."}; $d }
# Same statements as migration OrderAssignmentAndReturnStatuses, limited to one fixture order.
function Invoke-LegacyMigration($Id){
    $valid="o.shipper_id IS NOT NULL AND EXISTS (SELECT 1 FROM washgo.shippers s WHERE s.id = o.shipper_id AND s.merchant_id = o.merchant_id) AND NOT EXISTS (SELECT 1 FROM washgo.delivery_tasks t WHERE t.order_id = o.id AND t.status = 1 AND (t.shipper_id <> o.shipper_id OR t.task_type <> o.assigned_task_type))"
    Sql @"
UPDATE washgo.orders o SET status = 17 WHERE o.id='$Id' AND o.is_deleted = false AND o.status = 2 AND o.merchant_id IS NOT NULL AND o.assigned_task_type = 1 AND $valid;
UPDATE washgo.orders o SET status = 16 WHERE o.id='$Id' AND o.is_deleted = false AND o.status = 2 AND o.merchant_id IS NOT NULL;
UPDATE washgo.orders o SET status = 19 WHERE o.id='$Id' AND o.is_deleted = false AND o.status = 9 AND o.payment_status = 2 AND o.merchant_id IS NOT NULL AND o.assigned_task_type = 2 AND $valid;
"@ | Out-Null
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $seedShipperUser=$sessions.Shipper.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users, to_jsonb(u) || jsonb_build_object(
    'id','$fixtureUser','email','$fixtureEmail','full_name','Status Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).*
FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers, to_jsonb(s) || jsonb_build_object(
    'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).*
FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.Second=(Call-Api Post '/auth/login' @{email=$fixtureEmail;password='Shipper@123'} $null).data
    $second=$sessions.Second.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Status Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerId=[Guid]$locker.id
    $sum0=(Call-Api Post '/wash-order/summary' @{} $admin).data

    Write-Output '1. Deposited without a shop: no collect assignment, no collect'
    $o=New-Order
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $d=Expect-Status $o.id 2
    if($d.allowedActions.canAssignShipper -or $d.allowedActions.assignShipperReasonCode -ne 'NoMerchant'){throw 'Deposited order offered a collect assignment.'}
    if((Raw Post "/wash-order/$($o.id)/shipper-collect" @{pinCode='000000'} $admin).Status -notin 400,409){throw 'Collect allowed before any assignment.'}

    Write-Output '2. Admin assigns the shop: Deposited -> MerchantAssigned in one event'
    Assign-Order $o.id $null $admin | Out-Null
    $d=Expect-Status $o.id 16
    $ev=@(Events $o.id 'MerchantAssigned')
    if($ev.Count -ne 1 -or [int]$ev[0].fromStatus -ne 2 -or [int]$ev[0].status -ne 16){throw 'MerchantAssigned event wrong.'}
    $m=Detail $o.id $merchant
    if(!$m.allowedActions.canAssignShipper -or $m.allowedActions.assignTaskType -ne 1 -or $m.allowedActions.canUnassignShipper -or !$m.isNew){throw 'Merchant actions at MerchantAssigned wrong.'}
    if(@(Inbox $customer $o.id 'merchantAssigned.customer').Count -ne 1){
        $seen=@((Call-Api Get '/notification?page=1&take=100' $null $customer).list | Where-Object { $_.linkId -eq "$($o.id)" } | ForEach-Object { "$($_.type):$($_.messageKey)" }) -join ','
        throw "Customer not told about the shop assignment: [$seen] order $($o.id)"
    }
    $c=Detail $o.id $customer
    if($c.merchantName -or $c.merchantId -or (Step $c 16).isCurrent -ne $true -or !(Step $c 16).timestamp -or !(Step $c 2).isCompleted){throw 'Customer view of the shop assignment wrong.'}
    if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant).Status -ne 400){throw 'Processing opened before collect.'}

    Write-Output '3. Merchant assigns the collect Shipper: MerchantAssigned -> CollectionShipperAssigned; replay and stale'
    $op=[Guid]::NewGuid()
    $a1=Raw Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId";operationId=$op;expectedStatus=16} $merchant
    $a2=Raw Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId";operationId=$op;expectedStatus=16} $merchant
    if($a1.Status -ne 200 -or [int]$a1.Json.data.status -ne 17 -or $a2.Status -ne 200 -or !$a2.Json.meta.replayed){throw "Assign/replay wrong: $($a1.Status)/$($a2.Status)"}
    $ev=@(Events $o.id 'ShipperAssigned')
    if($ev.Count -ne 1 -or [int]$ev[0].fromStatus -ne 16 -or [int]$ev[0].status -ne 17 -or [int]$ev[0].taskType -ne 1){throw 'Collect assignment event wrong.'}
    if((Raw Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$fixtureShipper";expectedStatus=16} $merchant).Status -ne 409){throw 'Stale expectedStatus accepted.'}
    if(@(Inbox $customer $o.id 'shipperAssigned.collect.customer').Count -ne 1){throw 'Customer not told about the collect Shipper.'}
    $firstAssigned=(Step (Detail $o.id $customer) 17).timestamp

    Write-Output '4. Reassign keeps the status and first time; unassign returns to MerchantAssigned and revokes the old task'
    $body1=New-TestDeliveryBody $o.id $shipper
    Start-Sleep -Seconds 1
    $r=Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$fixtureShipper"} $merchant
    if([int]$r.data.status -ne 17 -or @(Events $o.id 'ShipperReassigned').Count -ne 1 -or (Step (Detail $o.id $customer) 17).timestamp -ne $firstAssigned){throw 'Reassignment changed status or the first assignment time.'}
    if(@(Inbox $customer $o.id 'shipperAssigned.collect.customer').Count -ne 1 -or @(Inbox $shipper $o.id 'shipperReassigned.collect.previousShipper').Count -ne 1){throw 'Reassignment notifications wrong.'}
    if((Raw Post "/wash-order/$($o.id)/shipper-collect" $body1 $shipper).Status -eq 200){throw 'Old Shipper kept the collect task.'}
    $body2=New-TestDeliveryBody $o.id $second
    $u=Raw Post "/wash-order/$($o.id)/unassign-shipper" @{reason='Smoke unassign';expectedStatus=17} $merchant
    if($u.Status -ne 200 -or [int]$u.Json.data.status -ne 16 -or $u.Json.data.allowedActions.canUnassignShipper -or !$u.Json.data.allowedActions.canAssignShipper){throw "Unassign wrong: $($u.Status)"}
    $ev=@(Events $o.id 'ShipperUnassigned')
    if($ev.Count -ne 1 -or [int]$ev[0].fromStatus -ne 17 -or [int]$ev[0].status -ne 16){throw 'Unassign event wrong.'}
    if((Raw Post "/wash-order/$($o.id)/shipper-collect" $body2 $second).Status -eq 200){throw 'Unassigned Shipper could still collect.'}
    if((Sql "SELECT count(*) FROM washgo.delivery_tasks WHERE order_id='$($o.id)' AND status=1;") -ne '0'){throw 'Open task left after unassign.'}
    if((Raw Post "/wash-order/$($o.id)/unassign-shipper" @{} $merchant).Status -ne 400){throw 'Unassign without an assignment accepted.'}
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    $d=Expect-Status $o.id 17
    if((Step (Detail $o.id $customer) 17).timestamp -ne $firstAssigned){throw 'Progress step moved after reassignment.'}

    Write-Output '5. Shipper collects: CollectionShipperAssigned -> Collected; processing strictly in sequence'
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Expect-Status $o.id 3 | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    $m=Detail $o.id $merchant
    if((@($m.allowedActions.allowedNextStatuses) -join ',') -ne '5'){throw "Washing next statuses: $(@($m.allowedActions.allowedNextStatuses) -join ',')"}
    foreach($skip in 6,7,8){ if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=$skip} $merchant).Status -ne 400){throw "Skip to $skip accepted."} }
    $rev=$m.revision
    if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=5;expectedRevision=$rev-1} $merchant).Status -ne 409){throw 'Stale revision accepted.'}
    $sop=[Guid]::NewGuid()
    $s1=Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=5;operationId=$sop;expectedRevision=$rev} $merchant
    $s2=Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=5;operationId=$sop;expectedRevision=$rev} $merchant
    if($s1.Status -ne 200 -or $s2.Status -ne 200 -or !$s2.Json.meta.replayed -or @((Detail $o.id $admin).timeline | Where-Object { [int]$_.status -eq 5 -and [int]$_.fromStatus -eq 4 }).Count -ne 1){throw 'Drying replay wrong.'}
    if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=5} $merchant).Status -ne 400){throw 'Repeated Drying accepted.'}
    if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant).Status -ne 400){throw 'Backwards transition accepted.'}
    foreach($next in 6,7,8){ Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=$next} $merchant | Out-Null }
    Expect-Status $o.id 8 | Out-Null
    foreach($s in 16,17,18,19){ if((Raw Put '/wash-order/status' @{orderId=$o.id;newStatus=$s} $admin).Status -ne 400){throw "Generic status route accepted $s."} }

    Write-Output '6. Paid: return assignment needs the Merchant ready confirmation first'
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    $m=Detail $o.id $merchant
    if([int]$m.status -ne 9 -or $m.allowedActions.canAssignShipper -or $m.allowedActions.assignShipperReasonCode -ne 'ReturnNotReady' -or !$m.allowedActions.canConfirmReturnReady){throw 'Paid actions wrong.'}
    if((Raw Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant).Status -ne 400){throw 'Return assigned straight from Paid.'}
    if((Detail $o.id $admin).allowedActions.canConfirmReturnReady){throw 'Admin offered the ready confirmation.'}
    if((Raw Post "/wash-order/$($o.id)/confirm-return-ready" @{} $admin).Status -ne 403){throw 'Admin confirmed readiness.'}
    if((Raw Post "/wash-order/$($o.id)/confirm-return-ready" @{expectedStatus=8} $merchant).Status -ne 409){throw 'Stale ready confirmation accepted.'}
    $rop=[Guid]::NewGuid()
    $c1=Raw Post "/wash-order/$($o.id)/confirm-return-ready" @{operationId=$rop;expectedStatus=9;note='Packed'} $merchant
    $c2=Raw Post "/wash-order/$($o.id)/confirm-return-ready" @{operationId=$rop;expectedStatus=9;note='Packed'} $merchant
    if($c1.Status -ne 200 -or [int]$c1.Json.data.status -ne 18 -or $c2.Status -ne 200 -or !$c2.Json.meta.replayed){throw "Ready confirmation/replay wrong: $($c1.Status)/$($c2.Status)"}
    $ev=@(Events $o.id 'ReturnReadyConfirmed')
    if($ev.Count -ne 1 -or [int]$ev[0].fromStatus -ne 9 -or [int]$ev[0].status -ne 18){throw 'ReturnReadyConfirmed event wrong.'}
    if((Raw Post "/wash-order/$($o.id)/confirm-return-ready" @{} $merchant).Status -ne 400){throw 'Second ready confirmation accepted.'}
    if(@(Inbox $customer $o.id 'returnReadyConfirmed.customer').Count -ne 1 -or @(Inbox $customer $o.id 'returnedToLocker.customer').Count){throw 'Ready notifications wrong.'}
    if(@((Call-Api Get '/notification?page=1&take=100' $null $admin).list | Where-Object { $_.linkId -eq "$($o.id)" -and $_.messageKey -eq 'orderEvent.ReturnReadyConfirmed' }).Count -ne 1){throw 'Admin not told about the ready confirmation.'}
    $c=Detail $o.id $customer
    if($c.pickupDeadline -or (Raw Post "/wash-order/$($o.id)/open-pickup-box" @{pinCode=$c.pickupPinCode} $customer).Status -eq 200){throw 'Customer pickup opened before the locker return.'}

    Write-Output '7. Return assignment: ReadyForReturnDispatch -> ReturnShipperAssigned; unassign back to ReadyForReturnDispatch'
    $r=Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$fixtureShipper";expectedStatus=18} $merchant
    if([int]$r.data.status -ne 19){throw 'Return assignment status wrong.'}
    $ev=@(Events $o.id 'ShipperAssigned' | Where-Object { [int]$_.taskType -eq 2 })
    if($ev.Count -ne 1 -or [int]$ev[0].fromStatus -ne 18 -or [int]$ev[0].status -ne 19){throw 'Return assignment event wrong.'}
    $u=Raw Post "/wash-order/$($o.id)/unassign-shipper" @{expectedStatus=19} $merchant
    if($u.Status -ne 200 -or [int]$u.Json.data.status -ne 18){throw 'Return unassign did not go back to ReadyForReturnDispatch.'}
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    Expect-Status $o.id 19 | Out-Null

    Write-Output '8. Counts: new statuses and groups match the total'
    $sum=(Call-Api Post '/wash-order/summary' @{} $admin).data
    $byStatus=($sum.statusCounts.byStatus.PSObject.Properties | Measure-Object Value -Sum).Sum
    $byGroup=($sum.statusCounts.byGroup.PSObject.Properties | Measure-Object Value -Sum).Sum
    if($sum.statusCounts.returnShipperAssigned -lt 1 -or $byStatus -ne $sum.total -or $byGroup -ne $sum.total -or $null -eq $sum.statusCounts.merchantAssigned -or $null -eq $sum.statusCounts.readyForReturnDispatch){throw "Summary wrong: total=$($sum.total) byStatus=$byStatus byGroup=$byGroup"}
    $dash=(Call-Api Get '/admin/dashboard?period=today' $null $admin).data
    if($null -eq $dash.orders.waitingReturnDelivery -or $dash.orders.waitingReturnDelivery -lt 1 -or $null -eq $dash.orders.waitingCollectAssignment){throw 'Admin dashboard lacks the new waiting counts.'}

    Write-Output '9. Locker return starts the pickup window; Returned -> Completed keep their own steps'
    Invoke-TestDelivery $o.id return $shipper | Out-Null
    $d=Expect-Status $o.id 10
    if(!$d.pickupDeadline){throw 'PickupDeadline not set at the locker return.'}
    if(@(Inbox $customer $o.id 'returnedToLocker.customer').Count -ne 1){throw 'Customer not invited to pick up after the locker return.'}
    Invoke-TestDelivery $o.id pickup $customer | Out-Null
    $done=Detail $o.id $customer
    $missing=@(1,2,16,17,3,4,5,6,7,8,9,18,19,10,11,12 | Where-Object { !(Step $done $_).isApplicable -or !(Step $done $_).timestamp -or (Step $done $_).basis -ne 'StatusChange' })
    if([int]$done.status -ne 12 -or $done.progressPercent -ne 100 -or $missing.Count){throw "Completed workflow wrong: $($missing -join ',')"}

    Write-Output '10. Legacy data: Deposited with shop -> MerchantAssigned, with a valid collect task -> CollectionShipperAssigned'
    $l1=New-Order; Invoke-TestDelivery $l1.id deposit $customer | Out-Null; Assign-Order $l1.id $null $admin | Out-Null
    $l2=New-Order; Invoke-TestDelivery $l2.id deposit $customer | Out-Null; Assign-Order $l2.id $null $admin | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$l2.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    # Rewind to the pre-status shape: order back at Deposited and assignment rows recorded without a status change.
    Sql @"
UPDATE washgo.orders SET status=2 WHERE id IN ('$($l1.id)','$($l2.id)');
UPDATE washgo.order_status_history SET from_status=2, to_status=2 WHERE order_id IN ('$($l1.id)','$($l2.id)') AND event_type IN ('MerchantAssigned','ShipperAssigned');
"@ | Out-Null
    Invoke-LegacyMigration $l1.id; Invoke-LegacyMigration $l2.id
    Expect-Status $l1.id 16 | Out-Null
    $x=Expect-Status $l2.id 17
    if((Step $x 16).basis -ne 'AssignmentEvent' -or !(Step $x 16).timestamp -or (Step $x 17).basis -ne 'AssignmentEvent' -or !(Step $x 17).timestamp -or (Step $x 17).isCurrent -ne $true){throw 'Legacy assignment steps lack their event times.'}
    Invoke-TestDelivery $l2.id collect $second | Out-Null
    Expect-Status $l2.id 3 | Out-Null
    $y=Detail $l2.id $admin
    if((Step $y 17).basis -ne 'AssignmentEvent' -or !(Step $y 17).isCompleted){throw 'Legacy collect step lost after collection.'}

    Write-Output 'PASS: Deposited (no shop) offers no collect trip; Admin shop assignment is Deposited->MerchantAssigned in one event with a safe Customer notice; Merchant collect assignment MerchantAssigned->CollectionShipperAssigned with replay/stale guards; reassignment keeps the status and first step time and revokes the old task; unassign returns to MerchantAssigned with no open task; collect only after the collect assignment; processing strictly Collected->Washing->Drying->Ironing->Folding->AwaitingPayment (skip/backwards/repeat 400, stale 409, replay once); Paid needs the Merchant ready confirmation (Admin 403, stale 409, replay once, Admin event, no pickup invite) before the return assignment ReadyForReturnDispatch->ReturnShipperAssigned, unassign back to ReadyForReturnDispatch; summary/byGroup totals and Admin waiting counts; PickupDeadline only at the locker return; full workflow with real times; legacy Deposited orders migrate to MerchantAssigned/CollectionShipperAssigned with assignment-event times.'
}
finally {
    $global:StrictTransitions=$null; $global:NoAutoReturnReady=$null
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.delivery_tasks SET status=4 WHERE status=1 AND order_id IN (SELECT id FROM washgo.orders WHERE locker_id='$lockerId');
UPDATE washgo.orders SET status=99, shipper_id=NULL, assigned_task_type=NULL WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.orders SET shipper_id=NULL WHERE shipper_id='$fixtureShipper'; DELETE FROM washgo.refresh_tokens WHERE user_id='$fixtureUser'; DELETE FROM washgo.delivery_tasks WHERE shipper_id='$fixtureShipper'; DELETE FROM washgo.shippers WHERE id='$fixtureShipper'; DELETE FROM washgo.users WHERE id='$fixtureUser';" | Out-Null
}
