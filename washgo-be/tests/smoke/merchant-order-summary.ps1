param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: order summary with the list filters, date range validation, Merchant code search, payment deadline.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Summary Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id
    $ids=@()
    foreach($i in 0..2){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $ids+=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data.id
    }
    Invoke-TestDelivery $ids[0] deposit $customer | Out-Null
    Invoke-TestDelivery $ids[1] deposit $customer | Out-Null
    # Run-scoped Merchant codes; the third order stays Pending with the same code prefix but no Merchant.
    Sql @"
UPDATE washgo.orders SET merchant_order_code='SUM$run'||'A' WHERE id='$($ids[0])';
UPDATE washgo.orders SET merchant_order_code='SUM$run'||'B', status=4 WHERE id='$($ids[1])';
UPDATE washgo.orders SET merchant_order_code='SUM$run'||'C' WHERE id='$($ids[2])';
"@ | Out-Null
    $kw="sum$run"

    Write-Output '1. Summary = list filters, one snapshot, status/page ignored'
    $s=(Call-Api Post '/wash-order/summary' @{keyword=$kw;status=4;page=2;take=1} $merchant).data
    if($s.total -ne 2 -or $s.statusCounts.deposited -ne 1 -or $s.statusCounts.washing -ne 1 -or $s.statusCounts.pending -ne 0 -or !$s.generatedAt){throw "Merchant summary wrong: $($s | ConvertTo-Json -Depth 4 -Compress)"}
    if($s.statusCounts.byStatus.Deposited -ne 1){throw 'byStatus missing.'}
    $list=Call-Api Post '/wash-order/filter' @{keyword=$kw;page=1;take=10} $merchant
    if($list.total -ne 2){throw "Merchant code search wrong: $($list.total)"}
    $a=(Call-Api Post '/wash-order/summary' @{keyword=$kw} $admin).data
    if($a.total -ne 3 -or $a.statusCounts.pending -ne 1){throw "Admin summary wrong: $($a.total)"}
    $p=(Call-Api Post '/wash-order/summary' @{keyword=$kw;paymentStatus=2} $merchant).data
    if($p.total -ne 0){throw 'Payment filter ignored.'}
    Assert-Status 403 Post '/wash-order/summary' @{keyword=$kw} $customer

    Write-Output '2. Date range on creation time, inclusive, validated'
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Sql "UPDATE washgo.orders SET created_on=$($now-3*86400) WHERE id='$($ids[0])';" | Out-Null
    $d=(Call-Api Post '/wash-order/summary' @{keyword=$kw;fromDate=$now-86400} $merchant).data
    if($d.total -ne 1 -or $d.statusCounts.washing -ne 1 -or $d.fromDate -ne $now-86400){throw 'fromDate not applied.'}
    $created=[long](Sql "SELECT created_on FROM washgo.orders WHERE id='$($ids[0])';")
    $e=(Call-Api Post '/wash-order/summary' @{keyword=$kw;fromDate=$created;toDate=$created} $merchant).data
    if($e.total -ne 1 -or $e.statusCounts.deposited -ne 1){throw 'Bounds not inclusive.'}
    if((Call-Api Post '/wash-order/filter' @{keyword=$kw;toDate=$now-86400;page=1;take=10} $merchant).total -ne 1){throw 'toDate not applied to the list.'}
    foreach($bad in @(@{fromDate=$now;toDate=$now-60}, @{fromDate=-1}, @{toDate=-5}, @{toDate=999999999999})){
        $body=@{keyword=$kw}+$bad
        Assert-Status 400 Post '/wash-order/summary' $body $merchant
        Assert-Status 400 Post '/wash-order/filter' ($body+@{page=1;take=10}) $merchant
    }

    Write-Output '3. Payment deadline and overdue types'
    Sql "UPDATE washgo.orders SET status=8, payment_status=1, payment_deadline=now() AT TIME ZONE 'utc' - interval '1 hour' WHERE id='$($ids[1])';" | Out-Null
    $o=(Call-Api Get "/wash-order/$($ids[1])" $null $merchant).data
    if(!$o.paymentDeadline -or !$o.isPaymentOverdue -or $o.paymentSecondsLeft -ne 0 -or (@($o.overdueTypes) -join ',') -ne 'PaymentOverdue'){throw "Overdue payment wrong: $($o.paymentDeadline) $($o.isPaymentOverdue) $($o.overdueTypes)"}
    Sql "UPDATE washgo.orders SET payment_deadline=now() AT TIME ZONE 'utc' + interval '1 hour' WHERE id='$($ids[1])';" | Out-Null
    $o=(Call-Api Post '/wash-order/filter' @{keyword="$kw"+'b';page=1;take=10} $merchant).list[0]
    if($o.isPaymentOverdue -or $o.paymentSecondsLeft -lt 3500 -or $o.paymentSecondsLeft -gt 3600 -or @($o.overdueTypes).Count -ne 0){throw "Payment countdown wrong: $($o.paymentSecondsLeft)"}
    $c=(Call-Api Get "/wash-order/$($ids[0])" $null $merchant).data
    if($null -ne $c.paymentSecondsLeft -or $c.isPaymentOverdue -or $c.isPickupOverdue){throw 'Deadline fields set on a Deposited order.'}

    Write-Output 'PASS: summary uses the list filters (keyword incl. Merchant code, payment, dates) in one snapshot and ignores status/page; Merchant/Admin scope; Customer denied; inclusive creation-date bounds; reversed/negative/out-of-range dates rejected on summary and list; payment deadline, countdown and overdue types.'
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
