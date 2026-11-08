param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
# Local DB only. All SQL fixtures have unique IDs and are deleted in finally. No gateway or email calls.
$sessions=@();$promotions=@();$orders=@();$settlements=@();$fixture=[Guid]::NewGuid();$run=$fixture.ToString('N').Substring(0,12)
try {
    foreach($role in @('Admin','Merchant','Customer')){
        $s=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data;$sessions+=$s
        Set-Variable -Name $role.ToLower() -Value $s.accessToken
        if($role -eq 'Merchant'){$source=$s.user.id}
        if($role -eq 'Customer'){$customerId=$s.user.id}
    }
    foreach($i in 1..3){
        $promotions+=(Call-Api Post '/promotion/' @{code="FLT_${run}_$i";discountType=1;discountValue=10;minOrderAmount=0;startAt="2026-01-0$($i)T00:00:00Z";endAt='2028-01-01T00:00:00Z';usageLimit=(4-$i);isActive=$true} $admin).data.id
    }
    $asc=Call-Api Post '/promotion/filter' @{keyword="FLT_$run";take=2;sortBy=@(@{field='UsageLimit';isDescending=$false})} $admin
    if($asc.total -ne 3 -or $asc.list[0].id -ne $promotions[2] -or $asc.list[1].id -ne $promotions[1]){throw 'Promotion numeric sort/page incorrect.'}
    $second=Call-Api Post '/promotion/filter' @{keyword="FLT_$run";page=2;take=2;sortBy=@(@{field='UsageLimit';isDescending=$false})} $admin
    if($second.list[0].id -ne $promotions[0]){throw 'Promotion page boundary incorrect.'}
    $desc=Call-Api Post '/promotion/filter' @{keyword="FLT_$run";sortBy=@(@{field='Code';isDescending=$true},@{field='StartAt';isDescending=$false})} $admin
    if($desc.list[0].id -ne $promotions[2]){throw 'Promotion string/multi-sort incorrect.'}
    Assert-Status 400 Post '/promotion/filter' @{sortBy=@(@{field='SQL-invalid'})} $admin
    $template=Sql 'SELECT id FROM washgo.orders LIMIT 1;'
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','filter-$fixture@example.test','provider_id',NULL))).* FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object('user_id','$fixture','tax_code',NULL))).* FROM washgo.merchants m WHERE user_id='$source';
"@|Out-Null
    foreach($i in 1..3){
        $oid=[Guid]::NewGuid();$orders+=$oid
        Sql @"
INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object('id','$oid','order_code','FLT_${run}_$i','merchant_order_code','FLT_${run}_$i','merchant_id','$fixture','customer_id','$customerId','status',12,'is_deleted',false))).* FROM washgo.orders o WHERE id='$template';
INSERT INTO washgo.promotion_usages(id,promotion_id,order_id,customer_id,order_amount,discount_amount,used_at,is_reverted,reverted_at,created_on,modified_on,is_deleted)
VALUES(gen_random_uuid(),'$($promotions[0])','$oid','$customerId',100000,$($i*1000),'2026-01-0$($i)T00:00:00Z',$($i -eq 2),$(if($i -eq 2){"'2026-01-05T00:00:00Z'"}else{'NULL'}),1,1,false);
"@|Out-Null
    }
    $history=Call-Api Post "/promotion/$($promotions[0])/usages/filter" @{take=2;sortBy=@(@{field='DiscountAmount';isDescending=$true})} $admin
    if($history.total -ne 3 -or $history.list.Count -ne 2 -or $history.list[0].discountAmount -ne 3000 -or !$history.list[0].orderCode -or !$history.list[0].customerName){throw 'Usage joins/sort/pagination incorrect.'}
    $reverted=Call-Api Post "/promotion/$($promotions[0])/usages/filter" @{isReverted=$true;customerId=$customerId;orderId="$($orders[1])"} $admin
    if($reverted.total -ne 1 -or !$reverted.list[0].revertedAt -or $reverted.list[0].revertReason){throw 'Reverted history lost or fabricated reason.'}
    Assert-Status 400 Post "/promotion/$($promotions[0])/usages/filter" @{fromDate=20;toDate=10} $admin
    Assert-Status 403 Post "/promotion/$($promotions[0])/usages/filter" @{} $merchant
    Assert-Status 403 Post "/promotion/$($promotions[0])/usages/filter" @{} $customer
    Assert-Status 404 Post "/promotion/$([Guid]::NewGuid())/usages/filter" @{} $admin
    foreach($i in 1..2){
        $sid=[Guid]::NewGuid();$settlements+=$sid
        $lines=@(@{OrderId="$($orders[0])";OrderCode='A';Collected=100;Refunded=0},@{OrderId="$($orders[1])";OrderCode='B';Collected=200;Refunded=0},@{OrderId="$($orders[2])";OrderCode='C';Collected=300;Refunded=0})
        # Pascal-case is the persisted historical snapshot contract.
        $snapshot=@{Lines=$lines}|ConvertTo-Json -Depth 8 -Compress
        Sql @"
INSERT INTO washgo.merchant_settlements(id,merchant_id,merchant_name,period_from,period_to,order_count,gross_revenue,refunded_amount,net_revenue,commission_rate,commission_amount,payout_amount,status,payment_reference,paid_at,breakdown_snapshot_json,created_on,modified_on,is_deleted)
VALUES('$sid','$fixture','Filter fixture $run','2026-0$($i)-01T00:00:00Z','2026-0$($i)-20T00:00:00Z',3,600,0,600,0,0,$($i*100),2,'FILTER-$run-$i','2026-0$($i)-21T12:00:00Z','$snapshot'::jsonb,1,1,false);
"@|Out-Null
    }
    $from=[DateTimeOffset]::Parse('2026-02-10T00:00:00Z').ToUnixTimeSeconds();$to=[DateTimeOffset]::Parse('2026-02-25T00:00:00Z').ToUnixTimeSeconds()
    $filtered=Call-Api Post '/finance/settlements/filter' @{merchantId="$fixture";fromDate=$from;toDate=$to;paidFromDate=$from;paidToDate=$to;sortBy=@(@{field='PayoutAmount';isDescending=$true})} $admin
    if($filtered.total -ne 1 -or $filtered.list[0].id -ne $settlements[1]){throw 'Settlement period/paid-date filter incorrect.'}
    $page=Call-Api Post '/finance/settlements/filter' @{merchantId="$fixture";take=1;sortBy=@(@{field='PayoutAmount';isDescending=$true})} $admin
    if($page.total -ne 2 -or $page.list[0].id -ne $settlements[1]){throw 'Settlement sort/page incorrect.'}
    if((Call-Api Post '/finance/settlements/filter' @{merchantId="$fixture";keyword="FILTER-$run"} $merchant).list.id -contains "$($settlements[0])"){throw 'Merchant scope bypassed.'}
    Assert-Status 400 Post '/finance/settlements/filter' @{paidFromDate=20;paidToDate=10} $admin
    Assert-Status 400 Post '/finance/settlements/filter' @{sortBy=@(@{field='unsupported'})} $admin
    $lines=Call-Api Get "/finance/settlements/$($settlements[0])/lines?page=2&take=1" $null $admin
    if($lines.total -ne 3 -or $lines.list.Count -ne 1 -or $lines.list[0].orderCode -ne 'B'){throw 'Database snapshot line paging failed.'}
    if((Call-Api Get "/finance/settlements/$($settlements[0])/lines?page=99&take=1" $null $admin).list.Count){throw 'Out-of-range page should be empty.'}
    Assert-Status 404 Get "/finance/settlements/$($settlements[0])/lines" $null $merchant
    # 25,001 source rows exercise the bounded synchronous report path without a gateway call.
    Sql @"
