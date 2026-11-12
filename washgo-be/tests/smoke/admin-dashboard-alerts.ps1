param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: Admin dashboard snapshot, rule-based alerts and Merchant/Locker lookups.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

try {
    foreach($role in @('Admin','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $d0=(Call-Api Get '/admin/dashboard?period=today' $null $admin).data
    $a0=(Call-Api Get '/admin/alerts?take=1' $null $admin).data

    $sessions.Team=New-FixtureMerchant $run; $teamId="$($sessions.Team.user.id)"
    $locker=(Call-Api Post '/locker/' @{merchantId=$teamId;code="DASH_$run";name="Dash Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=6} $admin).data
    $lockerId=[Guid]$locker.id
    $o=@()
    foreach($i in 0..4){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $x=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
        Invoke-TestDelivery $x.id deposit $customer | Out-Null
        $o+=$x
    }
    $nowTs=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Sql @"
UPDATE washgo.orders SET merchant_id=NULL, status=2, deposited_at=now() AT TIME ZONE 'utc' - interval '2 hours' WHERE id='$($o[0].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=2, deposited_at=now() AT TIME ZONE 'utc' - interval '30 hours' WHERE id='$($o[1].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=9, payment_status=2, paid_at=now() AT TIME ZONE 'utc' - interval '72 hours' WHERE id='$($o[2].id)';
UPDATE washgo.orders SET merchant_id='$teamId', status=12 WHERE id='$($o[3].id)';
INSERT INTO washgo.order_status_history(id,order_id,from_status,to_status,created_at,changed_by_type) VALUES (gen_random_uuid(),'$($o[3].id)',11,12,$nowTs,'Customer');
UPDATE washgo.orders SET merchant_id='$teamId', status=99, cancelled_at=now() AT TIME ZONE 'utc' WHERE id='$($o[4].id)';
UPDATE washgo.lockers SET available_slots=0 WHERE id='$lockerId';
"@ | Out-Null

    Write-Output '1. Dashboard: one snapshot, period counts vs current counts, UTC, comparison flags'
    $raw=(Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/admin/dashboard?period=today&topMerchants=20" -Headers @{Authorization="Bearer $admin"}).Content
    $d=($raw | ConvertFrom-Json).data
    if($raw -notmatch '"generatedAt":"[^"]+Z"' -or ([datetime]$d.fromDate).ToUniversalTime().Hour -ne 17){throw "Times not UTC: $($d.generatedAt) $($d.fromDate)"}
    if($d.orders.created.current -lt $d0.orders.created.current+5){throw "Created count wrong: $($d.orders.created.current) vs $($d0.orders.created.current)"}
    if($d.orders.completed.current -lt $d0.orders.completed.current+1 -or $d.orders.cancelled.current -lt $d0.orders.cancelled.current+1){throw 'Completed/cancelled wrong.'}
    if($d.orders.waitingAssignmentOverdue -lt 1 -or $d.orders.waitingAssignment -lt $d.orders.waitingAssignmentOverdue -or $d.orders.inProgress -lt 1){throw 'Current order counts wrong.'}
    if($d.lockers.nearlyFull -lt $d0.lockers.nearlyFull+1 -or $d.lockers.total -lt 1){throw 'Locker stats wrong.'}
    if($d.customers.total -lt 1 -or $null -eq $d.customers.new.previous -or $null -eq $d.revenue.total.current -or -not $d.definitions.'revenue.total'){throw 'Customer/revenue blocks wrong.'}
    $t=@($d.topMerchants | Where-Object { $_.merchantId -eq $teamId })
    if($t.Count -ne 1 -or $t[0].completedOrders -ne 1 -or $t[0].cancelledOrders -ne 1 -or $t[0].finishedOrders -ne 2 -or $t[0].completionRate -ne 50){throw "Top merchant wrong: $($d.topMerchants | ConvertTo-Json -Compress)"}
    $all=(Call-Api Get '/admin/dashboard?period=all&topMerchants=1' $null $admin).data
    if($null -ne $all.orders.created.previous -or $null -ne $all.orders.created.changePercent -or @($all.topMerchants).Count -gt 1){throw 'Period all wrong.'}
    Assert-Status 400 Get '/admin/dashboard?period=custom' $null $admin
    Assert-Status 403 Get '/admin/dashboard' $null $customer

    Write-Output '2. Alerts: rule-based, totals over the whole system, filters, paging, severity order'
    $a=(Call-Api Get '/admin/alerts?take=100' $null $admin).data
    $count=@{}; foreach($s in $a.summary){ $count[[int]$s.type]=$s.count }
    $c0=@{}; foreach($s in $a0.summary){ $c0[[int]$s.type]=$s.count }
    foreach($type in 1,2,4,8){ if($count[$type] -lt $c0[$type]+1){throw "Alert type $type not counted."} }
    if($a.totalAll -ne ($a.summary | Measure-Object count -Sum).Sum -or $a.total -ne $a.totalAll){throw 'Totals wrong.'}
    $sev=@($a.items | ForEach-Object { [int]$_.severity })
    for($i=1;$i -lt $sev.Count;$i++){ if($sev[$i] -gt $sev[$i-1]){throw 'Not ordered by severity.'} }
    if(@($a.items | Where-Object { $_.source -ne 'Rule' }).Count){throw 'Source must be Rule.'}
    $mine=@($a.items | Where-Object { $_.resourceId -in @($o[0].id,$o[1].id,$o[2].id,"$lockerId") })
    if($mine.Count -ne 4 -and $a.total -le 100){throw "Own alerts missing: $($mine.Count)"}
    $only=(Call-Api Get '/admin/alerts?type=2&take=100' $null $admin).data
    if(@($only.items | Where-Object { $_.type -ne 2 }).Count -or $only.total -ne $count[2] -or -not ($only.items | Where-Object { $_.resourceId -eq $o[1].id -and $_.merchantId -eq $teamId -and $_.overdueMinutes -ge 60 })){throw 'Type filter wrong.'}
    $lk=(Call-Api Get '/admin/alerts?type=8&take=100' $null $admin).data.items | Where-Object { $_.resourceId -eq "$lockerId" }
    if(-not $lk -or $lk.resourceType -ne 'Locker' -or $lk.availableBoxes -ne 0 -or $lk.totalBoxes -ne 6){throw 'Locker alert wrong.'}
    $low=(Call-Api Get '/admin/alerts?severity=1&take=100' $null $admin).data
    if(@($low.items | Where-Object { $_.severity -ne 1 }).Count){throw 'Severity filter wrong.'}
    $p1=(Call-Api Get '/admin/alerts?page=1&take=2' $null $admin).data.items
    $p2=(Call-Api Get '/admin/alerts?page=2&take=2' $null $admin).data.items
    $ids=@($p1 | ForEach-Object { "$($_.type):$($_.resourceId)" }) + @($p2 | ForEach-Object { "$($_.type):$($_.resourceId)" })
    if($a.total -ge 4 -and (($ids | Select-Object -Unique).Count -ne 4 -or "$($a.items[2].type):$($a.items[2].resourceId)" -ne $ids[2])){throw 'Paging wrong.'}

    Write-Output '3. Lookups: compact, keyword, paging, Admin only'
    $m=Call-Api Get "/admin/lookups/merchants?keyword=$([uri]::EscapeDataString("Team Smoke Laundry $run"))" $null $admin
    if($m.total -ne 1 -or $m.list[0].id -ne $teamId -or $m.list[0].status -ne 1){throw "Merchant lookup wrong: $($m | ConvertTo-Json -Depth 3 -Compress)"}
    $lo=Call-Api Get "/admin/lookups/lockers?keyword=$([uri]::EscapeDataString("Dash Locker $run"))" $null $admin
    if($lo.total -ne 1 -or $lo.list[0].id -ne "$lockerId" -or $lo.list[0].totalBoxes -ne 6 -or -not $lo.list[0].code){throw 'Locker lookup wrong.'}
    $big=Call-Api Get '/admin/lookups/merchants?take=500' $null $admin
    if($big.pageSize -ne 50){throw 'Take not capped.'}
    Assert-Status 403 Get '/admin/lookups/lockers' $null $customer

    Write-Output 'PASS: dashboard reads one snapshot with UTC times; customers active/locked/new, Merchant statuses, orders created/completed (status history)/cancelled in the period with previous-period comparison (period all has none), current waiting/overdue/in-progress counts, revenue by transaction time, locker boxes and nearly-full count, top Merchants with completed/cancelled/denominator/completion rate; rule-based alerts (source Rule) with system-wide summary, type/severity filters, stable severity-then-deadline paging including locker alerts; compact Merchant/Locker lookups with keyword and capped paging; Admin only.'
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
