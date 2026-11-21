param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: a Shipper takes several open tasks up to SystemSetting.MaxActiveTasksPerShipper. Fixture Shippers
# (cap-<run>-*@example.com), one locker and its orders are removed in finally; the original limit is restored.
Add-Type -AssemblyName System.Net.Http
$sessions=@{}; $run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$lockerId=$null; $originalLimit=$null
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
function Set-Limit($Value){
    $v=(Call-Api Get '/system-setting/' $null $admin).data.version
    Raw Put '/system-setting/dispatch' @{operationId=(Op);expectedVersion=$v;maxActiveTasksPerShipper=$Value} $admin
}
function New-Shipper($Tag){
    (Call-Api Post '/shipper/' @{fullName="Cap Shipper $Tag";phone="09$(Get-Random -Minimum 10000000 -Maximum 99999999)";email="cap-$run-$Tag@example.com";vehicleType=1;vehiclePlate='59-X1 123.45'} $merchant).data
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
            $message=[System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new('PUT'), "$BaseUrl/wash-order/assign-shipper")
            $message.Headers.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$merchant)
            $message.Content=[System.Net.Http.StringContent]::new(($r | ConvertTo-Json),[Text.Encoding]::UTF8,'application/json')
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
    $originalLimit=(Call-Api Get '/system-setting/' $null $admin).data.maxActiveTasksPerShipper
    Check ($originalLimit -ge 1) "Limit must be a positive server value, got $originalLimit."
    $bad=Set-Limit 0
    Check ($bad.Status -eq 400 -and $null -ne $bad.Json.errors.maxActiveTasksPerShipper) "Limit 0 accepted: $($bad.Status)"
    Check ((Set-Limit 2).Status -eq 200) 'Could not set the limit to 2.'
    $x=New-Shipper 'x'; $y=New-Shipper 'y'
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
    Check ((Set-Limit 3).Status -eq 200) 'Could not raise the limit.'
    $codes=Send-Parallel @(@{orderId="$o5";shipperId="$($x.id)";operationId=(Op);expectedStatus=16}, @{orderId="$o2";shipperId="$($x.id)";operationId=(Op);expectedStatus=16})
    Check ((@($codes | Where-Object {$_ -eq 200}).Count -eq 1) -and (@($codes | Where-Object {$_ -eq 409}).Count -eq 1)) "Race for the last slot: $($codes -join ',')"
    Check ((Open-Tasks $x.id) -eq 3) 'Race went over the limit.'
    $loser=if([int](Call-Api Get "/wash-order/$o5" $null $admin).data.status -eq 16){$o5}else{$o2}

    Write-Output '7. Lowering the limit below the current count cancels nothing and only blocks new tasks'
    Check ((Set-Limit 1).Status -eq 200) 'Could not lower the limit.'
    $page=(Call-Api Get "/shipper/$($x.id)" $null $merchant).data
    Check ($page.activeTaskCount -eq 3 -and $page.maxActiveTasks -eq 1 -and $page.remainingTaskCapacity -eq 0) 'Shipper page numbers wrong after lowering.'
    Check ((Open-Tasks $x.id) -eq 3) 'Lowering the limit cancelled tasks.'
    Check ((Assign $loser $x.id).Status -eq 409) 'Assignment above a lowered limit accepted.'

    Write-Output '8. Other refusals keep their own codes (not capacity)'
    Check ((Set-Limit 3).Status -eq 200) 'Could not reset the limit.'
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$($y.id)';" | Out-Null
    Check ((Row $loser $y.id -All).blockedReasonCode -eq 'ShipperOnLeave') 'On-leave Shipper reason wrong.'
    $leave=Assign $loser $y.id
    Check ($leave.Status -eq 400 -and $leave.Json.meta.code -eq 'ShipperNotAvailable') "On-leave assignment: $($leave.Status) $($leave.Json.meta.code)"
    Sql "UPDATE washgo.shippers SET is_active=true, status=2 WHERE id='$($y.id)';" | Out-Null
    Check ((Row $loser $y.id -All).blockedReasonCode -eq 'ShipperNotAvailable') 'Busy Shipper reason wrong.'
    $other=Assign $loser ([Guid]::NewGuid())
    Check ($other.Status -eq 400 -and $other.Json.meta.code -eq 'ShipperNotInMerchant') "Unknown Shipper: $($other.Status) $($other.Json.meta.code)"

    Write-Output 'PASS: open tasks up to MaxActiveTasksPerShipper (0/1/under/at the limit), status unchanged, full Shipper hidden by default and explained with includeUnavailable, 409 ShipperCapacityExceeded with numbers, replay/same trip take no slot, finished trip and unassign free a slot, storage tasks counted, one winner for the last slot, lowering the limit cancels nothing, other refusals keep their codes.'
}
finally {
    # Restore the limit while the Admin session is still valid, then sign out.
    try { if($originalLimit){ Check ((Set-Limit $originalLimit).Status -eq 200) 'restore' } } catch { Write-Warning 'Could not restore the Shipper task limit.' }
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
