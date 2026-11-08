param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: a private Merchant B (commission 25%) with its own locker, service and Shipper,
# so revenue and commission figures for the test period come only from orders created here.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$merchantB=[Guid]::NewGuid(); $serviceId=[Guid]::NewGuid(); $shipperUser=[Guid]::NewGuid(); $shipperB=[Guid]::NewGuid()

function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2
        items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
}
function Pay($Id,$Url='/payment/create-url') {
    $p=(Call-Api Post $Url @{orderId=$Id;paymentMethod=2} $customer).data
    Call-Api Post '/payment/callback' @{orderId=$Id;transactionReference=$p.transactionReference;success=$true} $admin | Out-Null
}
function Complete-Order {
    $id=(New-Order).id
    Invoke-TestDelivery $id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$shipperB"} $merchantTok | Out-Null
    Invoke-TestDelivery $id collect $shipper | Out-Null
    Call-Api Put "/wash-order/$id/actual-weight" @{weightKg=2} $merchantTok | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=4} $merchantTok | Out-Null
    Call-Api Put '/wash-order/status' @{orderId=$id;newStatus=8} $merchantTok | Out-Null
    Pay $id
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$id;shipperId="$shipperB"} $merchantTok | Out-Null
    Invoke-TestDelivery $id return $shipper | Out-Null
    Invoke-TestDelivery $id pickup $customer | Out-Null
    return $id
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$seedMerchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$merchantB','email','finance-$run@example.com','full_name','Finance Smoke Merchant','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedMerchantId';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object(
'user_id','$merchantB','business_name','Finance Smoke Laundry','tax_code',NULL,'status',1,'commission_rate',25))).* FROM washgo.merchants m WHERE user_id='$seedMerchantId';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$serviceId','merchant_id','$merchantB','code','FIN_KG_$run','name','Finance Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$seedMerchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$shipperUser','email','finance-shipper-$run@example.com','full_name','Finance Smoke Shipper','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$shipperB','user_id','$shipperUser','merchant_id','$merchantB','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="finance-$run@example.com";password='Merchant@123'} $null).data;$merchantTok=$sessions.B.accessToken
    $sessions.S=(Call-Api Post '/auth/login' @{email="finance-shipper-$run@example.com";password='Shipper@123'} $null).data;$shipper=$sessions.S.accessToken
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantB";code="FN_$run";name='Finance Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69
        boxes=@(@{boxNumber='1';size=2},@{boxNumber='2';size=2},@{boxNumber='3';size=2})
    } $admin).data;$lockerId=[Guid]$locker.id
    $from=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-5

    Write-Output '1. Activity: two paid orders, one fully refunded, one cancelled with a 30% fee'
    $o1=Complete-Order
    $o3=Complete-Order
    $o2=(New-Order).id
    Invoke-TestDelivery $o2 deposit $customer | Out-Null
    Call-Api Post "/wash-order/$o2/cancel" @{reason='Finance smoke cancel'} $customer | Out-Null
    Pay $o2 '/payment/cancellation-fee/create-url'
    $refund=(Call-Api Post '/payment/refund-requests' @{orderId=$o3;reason='Finance smoke refund'} $customer).data
    $approved=(Call-Api Post "/payment/refund-requests/$($refund.id)/review" @{approve=$true;reviewNote='ok';expectedVersion=$refund.version} $admin).data
    Call-Api Post "/payment/refund-requests/$($refund.id)/confirm-refunded" @{refundReference="RF-$run";expectedVersion=$approved.version} $admin | Out-Null
    $to=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+1

    Write-Output '2. Revenue report (UC 3.8.1)'
    Assert-Status 403 Post '/finance/revenue-report' @{} $customer
    Assert-Status 403 Post '/finance/revenue-report' @{} $seedMerchant
    Assert-Status 400 Post '/finance/revenue-report' @{fromDate=$to;toDate=$from} $admin
    $r=(Call-Api Post '/finance/revenue-report' @{fromDate=$from;toDate=$to;merchantId="$merchantB"} $admin).data
    if($r.laundryRevenue -ne 200000 -or $r.cancellationFeeRevenue -ne 30000 -or $r.refundedAmount -ne 100000 -or
       $r.totalRevenue -ne 130000 -or $r.paidOrderCount -ne 2){throw "Report totals wrong: $($r | ConvertTo-Json -Compress -Depth 2)"}
    if($r.byMerchant.Count -ne 1 -or $r.byMerchant[0].revenue -ne 130000 -or $r.byMerchant[0].merchantName -ne 'Finance Smoke Laundry'){throw 'Revenue by Merchant wrong.'}
    if($r.byService.Count -ne 1 -or $r.byService[0].amount -ne 200000 -or $r.byService[0].quantity -ne 2){throw 'Revenue by service wrong.'}
    if(($r.daily | Measure-Object revenue -Sum).Sum -ne 130000){throw 'Daily series does not add up.'}
    $empty=Call-Api Post '/finance/revenue-report' @{fromDate=1577836800;toDate=1577923200;merchantId="$merchantB"} $admin
    if($empty.data.totalRevenue -ne 0 -or !$empty.message){throw 'Empty-period message missing.'}
    $csv=Invoke-WebRequest "$BaseUrl/finance/revenue-report/export" -UseBasicParsing -Method Post -ContentType 'application/json' `
        -Headers @{Authorization="Bearer $admin"} -Body (@{fromDate=$from;toDate=$to;merchantId="$merchantB"}|ConvertTo-Json)
    $text=if($csv.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($csv.Content)}else{"$($csv.Content)"}
    if($csv.Headers['Content-Type'] -notmatch 'text/csv' -or $text -notmatch 'Tong doanh thu,130000' -or $text -notmatch 'Finance Smoke Package'){throw 'CSV export wrong.'}

    Write-Output '3. Commission per Merchant (UC 3.8.2)'
    Assert-Status 403 Post '/finance/commissions' @{} $seedMerchant
    $c=@((Call-Api Post '/finance/commissions' @{fromDate=$from;toDate=$to;merchantId="$merchantB"} $admin).data)
    if($c.Count -ne 1 -or $c[0].grossRevenue -ne 200000 -or $c[0].refundedAmount -ne 100000 -or $c[0].netRevenue -ne 100000 -or
       $c[0].commissionRate -ne 25 -or $c[0].commissionAmount -ne 25000 -or $c[0].payoutAmount -ne 75000){throw "Commission wrong: $($c | ConvertTo-Json -Compress)"}

    Write-Output '4. Settlement period: create once, Merchant sees only own, confirm payout'
    Assert-Status 400 Post '/finance/settlements' @{merchantId="$merchantB";fromDate=$from;toDate=$to+3600} $admin
    Start-Sleep -Seconds 2
    $quote=(Call-Api Post '/finance/settlements/quote' @{merchantId="$merchantB";fromDate=$from;toDate=$to} $admin).data
    $s=(Call-Api Post '/finance/settlements' @{merchantId="$merchantB";fromDate=$from;toDate=$to;quoteToken=$quote.quoteToken} $admin).data
    if($s.status -ne 1 -or $s.payoutAmount -ne 75000 -or $s.commissionAmount -ne 25000){throw 'Settlement snapshot wrong.'}
    Assert-Status 409 Post '/finance/settlements' @{merchantId="$merchantB";fromDate=$from;toDate=$to} $admin
    Sql "UPDATE washgo.merchants SET commission_rate=10 WHERE user_id='$merchantB';" | Out-Null
    $mine=Call-Api Post '/finance/settlements/filter' @{merchantId="$seedMerchantId"} $merchantTok
    if($mine.total -ne 1 -or $mine.list[0].id -ne $s.id -or $mine.list[0].commissionRate -ne 25){throw 'Merchant settlement scope or snapshot wrong.'}
    if(@((Call-Api Post '/finance/settlements/filter' @{take=100} $seedMerchant).list | Where-Object { $_.id -eq $s.id }).Count){throw 'Other Merchant sees the settlement.'}
    Assert-Status 400 Post "/finance/settlements/$($s.id)/confirm-paid" @{reference=' '} $admin
    $paid=(Call-Api Post "/finance/settlements/$($s.id)/confirm-paid" @{reference="BANK-$run";expectedVersion=$s.version} $admin).data
    if($paid.status -ne 2 -or $paid.paymentReference -ne "BANK-$run"){throw 'Payout not recorded.'}
    $notes=@((Call-Api Get '/notification?take=50' $null $merchantTok).list | Where-Object { $_.type -in 24,25 })
    if($notes.Count -ne 2){throw 'Merchant not notified about settlement.'}

    Write-Output '5. Refund list (UC 3.8.3)'
    $list=@((Call-Api Get '/payment/refund-requests?status=4' $null $admin).data | Where-Object { $_.id -eq $refund.id })
    if($list.Count -ne 1 -or !$list[0].orderCode -or !$list[0].customerName -or $list[0].status -ne 4){throw 'Refund list missing details.'}
    if(@((Call-Api Get '/payment/refund-requests?status=1' $null $admin).data | Where-Object { $_.id -eq $refund.id }).Count){throw 'Status filter wrong.'}
    if((Call-Api Get "/payment/refund-requests/$($refund.id)" $null $admin).data.amount -ne 100000){throw 'Refund detail wrong.'}
    Assert-Status 404 Get "/payment/refund-requests/$($refund.id)" $null $seedMerchant

    Write-Output 'PASS: revenue report totals/by merchant/by service/daily/empty period/CSV, commission and payout, settlement snapshot/overlap/scope/payout/notifications, refund list filter and detail.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($lockerId){
        Sql @"
BEGIN;
UPDATE washgo.orders SET status=99 WHERE locker_id='$lockerId' AND status NOT IN(12,99);
UPDATE washgo.locker_slots SET status=1 WHERE locker_id='$lockerId';
UPDATE washgo.lockers SET available_slots=total_slots,status=3,is_deleted=true WHERE id='$lockerId';
COMMIT;
"@ | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$shipperB'; UPDATE washgo.users SET is_active=false WHERE id IN('$merchantB','$shipperUser'); UPDATE washgo.merchants SET status=3 WHERE user_id='$merchantB'; UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id='$serviceId';"|Out-Null
}
