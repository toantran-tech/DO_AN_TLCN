param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY (Mock payment): Merchant business report KPIs, comparison, series, top services and CSV export.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

function New-Order(){
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    $o
}
function Pay($o){
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$($x.id)"} $team | Out-Null
    Invoke-TestDelivery $o.id collect $xToken | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $team | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $team | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $team | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id
    $x=New-FixtureShipper $team "Report $run" $run
    $sessions.X=(Call-Api Post '/auth/login' @{email=$x.email;password=(Read-TempPassword $x.email)} $null).data; $xToken=$sessions.X.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$teamId";name="Report Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id

    # o1: paid and completed; o2: paid; o3: cancelled after deposit.
    $o1=New-Order; Pay $o1
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o1.id;shipperId="$($x.id)"} $team | Out-Null
    Invoke-TestDelivery $o1.id return $xToken | Out-Null
    Invoke-TestDelivery $o1.id pickup $customer | Out-Null
    $o2=New-Order; Pay $o2
    $o3=New-Order
    Call-Api Post "/wash-order/$($o3.id)/cancel" @{reason="Report test $run"} $customer | Out-Null

    Write-Output '1. KPIs match the stored orders and payments'
    $r=(Call-Api Get '/merchant-reports/overview?period=today' $null $team).data
    $k=$r.kpis
    $final=[decimal](Sql "SELECT SUM(final_amount) FROM washgo.orders WHERE id IN ('$($o1.id)','$($o2.id)');")
    $comm=[decimal](Sql "SELECT SUM(ROUND(final_amount*commission_rate_applied/100)) FROM washgo.orders WHERE id IN ('$($o1.id)','$($o2.id)');")
    if($k.grossLaundryRevenue.current -ne $final -or $k.refunds.current -ne 0 -or $k.netRevenue.current -ne $final){throw "Revenue wrong: $($k.grossLaundryRevenue.current) vs $final"}
    if($k.commission.current -ne $comm -or $k.merchantPayout.current -ne ($final-$comm) -or $k.ordersWithoutCommissionSnapshot -ne 0){throw "Commission wrong: $($k.commission.current) vs $comm"}
    if($k.ordersCreated.current -ne 3 -or $k.ordersCompleted.current -ne 1 -or $k.ordersCancelled.current -ne 1){throw "Order counts wrong: $($k.ordersCreated.current)/$($k.ordersCompleted.current)/$($k.ordersCancelled.current)"}
    if($k.onTimeRate.denominator -ne 2 -or $k.onTimeRate.numerator -ne 2 -or $k.onTimeRate.rate -ne 100){throw 'On-time rate wrong.'}
    if($k.grossLaundryRevenue.previous -ne 0 -or $null -ne $k.grossLaundryRevenue.changePercent -or !$k.grossLaundryRevenue.isNew){throw 'No-baseline change wrong.'}
    if(!$r.incomplete -or $r.timezone -ne 'Asia/Ho_Chi_Minh' -or $r.granularity -ne 'hour' -or !$r.definitions.netRevenue){throw 'Metadata wrong.'}

    Write-Output '2. Series and comparison ranges'
    if(@($r.series).Count -ne 24){throw "Expected 24 hourly buckets, got $(@($r.series).Count)."}
    $sum=(@($r.series) | Where-Object { $_.computed } | Measure-Object -Property netRevenue -Sum).Sum
    if([decimal]$sum -ne $final -or @($r.series | Where-Object { !$_.computed -and $_.netRevenue -ne 0 }).Count){throw 'Series does not add up.'}
    if(([datetime]$r.previous.toExclusive) -ne ([datetime]$r.current.from)){throw 'Previous range not adjacent.'}
    $m=(Call-Api Get '/merchant-reports/overview?period=custom&from=2026-10-01&to=2026-10-31' $null $team).data
    if($m.granularity -ne 'day' -or @($m.series).Count -ne 31 -or $m.incomplete -or $m.current.fromLocalDate -ne '2026-10-01' -or $m.current.toLocalDate -ne '2026-10-31' -or $m.previous.fromLocalDate -ne '2026-08-31'){throw 'Custom range wrong.'}

    Write-Output '3. Top services'
    $t=@($r.topServices)[0]
    if($t.rank -ne 1 -or $t.serviceId -ne $standardWash -or $t.orderCount -ne 2 -or $t.quantity -ne 2 -or !$t.name){throw "Top service wrong: $($t | ConvertTo-Json -Compress)"}

    Write-Output '4. Validation and scope'
    foreach($q in @('period=year','period=custom&from=2026-10-10&to=2026-10-01','period=custom&from=2025-01-01&to=2026-06-30','period=month&granularity=hour','period=custom&from=bad&to=2026-10-01','period=today&granularity=minute')){
        Assert-Status 400 Get "/merchant-reports/overview?$q" $null $team
    }
    Assert-Status 403 Get '/merchant-reports/overview?period=today' $null $customer
    Assert-Status 403 Get '/merchant-reports/overview?period=today' $null $admin

    Write-Output '5. CSV export'
    $f=Invoke-WebRequest -UseBasicParsing -Uri "$BaseUrl/merchant-reports/export?period=today" -Headers @{Authorization="Bearer $team"}
    $bytes=$f.RawContentStream.ToArray()
    $text=[Text.Encoding]::UTF8.GetString($bytes)
    if($f.Headers['Content-Type'] -notmatch 'text/csv' -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF -or $f.Headers['Content-Disposition'] -notmatch 'bao-cao-.*\.csv' -or $text -notmatch 'merchantPayout' -or $text -notmatch [regex]::Escape(($final).ToString('0.##',[Globalization.CultureInfo]::InvariantCulture))){throw 'CSV export wrong.'}

    Write-Output 'PASS: Merchant report for today matches stored payments and orders (gross, refunds, net, commission at each order locked rate, payout, created/completed/cancelled, on-time numerator/denominator), no-baseline change is null + isNew, incomplete period metadata, 24 hourly buckets that add up, adjacent previous range, custom month by day with same-length previous range, top service by distinct orders, invalid period/range/granularity/too many buckets rejected, Merchant-only, CSV with BOM, file name, KPI and definitions.'
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
