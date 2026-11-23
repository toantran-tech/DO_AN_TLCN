param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: uses fixture Shippers, a locker and its orders.
Add-Type -AssemblyName System.Net.Http
$sessions=@{}; $run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$lockerId=$null
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{Authorization="Bearer $Token"};TimeoutSec=30}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $text=$_.ErrorDetails.Message
        if(!$text){ try { $text=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { } }
        $json=if($text){ try { $text | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Op(){ [Guid]::NewGuid().ToString() }
function Limit-Body($ShipperId,$Value){
    $current=(Call-Api Get "/shipper/$ShipperId" $null $merchant).data
    @{fullName=$current.fullName;phone=$current.phone;email=$current.email;vehicleType=$current.vehicleType;
      vehiclePlate=$current.vehiclePlate;isActive=$current.isActive;expectedRevision=$current.revision;operationId=(Op);maxActiveTasks=$Value}
}
function Set-Limit($ShipperId,$Value){
    Raw Put "/shipper/$ShipperId" (Limit-Body $ShipperId $Value) $merchant
}
function New-Shipper($Tag,$Limit){
    $body=@{fullName="Cap Shipper $Tag";phone="09$(Get-Random -Minimum 10000000 -Maximum 99999999)";email="cap-$run-$Tag@example.com";vehicleType=1;vehiclePlate='59-X1 123.45'}
    if($null -ne $Limit){ $body.maxActiveTasks=$Limit }
    (Call-Api Post '/shipper/' $body $merchant).data
}
function New-AssignableOrder(){
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null   # deposit, then the Admin assigns the locker's Merchant (status 16)
    $o.id
}
function Row($OrderId,$ShipperId,[switch]$All){
    $q=if($All){'?includeUnavailable=true'}else{''}
    @((Call-Api Get "/wash-order/$OrderId/available-shippers$q" $null $merchant).data) | Where-Object { $_.id -eq $ShipperId }
}
function Assign($OrderId,$ShipperId,$Status=16,$Key=(Op)){
    Raw Put '/wash-order/assign-shipper' @{orderId="$OrderId";shipperId="$ShipperId";operationId=$Key;expectedStatus=$Status} $merchant
}
function Open-Tasks($ShipperId){ [int](Sql "SELECT count(*) FROM washgo.orders o WHERE o.shipper_id='$ShipperId' AND NOT o.is_deleted AND ((o.status=17 AND o.assigned_task_type=1) OR (o.status=19 AND o.assigned_task_type=2) OR (o.status IN (10,14) AND o.assigned_task_type=3) OR (o.status IN (13,15) AND o.assigned_task_type=4));") }
function Send-Parallel($Requests){
    $client=[System.Net.Http.HttpClient]::new()
    try {
        $tasks=@(foreach($r in $Requests){
            $path=if($r.Path){$r.Path}else{'/wash-order/assign-shipper'}
            $body=if($r.Path){$r.Body}else{$r}
            $message=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new('PUT'), "$BaseUrl$path")
            $message.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$merchant)
            $message.Content=[System.Net.Http.StringContent]::new(($body | ConvertTo-Json),[Text.Encoding]::UTF8,'application/json')
            $client.SendAsync($message)
        })
        [System.Threading.Tasks.Task]::WaitAll([System.Threading.Tasks.Task[]]$tasks)
        @(foreach($t in $tasks){ [int]$t.Result.StatusCode })
    } finally { $client.Dispose() }
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken; $merchant=$sessions.Merchant.accessToken; $customer=$sessions.Customer.accessToken
    $retired=Raw Put '/system-setting/dispatch' @{maxActiveTasksPerShipper=2} $admin
    Check ($retired.Status -eq 410) 'Admin still controls a global limit.'
    $x=New-Shipper 'x' 2; $y=New-Shipper 'y'
    Check ($x.maxActiveTasks -eq 2 -and $y.maxActiveTasks -eq 3) 'Custom/default limits wrong.'
    $blocked=Raw Put "/shipper/$($x.id)" (Limit-Body $x.id 2) $admin
    Check ($blocked.Status -eq 403 -and $blocked.Json.meta.code -eq 'ShipperTaskLimitForbidden') 'Admin could change the limit.'
    foreach($invalid in @(0,-1,21,1.5,$null)){
        Check ((Set-Limit $x.id $invalid).Status -eq 400) "Invalid limit accepted: $invalid"
    }
    $locker=(Call-Api Post '/locker/' @{merchantIds=@("$($sessions.Merchant.user.id)");name="Cap Locker $run";address='Private development fixture';city='Test';latitude=10.77;longitude=106.69;boxCount=6} $admin).data
    $lockerId=[Guid]$locker.id
    $o1=New-AssignableOrder; $o2=New-AssignableOrder; $o3=New-AssignableOrder; $o4=New-AssignableOrder; $o5=New-AssignableOrder

    Write-Output '1. 0 and 1 open task: still listed and assignable, status stays Available'
    $r=Row $o1 $x.id
    Check ($r.activeTaskCount -eq 0 -and $r.maxActiveTasks -eq 2 -and $r.remainingTaskCapacity -eq 2 -and $r.canAssign -and $null -eq $r.blockedReasonCode) 'Row with 0 tasks wrong.'
    $k1=Op
    Check ((Assign $o1 $x.id 16 $k1).Status -eq 200) 'First task refused.'
    Check ((Call-Api Get "/shipper/$($x.id)" $null $merchant).data.status -eq 1) 'Operational status changed because of one task.'
    $r=Row $o2 $x.id
    Check ($r.activeTaskCount -eq 1 -and $r.remainingTaskCapacity -eq 1 -and $r.canAssign) 'Row with 1 task wrong.'
    Check ((Assign $o2 $x.id).Status -eq 200) 'Second task (under the limit) refused.'

    Write-Output '2. At the limit: hidden by default, listed with a reason on request, 409 with the latest numbers'
    Check ($null -eq (Row $o3 $x.id)) 'Full Shipper still offered by default.'
    $r=Row $o3 $x.id -All
    Check (!$r.canAssign -and $r.blockedReasonCode -eq 'ShipperCapacityExceeded' -and $r.activeTaskCount -eq 2 -and $r.remainingTaskCapacity -eq 0) 'Full row reason/numbers wrong.'
    $full=Assign $o3 $x.id
    Check ($full.Status -eq 409 -and $full.Json.meta.code -eq 'ShipperCapacityExceeded' -and $full.Json.meta.activeTaskCount -eq 2 -and $full.Json.meta.maxActiveTasks -eq 2 -and $full.Json.meta.remainingTaskCapacity -eq 0) "Full assignment: $($full.Status) $($full.Json.meta.code)"

    Write-Output '3. Replay and re-assigning the same trip to the same Shipper take no extra slot'
    Check ((Assign $o1 $x.id 16 $k1).Status -eq 200) 'Replay of the first assignment refused.'
    Check ((Assign $o1 $x.id 17).Status -eq 200) 'Same trip, same Shipper refused at the limit.'
    Check ((Open-Tasks $x.id) -eq 2) 'Replay/re-assignment changed the open task count.'

    Write-Output '4. Finishing a trip frees a slot although the order is still being washed; unassigning frees one too'
    Invoke-TestDelivery $o1 collect $admin | Out-Null
    Check ([int](Call-Api Get "/wash-order/$o1" $null $admin).data.status -eq 3) 'Collect did not complete.'
    Check ((Assign $o3 $x.id).Status -eq 200) 'Slot not freed by the finished trip.'
    Check ((Raw Post "/wash-order/$o2/unassign-shipper" @{reason='Capacity smoke';expectedStatus=17} $merchant).Status -eq 200) 'Unassign failed.'
    Check ((Row $o2 $x.id).activeTaskCount -eq 1) 'Unassign did not free the slot.'

    Write-Output '5. Storage tasks count with the same rule'
    Sql "UPDATE washgo.orders SET status=14, shipper_id='$($x.id)', assigned_task_type=3 WHERE id='$o4';" | Out-Null
    $r=Row $o5 $x.id -All
    Check ($r.activeTaskCount -eq 2 -and !$r.canAssign) "Storage transfer not counted: $($r.activeTaskCount)"
    Check ((Call-Api Get "/shipper/$($x.id)" $null $merchant).data.activeTaskCount -eq 2) 'Shipper page counts differently.'

    Write-Output '6. Two assignments racing for the last slot: exactly one wins'
    Check ((Set-Limit $x.id 3).Status -eq 200) 'Could not raise the limit.'
    $codes=Send-Parallel @(@{orderId="$o5";shipperId="$($x.id)";operationId=(Op);expectedStatus=16}, @{orderId="$o2";shipperId="$($x.id)";operationId=(Op);expectedStatus=16})
    Check ((@($codes | Where-Object {$_ -eq 200}).Count -eq 1) -and (@($codes | Where-Object {$_ -eq 409}).Count -eq 1)) "Race for the last slot: $($codes -join ',')"
    Check ((Open-Tasks $x.id) -eq 3) 'Race went over the limit.'
    $loser=if([int](Call-Api Get "/wash-order/$o5" $null $admin).data.status -eq 16){$o5}else{$o2}

    Write-Output '7. Lower than active count is rejected atomically, equal is valid and full'
    $body=Limit-Body $x.id 2
    $body.fullName='Must not change'
    $lower=Raw Put "/shipper/$($x.id)" $body $merchant
    Check ($lower.Status -eq 409 -and $lower.Json.meta.code -eq 'ShipperTaskLimitBelowActiveCount' -and
        $lower.Json.meta.minimumAllowedMaxActiveTasks -eq 3) 'Lowering below active count must fail.'
    $page=(Call-Api Get "/shipper/$($x.id)" $null $merchant).data
    Check ($page.fullName -eq $x.fullName -and $page.activeTaskCount -eq 3 -and $page.maxActiveTasks -eq 3) 'Failed update changed the profile.'
    $body=Limit-Body $x.id 3
    Check ((Raw Put "/shipper/$($x.id)" $body $merchant).Status -eq 200) 'Equal limit refused.'
    Check ((Raw Get "/shipper/$($x.id)/operations/$($body.operationId)" $null $merchant).Status -eq 200) 'Update lookup failed.'
    $edited=Raw Put "/shipper/$($x.id)" $body $merchant
    Check ($edited.Status -eq 200 -and $edited.Json.data.maxActiveTasks -eq 3) 'Replay returned the wrong result.'
    $body.maxActiveTasks=4
    $reused=Raw Put "/shipper/$($x.id)" $body $merchant
    Check ($reused.Status -eq 409 -and $reused.Json.meta.code -eq 'OperationIdReused') 'Changed payload reused an operation.'
    $body.operationId=Op
    $stale=Raw Put "/shipper/$($x.id)" $body $merchant
    Check ($stale.Status -eq 409 -and $stale.Json.meta.code -eq 'RevisionMismatch') 'Stale revision changed the limit.'
    Check ((Assign $loser $x.id).Status -eq 409) 'Extra assignment accepted at equal limit.'

    Write-Output '8. Lowering a limit races with assignment'
    Check ((Set-Limit $x.id 4).Status -eq 200) 'Could not raise limit to 4.'
    $codes=Send-Parallel @(@{Path="/shipper/$($x.id)";Body=(Limit-Body $x.id 3)}, @{orderId="$loser";shipperId="$($x.id)";operationId=(Op);expectedStatus=16})
    Check (($codes | Sort-Object) -join ',' -eq '200,409') "Limit/assignment race: $($codes -join ',')"
    $page=(Call-Api Get "/shipper/$($x.id)" $null $merchant).data
    Check ($page.activeTaskCount -eq $page.maxActiveTasks) 'Race left an inconsistent limit.'
    if($codes[1] -eq 200){
        Check ((Raw Post "/wash-order/$loser/unassign-shipper" @{reason='Capacity race cleanup';expectedStatus=17} $merchant).Status -eq 200) 'Unassign failed.'
    }

    Write-Output '9. Other refusals keep their own codes'
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$($y.id)';" | Out-Null
    Check ((Row $loser $y.id -All).blockedReasonCode -eq 'ShipperOnLeave') 'On-leave Shipper reason wrong.'
    $leave=Assign $loser $y.id
    Check ($leave.Status -eq 400 -and $leave.Json.meta.code -eq 'ShipperNotAvailable') "On-leave assignment: $($leave.Status) $($leave.Json.meta.code)"
    Sql "UPDATE washgo.shippers SET is_active=true, status=2 WHERE id='$($y.id)';" | Out-Null
    Check ((Row $loser $y.id -All).blockedReasonCode -eq 'ShipperNotAvailable') 'Busy Shipper reason wrong.'
    $other=Assign $loser ([Guid]::NewGuid())
    Check ($other.Status -eq 400 -and $other.Json.meta.code -eq 'ShipperNotInMerchant') "Unknown Shipper: $($other.Status) $($other.Json.meta.code)"

    Write-Output 'PASS: per-Shipper limits, authorization, concurrent changes, assignment and replay.'
}
finally {
    foreach($session in $sessions.Values){ try { Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null } catch { } }
    $fixtures="SELECT id FROM washgo.users WHERE email LIKE 'cap-$run-%'"
    $shipperIds="SELECT id FROM washgo.shippers WHERE user_id IN ($fixtures)"
    try { Sql @"
BEGIN;
$(if($lockerId){"UPDATE washgo.delivery_tasks SET status=4 WHERE status=1 AND order_id IN (SELECT id FROM washgo.orders WHERE locker_id='$lockerId');
UPDATE washgo.orders SET status=99, shipper_id=NULL, assigned_task_type=NULL WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';"})
UPDATE washgo.orders SET shipper_id=NULL WHERE shipper_id IN ($shipperIds);
DELETE FROM washgo.delivery_tasks WHERE shipper_id IN ($shipperIds);
DELETE FROM washgo.shipper_audits WHERE shipper_id IN ($shipperIds);
DELETE FROM washgo.shippers WHERE user_id IN ($fixtures);
DELETE FROM washgo.notification_deliveries WHERE user_id IN ($fixtures);
DELETE FROM washgo.notifications WHERE user_id IN ($fixtures);
DELETE FROM washgo.refresh_tokens WHERE user_id IN ($fixtures);
DELETE FROM washgo.users WHERE id IN ($fixtures);
COMMIT;
"@ | Out-Null } catch { Write-Warning "Capacity smoke cleanup failed: $($_.Exception.Message)" }
}
