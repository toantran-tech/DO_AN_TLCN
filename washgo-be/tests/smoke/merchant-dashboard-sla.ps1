param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Merchant SLA alerts counted in the database with a paged priority list, and UTC times on dashboard/SLA.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

try {
    foreach($role in @('Admin','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId="$($sessions.Team.user.id)"
    $locker=(Call-Api Post '/locker/' @{merchantId=$teamId;code="SLA_$run";name="Sla Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerId=[Guid]$locker.id
    $o=@()
    foreach($i in 0..3){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $x=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $x.id deposit $customer | Out-Null
        $o+=$x
    }
    Sql @"
UPDATE washgo.orders SET merchant_id='$teamId', status=2, deposited_at=now() AT TIME ZONE 'utc' - interval '30 hours' WHERE id='$($o[0].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=4, estimated_finish_at=now() AT TIME ZONE 'utc' - interval '1 hour' WHERE id='$($o[1].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=9, payment_status=2, paid_at=now() AT TIME ZONE 'utc' - interval '72 hours' WHERE id='$($o[2].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=2, deposited_at=now() AT TIME ZONE 'utc' - interval '5 minutes' WHERE id='$($o[3].id)';
"@ | Out-Null

    Write-Output '1. Counts over the whole scope, priority list paged in SQL'
    $p1=(Call-Api Get '/merchant/sla-alerts?page=1&take=2' $null $team).data
    $p2=(Call-Api Get '/merchant/sla-alerts?page=2&take=2' $null $team).data
    if($p1.collectionAlertsCount -ne 1 -or $p1.processingAlertsCount -ne 1 -or $p1.returnAlertsCount -ne 1 -or $p1.totalAlerts -ne 3 -or $p1.priorityTotal -ne 3){throw "Counts wrong: $($p1 | ConvertTo-Json -Depth 2 -Compress)"}
    $ids=@($p1.priorityOrders | ForEach-Object { $_.orderId }) + @($p2.priorityOrders | ForEach-Object { $_.orderId })
    if(@($p1.priorityOrders).Count -ne 2 -or @($p2.priorityOrders).Count -ne 1 -or ($ids | Select-Object -Unique).Count -ne 3 -or $ids -contains $o[3].id){throw 'Paging wrong.'}
    if($p1.priorityOrders[0].orderId -ne $o[2].id -or $p1.priorityOrders[0].overdueMinutes -lt $p1.priorityOrders[1].overdueMinutes){throw 'Not ordered by longest overdue first.'}
    $only=(Call-Api Get '/merchant/sla-alerts?alertType=1' $null $team).data
    if($only.priorityTotal -ne 1 -or $only.priorityOrders[0].orderId -ne $o[0].id -or $only.totalAlerts -ne 3){throw 'alertType filter wrong.'}

    Write-Output '2. UTC times (no hidden +7h)'
    $raw=(Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/merchant/sla-alerts?take=1" -Headers @{Authorization="Bearer $team"}).Content
    $server=[datetime]::Parse(($raw | ConvertFrom-Json).data.serverNow).ToUniversalTime()
    if([math]::Abs(($server-[datetime]::UtcNow).TotalMinutes) -gt 3 -or $raw -notmatch '"serverNow":"[^"]+Z"'){throw "serverNow not UTC: $server"}
    $d=(Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/merchant/dashboard?period=today" -Headers @{Authorization="Bearer $team"}).Content | ConvertFrom-Json
    $gen=[datetime]::Parse($d.data.generatedAt).ToUniversalTime()
    $from=[datetime]::Parse($d.data.fromDate).ToUniversalTime()
    if([math]::Abs(($gen-[datetime]::UtcNow).TotalMinutes) -gt 3){throw "generatedAt not UTC: $gen"}
    if($from.Hour -ne 17 -or $from.Minute -ne 0){throw "fromDate is not Vietnam midnight in UTC: $from"}
    if($d.data.totalOrders -ne 4){throw "Dashboard total wrong: $($d.data.totalOrders)"}

    Write-Output 'PASS: SLA alert counts computed over the whole Merchant scope in SQL; priority list paged (page/take) longest overdue first with a stable order and an alertType filter, non-overdue orders excluded; serverNow/generatedAt/deadlines are UTC with Z (dashboard today starts at 17:00Z = 00:00 Vietnam); dashboard reads one snapshot.'
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
    Remove-FixtureMerchant $teamId
}