INSERT INTO washgo.payment_transactions(id,order_id,transaction_reference,payment_method,purpose,status,amount,completed_at,merchant_id_snapshot,commission_rate_snapshot,financial_snapshot_basis,created_on,modified_on,is_deleted)
SELECT gen_random_uuid(),'$($orders[0])','FILTER-$run-'||n,2,1,2,1,'2026-01-05T12:00:00Z','$fixture',0,'test-fixture',1,1,false FROM generate_series(1,25001) n;
"@|Out-Null
    $report=@{merchantId="$fixture";fromDate=[DateTimeOffset]::Parse('2026-01-01T00:00:00Z').ToUnixTimeSeconds();toDate=[DateTimeOffset]::Parse('2026-01-31T23:59:59Z').ToUnixTimeSeconds()}
    Assert-Status 413 Post '/finance/revenue-report' $report $admin
    Assert-Status 413 Post '/finance/revenue-report/export' $report $admin
    Sql "DELETE FROM washgo.payment_transactions WHERE transaction_reference='FILTER-$run-25001';"|Out-Null
    if((Call-Api Post '/finance/revenue-report' $report $admin).data.totalRevenue -ne 25000){throw 'Report at row limit is incorrect.'}
    Write-Output 'PASS: Promotion multi-sort/pages, voucher usage/revert history and roles, settlement period/paid filters/scope, PostgreSQL snapshot line paging, 25,000-row boundary and 25,001-row report/CSV rejection.'
} catch { Write-Output $_.ScriptStackTrace; throw }
finally {
    foreach($sid in $settlements){Sql "DELETE FROM washgo.merchant_settlements WHERE id='$sid';"|Out-Null}
    foreach($oid in $orders){Sql "DELETE FROM washgo.promotion_usages WHERE order_id='$oid'; DELETE FROM washgo.payment_transactions WHERE order_id='$oid'; DELETE FROM washgo.orders WHERE id='$oid';"|Out-Null}
    foreach($promotionKey in $promotions){Sql "DELETE FROM washgo.promotions WHERE id='$promotionKey';"|Out-Null}
    Sql "DELETE FROM washgo.merchants WHERE user_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture';"|Out-Null
    foreach($session in $sessions){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}
}
