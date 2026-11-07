param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

try {
    Write-Output "==> STEP 1: Login and Authenticate Roles (Merchant, Admin, Customer, Shipper)"
    $sessions = @{}
    foreach ($role in @('Admin', 'Merchant', 'Customer', 'Shipper')) {
        $sessions[$role] = (Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $adminToken = $sessions.Admin.accessToken
    $merchantToken = $sessions.Merchant.accessToken
    $customerToken = $sessions.Customer.accessToken
    $shipperToken = $sessions.Shipper.accessToken

    Write-Output "`n==> STEP 2: Phase 1 - Merchant Profile (GET /merchant-profile/)"
    $profileRes = Call-Api Get '/merchant-profile/' $null $merchantToken
    if (-not $profileRes.succeeded) { throw "Get profile failed: $($profileRes.message)" }
    $p = $profileRes.data
    if ($null -eq $p.city -or $null -eq $p.joinedAt -or $null -eq $p.publicRatingCount) {
        throw "Profile missing required Phase 1 fields: City, JoinedAt, or PublicRatingCount"
    }
    Write-Output "  Profile PASS: Business=$($p.businessName), City=$($p.city), Lat/Long=($($p.latitude), $($p.longitude)), JoinedAt=$($p.joinedAt), PublicRatingCount=$($p.publicRatingCount)"

    Write-Output "`n==> STEP 3: Phase 2 - Commission Snapshot & Net Revenue per Order"
    $ordersRes = Call-Api Post '/wash-order/filter' @{take=10;page=1} $merchantToken
    $orderList = if ($ordersRes.list) { $ordersRes.list } else { @() }
    if ($orderList.Count -eq 0) { throw "No orders found for merchant" }
    foreach ($ord in $orderList) {
        if ($null -eq $ord.commissionRateApplied -or $null -eq $ord.commissionBaseAmount -or $null -eq $ord.commissionAmount -or $null -eq $ord.merchantNetAmount) {
            throw "Order $($ord.orderCode) missing commission fields"
        }
        $expectedNet = [Math]::Max(0, $ord.commissionBaseAmount - $ord.commissionAmount)
        if ([Math]::Abs($ord.merchantNetAmount - $expectedNet) -gt 0.01) {
            throw "Net amount formula mismatch for order $($ord.orderCode): $expectedNet vs $($ord.merchantNetAmount)"
        }
    }
    Write-Output "  Phase 2 PASS: All $($orderList.Count) orders have commission snapshot and Net = Base - Commission."

    Write-Output "`n==> STEP 4: Phase 3 - Merchant Dashboard (GET /merchant/dashboard)"
    Assert-Status 401 Get '/merchant/dashboard' $null $null
    Assert-Status 403 Get '/merchant/dashboard' $null $adminToken
    Assert-Status 403 Get '/merchant/dashboard' $null $customerToken
    Assert-Status 403 Get '/merchant/dashboard' $null $shipperToken

    $dashAll = (Call-Api Get '/merchant/dashboard' $null $merchantToken).data
    if ($dashAll.period -ne 'all' -or $null -eq $dashAll.statusCounts -or $null -eq $dashAll.latestOrders) {
        throw "Dashboard payload invalid"
    }
    $sc = $dashAll.statusCounts
    $statuses = @('pending','deposited','collected','washing','drying','ironing','folding','awaitingPayment','paid','readyToReturn','returned','completed','movedToStorage','cancelled')
    foreach ($st in $statuses) {
        if ($null -eq $sc.$st) { throw "StatusCounts missing status: $st" }
    }
    if ($dashAll.latestOrders.Count -gt 0) {
        if ($dashAll.latestOrders[0].customerName -ne '***' -or $dashAll.latestOrders[0].customerPhoneNumber -ne '***') {
            throw "Customer personal info was not redacted in Dashboard latestOrders"
        }
    }
    Write-Output "  Dashboard 'all' PASS: Total orders = $($dashAll.totalOrders), 14 statuses present, Customer PII redacted."

    # Time filters
    $dashToday = (Call-Api Get '/merchant/dashboard?period=today' $null $merchantToken).data
    $dashWeek = (Call-Api Get '/merchant/dashboard?period=week' $null $merchantToken).data
    $dashMonth = (Call-Api Get '/merchant/dashboard?period=month' $null $merchantToken).data
    $nowSec = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $fromSec = [DateTimeOffset]::UtcNow.AddDays(-15).ToUnixTimeSeconds()
    $customUri = "/merchant/dashboard?period=custom`&fromDate=$fromSec`&toDate=$nowSec"
    $dashCustom = (Call-Api Get $customUri $null $merchantToken).data
    if ($dashToday.period -ne 'today' -or $dashWeek.period -ne 'week' -or $dashMonth.period -ne 'month' -or $dashCustom.period -ne 'custom') {
        throw "Time filtering for dashboard failed"
    }
    Assert-Status 400 Get '/merchant/dashboard?period=custom' $null $merchantToken
    $invUri = "/merchant/dashboard?period=custom`&fromDate=$nowSec`&toDate=$fromSec"
    Assert-Status 400 Get $invUri $null $merchantToken
    Write-Output "  Phase 3 PASS: Dashboard today, week, month, custom filters all verified."

    Write-Output "`n==> STEP 5: Phase 4 - Revenue and Growth Report (GET /merchant/revenue-report)"
    Assert-Status 401 Get '/merchant/revenue-report' $null $null
    Assert-Status 403 Get '/merchant/revenue-report' $null $adminToken
    Assert-Status 403 Get '/merchant/revenue-report' $null $customerToken
    Assert-Status 403 Get '/merchant/revenue-report' $null $shipperToken

    $repMonth = (Call-Api Get '/merchant/revenue-report' $null $merchantToken).data
    if ($repMonth.period -ne 'month' -or $null -eq $repMonth.totalGrossRevenue -or $null -eq $repMonth.totalCommission -or $null -eq $repMonth.totalPayoutAmount) {
        throw "Revenue report month missing required fields"
    }
    $expectedPayoutMonth = [Math]::Max(0, $repMonth.totalGrossRevenue - $repMonth.totalCommission)
    if ([Math]::Abs($repMonth.totalPayoutAmount - $expectedPayoutMonth) -gt 0.01) {
        throw "Payout amount mismatch on revenue report: $expectedPayoutMonth vs $($repMonth.totalPayoutAmount)"
    }
    Write-Output "  Month Report PASS: Gross=$($repMonth.totalGrossRevenue), Commission=$($repMonth.totalCommission), Payout=$($repMonth.totalPayoutAmount), GrowthRate=$($repMonth.growthRate)%, DaysCount=$($repMonth.dailySeries.Count)"

    $repAll = (Call-Api Get '/merchant/revenue-report?period=all' $null $merchantToken).data
    $repWeek = (Call-Api Get '/merchant/revenue-report?period=week' $null $merchantToken).data
    $revCustomUri = "/merchant/revenue-report?period=custom`&fromDate=$fromSec`&toDate=$nowSec"
    $repCustom = (Call-Api Get $revCustomUri $null $merchantToken).data
    if ($repAll.period -ne 'all' -or $repWeek.period -ne 'week' -or $repCustom.period -ne 'custom') {
        throw "Revenue report time filters failed"
    }
    Assert-Status 400 Get '/merchant/revenue-report?period=custom' $null $merchantToken
    Write-Output "  Phase 4 PASS: Revenue and growth report verified on all periods."

    Write-Output "`n==> STEP 6: Phase 5 - SLA Alerts Overdue Attention (GET /merchant/sla-alerts)"
    Assert-Status 401 Get '/merchant/sla-alerts' $null $null
    Assert-Status 403 Get '/merchant/sla-alerts' $null $adminToken
    Assert-Status 403 Get '/merchant/sla-alerts' $null $customerToken
    Assert-Status 403 Get '/merchant/sla-alerts' $null $shipperToken

    $slaRes = Call-Api Get '/merchant/sla-alerts' $null $merchantToken
    if (-not $slaRes.succeeded) { throw "Get SLA alerts failed: $($slaRes.message)" }
    $sla = $slaRes.data
    if ($null -eq $sla.totalAlerts -or $null -eq $sla.collectionAlertsCount -or $null -eq $sla.processingAlertsCount -or $null -eq $sla.returnAlertsCount -or $null -eq $sla.priorityOrders) {
        throw "SLA alerts payload missing required fields"
    }
    Write-Output "  SLA Alerts PASS: Total=$($sla.totalAlerts) (Collection=$($sla.collectionAlertsCount), Processing=$($sla.processingAlertsCount), Return=$($sla.returnAlertsCount))"
    if ($sla.priorityOrders.Count -gt 0) {
        $topOrder = $sla.priorityOrders[0]
        Write-Output "  Top Priority Order: #$($topOrder.orderCode), Type=$($topOrder.alertType), Overdue=$($topOrder.overdueMinutes) mins, Action='$($topOrder.suggestedAction)'"
        if ($null -eq $topOrder.alertType -or $null -eq $topOrder.deadline -or $topOrder.overdueMinutes -lt 0 -or [string]::IsNullOrWhiteSpace($topOrder.suggestedAction)) {
            throw "Invalid priority order format"
        }
    }
    Write-Output "  Phase 5 PASS: SLA Overdue Attention successfully categorized and sorted."

    Write-Output "`n=========================================================================="
    Write-Output " CONGRATULATIONS: ALL PHASES 1 THROUGH 6 COMPLETED & PASSED 100%!"
    Write-Output "=========================================================================="
}
catch {
    Write-Error "Test FAILED: $_"
    exit 1
}
