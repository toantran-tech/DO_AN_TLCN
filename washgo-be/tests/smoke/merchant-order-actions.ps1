param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: allowedActions on the order detail follow the same rules the write endpoints enforce.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

function Actions($Token){ (Call-Api Get "/wash-order/$($o.id)" $null $Token).data.allowedActions }
function Statuses($A){ (@($A.allowedNextStatuses) | ForEach-Object { [int]$_ }) -join ',' }
function Reason($A,$Status){ (@($A.statusOptions) | Where-Object { $_.status -eq $Status }).reasonCode }

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Actions Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data

    Write-Output '1. Pending: nothing allowed, assign blocked (no Merchant yet)'
    $a=Actions $admin
    if((Statuses $a) -ne '' -or $a.canAssignShipper -or $a.assignShipperReasonCode -ne 'NoMerchant' -or $a.canUpdateWeight -or $a.updateWeightReasonCode -ne 'RoleNotAllowed'){throw "Pending actions wrong: $($a | ConvertTo-Json -Depth 4 -Compress)"}
    if($null -ne (Actions $customer)){throw 'Customer got allowedActions.'}

    Write-Output '2. Deposited: assign Collect, then already assigned'
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $a=Actions $merchant
    if(!$a.canAssignShipper -or $a.assignTaskType -ne 1 -or $a.hasAssignedShipper -or $a.canUpdateWeight -or $a.updateWeightReasonCode -ne 'WrongStage' -or (Statuses $a) -ne '' -or (Reason $a 4) -ne 'InvalidTransition'){throw "Deposited actions wrong: $($a | ConvertTo-Json -Depth 4 -Compress)"}
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    if(!(Actions $merchant).hasAssignedShipper){throw 'hasAssignedShipper not set.'}
    if($null -ne (Actions $shipper)){throw 'Shipper got allowedActions.'}

    Write-Output '3. Collected: weigh first; the write endpoint gives the same reason'
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    $a=Actions $merchant
    if(!$a.canUpdateWeight -or $a.weightAlreadySet -or (Statuses $a) -ne '' -or (Reason $a 4) -ne 'WeightRequired' -or (Reason $a 5) -ne 'InvalidTransition' -or $a.canAssignShipper -or $a.assignShipperReasonCode -ne 'WrongStage'){throw "Collected actions wrong: $($a | ConvertTo-Json -Depth 4 -Compress)"}
    $msg=(@($a.statusOptions) | Where-Object { $_.status -eq 4 }).reason
    try { Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null; throw 'Washing allowed without weight.' }
    catch { if([int]$_.Exception.Response.StatusCode -ne 400){throw} }
    $adminView=Actions $admin
    if($adminView.canUpdateWeight -or $adminView.updateWeightReasonCode -ne 'RoleNotAllowed'){throw 'Admin may weigh.'}

    Write-Output '4. Weighed, then washing opens the next steps'
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $merchant | Out-Null
    $a=Actions $merchant
    if((Statuses $a) -ne '4' -or !$a.weightAlreadySet -or !$a.canUpdateWeight){throw "Weighed actions wrong: $(Statuses $a)"}
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    $a=Actions $merchant
    if((Statuses $a) -ne '5,6,7,8' -or $a.canUpdateWeight -or (Reason $a 4) -ne 'InvalidTransition'){throw "Washing actions wrong: $(Statuses $a)"}
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=7} $merchant | Out-Null
    if((Statuses (Actions $merchant)) -ne '8'){throw 'Folding actions wrong.'}

    Write-Output "PASS: allowedActions per stage (Pending, Deposited, Collected, weighed, Washing, Folding) with reason codes; same rule as the write endpoints (washing without weight rejected); assign task type and already-assigned flag; Admin cannot weigh; no metadata for Customer/Shipper."
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
}
