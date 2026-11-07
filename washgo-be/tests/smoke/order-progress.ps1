param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: progressPercent and workflowSteps through a whole order, per service type, and for a cancelled order.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'; $shoeCare='b3333333-bbbb-3333-bbbb-333333333333'

function New-Order($ServiceId) {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$ServiceId;quantity=1})} $customer).data
}
function Progress($Id) { (Call-Api Get "/wash-order/$Id" $null $customer).data }
function Step($Order,$Status) { $Order.workflowSteps | Where-Object { $_.status -eq $Status } }
function Expect($Id,$Percent,$Current) {
    $p=Progress $Id
    $cur=@($p.workflowSteps | Where-Object { $_.isCurrent })
    if($p.progressPercent -ne $Percent -or $cur.Count -ne 1 -or $cur[0].status -ne $Current){throw "Progress of $Id wrong: $($p.progressPercent)% current=$($cur.status) (expected $Percent% / $Current)"}
    return $p
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken;$shipper=$sessions.Shipper.accessToken
    $shipperId=Sql "SELECT id FROM washgo.shippers WHERE user_id='$($sessions.Shipper.user.id)';"
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Progress Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id

    Write-Output '1. Standard wash: drying/folding apply, ironing does not; 10 steps'
    $o=New-Order $standardWash
    $p=Expect $o.id 10 1
    if(!(Step $p 5).isApplicable -or !(Step $p 7).isApplicable -or (Step $p 6).isApplicable -or (Step $p 13).isApplicable -or @($p.workflowSteps | Where-Object { $_.isApplicable }).Count -ne 10){throw 'Applicable steps wrong for a standard wash.'}
    if(!(Step $p 1).timestamp){throw 'Step timestamp missing.'}
    $row=(Call-Api Post '/wash-order/mine' @{page=1;take=50} $customer).list | Where-Object { $_.id -eq $o.id }
    if($row.progressPercent -ne 10 -or $row.workflowSteps.Count -lt 12){throw 'List progress missing.'}

    Write-Output '2. Through the flow: skipped processing steps stay applicable but not completed'
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Expect $o.id 20 2 | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $o.id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=2} $merchant | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $merchant | Out-Null
    Expect $o.id 40 4 | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $merchant | Out-Null
    $p=Expect $o.id 50 8
    if((Step $p 5).isCompleted -or !(Step $p 4).isCompleted){throw 'Skipped drying marked completed.'}
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    Expect $o.id 60 9 | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$shipperId"} $merchant | Out-Null
    Invoke-TestDelivery $o.id return $shipper | Out-Null
    Expect $o.id 70 10 | Out-Null
    Invoke-TestDelivery $o.id pickup $customer | Out-Null
    $done=Expect $o.id 100 12
    if(!(Step $done 12).isCompleted){throw 'Completed step not completed.'}

    Write-Output '3. Shoe care: drying applies, folding and ironing do not'
    $s=New-Order $shoeCare
    $ps=Progress $s.id
    if(!(Step $ps 5).isApplicable -or (Step $ps 6).isApplicable -or (Step $ps 7).isApplicable -or $ps.progressPercent -ne 11){throw "Shoe care steps wrong: $($ps.progressPercent)"}

    Write-Output '4. Cancelled: share at cancellation, later steps not applicable, cancelled step current'
    Call-Api Post "/wash-order/$($s.id)/cancel" @{reason='Progress smoke'} $customer | Out-Null
    $c=Progress $s.id
    $cur=@($c.workflowSteps | Where-Object { $_.isCurrent })
    if($c.progressPercent -ne 11 -or $cur.Count -ne 1 -or $cur[0].status -ne 99 -or (Step $c 2).isApplicable){throw 'Cancelled progress wrong.'}

    Write-Output 'PASS: progressPercent and workflowSteps on detail and list; applicable steps per service type (standard wash vs shoe care); percent rises through deposit, collect, washing, payment, return and pickup (100%); skipped processing steps stay applicable but not completed; cancelled orders keep their share with the cancelled step current.'
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
