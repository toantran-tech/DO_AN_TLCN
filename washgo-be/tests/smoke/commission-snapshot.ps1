param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY (Mock payment): locked commission per order (0% valid, missing snapshot flagged, no fallback to the
# current rate), order finance fields, Admin commissions, old Merchant revenue report and settlement breakdown.
$sessions=@{}; $teamId=$null; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function New-PaidOrder(){
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $o=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$o.id;shipperId="$($x.id)"} $team | Out-Null
    Invoke-TestDelivery $o.id collect $xToken | Out-Null
    Call-Api Put "/wash-order/$($o.id)/actual-weight" @{weightKg=6} $team | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=4} $team | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$o.id;newStatus=8} $team | Out-Null
    $pay=(Call-Api Post '/payment/create-url' @{orderId=$o.id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$o.id;transactionReference=$pay.transactionReference;success=$true} $admin | Out-Null
    (Call-Api Get "/wash-order/$($o.id)" $null $team).data
}

try {
    foreach($role in @('Admin','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId="$($sessions.Team.user.id)"
    $x=New-FixtureShipper $team "Comm $run" $run
    $sessions.X=(Call-Api Post '/auth/login' @{email=$x.email;password=(Read-TempPassword $x.email)} $null).data; $xToken=$sessions.X.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId=$teamId;name="Comm Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerId=[Guid]$locker.id
    $start=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-5

    Write-Output '1. 0% is a valid locked rate; later rate change does not touch old orders'
    Sql "UPDATE washgo.merchants SET commission_rate=0 WHERE user_id='$teamId';" | Out-Null
    $zero=New-PaidOrder
    if($zero.commissionStatus -ne 'Locked' -or $zero.commissionRateApplied -ne 0 -or $zero.commissionAmount -ne 0 -or !$zero.commissionLockedAt){throw "0% order wrong: $($zero.commissionStatus) $($zero.commissionAmount)"}
    if($zero.collectedLaundryAmount -ne $zero.finalAmount -or $zero.commissionOnCollected -ne 0 -or $zero.financialStatus -ne 'Collected'){throw 'Order finance fields wrong.'}
    Sql "UPDATE washgo.merchants SET commission_rate=15 WHERE user_id='$teamId';" | Out-Null
    $fifteen=New-PaidOrder
    Sql "UPDATE washgo.merchants SET commission_rate=25 WHERE user_id='$teamId';" | Out-Null
    if((Call-Api Get "/wash-order/$($fifteen.id)" $null $team).data.commissionRateApplied -ne 15){throw 'Rate change rewrote an old order.'}
    $missing=New-PaidOrder
    Sql "UPDATE washgo.orders SET commission_rate_applied=NULL, commission_locked_at=NULL WHERE id='$($missing.id)';" | Out-Null
    $m=(Call-Api Get "/wash-order/$($missing.id)" $null $team).data
    if($m.commissionStatus -ne 'Missing' -or $null -ne $m.commissionAmount -or $null -ne $m.commissionOnCollected -or $null -ne $m.merchantNetAmount){throw 'Missing snapshot not flagged.'}

    $c15=[decimal](Sql "SELECT ROUND(final_amount*15/100) FROM washgo.orders WHERE id='$($fifteen.id)';")
    $gross=[decimal](Sql "SELECT SUM(final_amount) FROM washgo.orders WHERE id IN ('$($zero.id)','$($fifteen.id)','$($missing.id)');")
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+5

    Write-Output '2. Admin commissions: missing snapshot counted, never priced at the current 25%'
    $row=@((Call-Api Post '/finance/commissions' @{fromDate=$start;toDate=$now;merchantId=$teamId} $admin).data)[0]
    if($row.commissionAmount -ne $c15 -or $row.ordersWithoutCommissionSnapshot -ne 1 -or $row.commissionComplete -or $row.uncoveredNetAmount -ne $m.finalAmount){throw "Commissions wrong: $($row | ConvertTo-Json -Compress)"}

    Write-Output '3. Old Merchant revenue report uses the same definitions'
    $r=(Call-Api Get '/merchant/revenue-report?period=today' $null $team).data
    if($r.totalGrossRevenue -ne $gross -or $r.totalCommission -ne $c15 -or $r.totalPayoutAmount -ne ($gross-$c15) -or $r.commissionComplete -or $r.ordersWithoutCommissionSnapshot -ne 1 -or $null -ne $r.growthRate){throw "Old report wrong: $($r | ConvertTo-Json -Depth 2 -Compress)"}
    $o=(Call-Api Get '/merchant-reports/overview?period=today' $null $team).data.kpis
    if($o.commission.current -ne $c15 -or $o.ordersWithoutCommissionSnapshot -ne 1){throw 'Overview not aligned.'}
    $full=(Call-Api Get '/merchant-reports/overview?period=today' $null $team).data.kpis
    if($full.commissionCoverage.currentComplete -or $full.commissionCoverage.currentUncoveredOrders -ne 1 -or !$full.merchantPayout.isEstimated -or $null -ne $full.merchantPayout.changePercent){throw 'Estimated payout not flagged.'}
    $life=(Call-Api Get '/merchant-reports/lifetime' $null $team).data
    if($life.commissionComplete -or $life.ordersWithoutCommissionSnapshot -ne 1 -or $life.commission -ne $c15){throw 'Lifetime coverage wrong.'}

    Write-Output '4. Settlement breakdown per order'
    Start-Sleep -Seconds 2
    $end=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-1
    $s=(Call-Api Post '/finance/settlements' @{merchantId=$teamId;fromDate=$start;toDate=$end} $admin).data
    $b=(Call-Api Get "/finance/settlements/$($s.id)/orders" $null $team).data
    if(@($b.lines).Count -ne 3 -or $b.ordersWithoutCommissionSnapshot -ne 1 -or $b.settledCommissionAmount -ne $c15){throw "Breakdown wrong: $($b | ConvertTo-Json -Depth 3 -Compress)"}
    $line=@($b.lines | Where-Object { $_.orderId -eq $missing.id })[0]
    if($null -ne $line.commissionRate -or $null -ne $line.commission -or $line.collected -ne $m.finalAmount){throw 'Missing line wrong.'}
    if(@($b.lines | Where-Object { $_.orderId -eq $zero.id })[0].commission -ne 0){throw '0% line wrong.'}
    Assert-Status 403 Get "/finance/settlements/$($s.id)/orders" $null $customer

    Write-Output 'PASS: 0% locked rate kept as 0 (not missing); rate change does not rewrite old orders; missing snapshot shows Missing with null commission; order DTO carries collected/refunded/commission on collected/financial status; Admin commissions and both Merchant reports exclude missing snapshots from commission, count them and never use the current rate; growth undefined when incomplete; settlement breakdown lists each order with its locked rate.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1,is_deleted=true WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=0,status=3,is_deleted=true WHERE id='$lockerId';
UPDATE washgo.merchant_settlements SET is_deleted=true WHERE merchant_id='$teamId';
COMMIT;
"@ | Out-Null
    }
    Remove-FixtureMerchant $teamId
}
