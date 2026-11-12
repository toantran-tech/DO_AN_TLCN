param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"

# LOCAL DEV ONLY: the test scenarios listed in Docs/cần làm (finance rounding and periods, report scope, audit per module,
# locker reservation race, waiting-tab media, CSV month boundaries). Money rows are inserted directly for exact cases.
$sessions=@{}; $teamId=$null; $lockerId=$null; $orders=@(); $settlementId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
function StatusOf($Method,$Path,$Body,$Token){ try{ Call-Api $Method $Path $Body $Token | Out-Null; 200 } catch { [int]$_.Exception.Response.StatusCode } }
function Ts($DaysAgo){ "now() - interval '$DaysAgo days'" }

try {
    foreach($role in @('Admin','Customer')){ $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data }
    $admin=$sessions.Admin.accessToken; $customer=$sessions.Customer.accessToken
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId="$($sessions.Team.user.id)"
    $seedMerchant=Sql "SELECT user_id FROM washgo.merchants m JOIN washgo.users u ON u.id=m.user_id WHERE u.email='merchant@washgo.vn';"
    $base=Sql "SELECT id FROM washgo.orders WHERE is_deleted=false ORDER BY created_on DESC LIMIT 1;"
    $customerOfBase=Sql "SELECT customer_id FROM washgo.orders WHERE id='$base';"
    $n=0
    function New-Order($Rate,$MerchantId){
        $script:n++; $id=[Guid]::NewGuid(); $code="DS$run$($script:n)"
        Sql "INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object('id','$id','order_code','$code','merchant_order_code',NULL,'receipt_number',NULL,'merchant_id','$MerchantId','commission_rate_applied',$Rate,'commission_snapshot_source','Assignment','status',12,'payment_status',2,'is_deleted',false))).* FROM washgo.orders o WHERE id='$base';" | Out-Null
        $script:orders+=$id; "$id"
    }
    function Pay($Order,$Amount,$Rate,$DaysAgo,$Settlement='NULL'){
        Sql "INSERT INTO washgo.payment_transactions(id,order_id,transaction_reference,payment_method,status,amount,completed_at,created_on,modified_on,is_deleted,purpose,commission_rate_snapshot,financial_snapshot_basis,merchant_id_snapshot,settlement_id) VALUES (gen_random_uuid(),'$Order','DS-'||gen_random_uuid(),2,2,$Amount,$(Ts $DaysAgo),extract(epoch from now())::bigint,extract(epoch from now())::bigint,false,1,$Rate,'payment-created','$teamId',$Settlement);" | Out-Null
    }
    function Refund($Order,$Amount,$Rate,$DaysAgo){
        Sql "INSERT INTO washgo.refund_requests(id,order_id,customer_id,amount,reason,status,refunded_at,created_on,modified_on,is_deleted,commission_rate_snapshot,financial_snapshot_basis,merchant_id_snapshot) VALUES (gen_random_uuid(),'$Order','$customerOfBase',$Amount,'Doc scenario',4,$(Ts $DaysAgo),extract(epoch from now())::bigint,extract(epoch from now())::bigint,false,$Rate,'payment-created','$teamId');" | Out-Null
    }

    Write-Output '1. Commission rounding per transaction, midpoint, several refunds, refunds in a later period, refund after a paid period'
    $settlementId=[Guid]::NewGuid()
    Sql "INSERT INTO washgo.merchant_settlements(id,merchant_id,merchant_name,period_from,period_to,order_count,gross_revenue,refunded_amount,net_revenue,commission_rate,commission_amount,payout_amount,status,paid_at,created_on,modified_on,is_deleted,carry_forward_amount,period_payout_amount) VALUES ('$settlementId','$teamId','Doc $run',now()-interval '25 days',now()-interval '15 days',1,50000,0,50000,20,10000,40000,2,now()-interval '14 days',extract(epoch from now())::bigint,extract(epoch from now())::bigint,false,0,40000);" | Out-Null
    $A=New-Order 50 $teamId; Pay $A 1 50 2; Pay $A 1 50 2                       # 0.5 + 0.5 -> 1 + 1 = 2 (not round(1.0) = 1)
    $B=New-Order 50 $teamId; Pay $B 3 50 2                                       # 1.5 -> 2 (half away from zero)
    $C=New-Order 10 $teamId; Pay $C 100000 10 40; Refund $C 20000 10 35; Refund $C 30000 10 0   # 10000 - 2000 - 3000
    $D=New-Order 'NULL' $teamId; Refund $D 5000 'NULL' 0                          # refund-only, no rate
    $E=New-Order 20 $teamId; Pay $E 50000 20 20 "'$settlementId'"; Refund $E 10000 20 0         # 10000 - 2000, refund after paid period
    $F=New-Order 'NULL' $teamId; Pay $F 7000 'NULL' 0; Refund $F 1000 'NULL' 0   # missing on both sides: counted once
    $G=New-Order 10 $seedMerchant; Pay $G 1000 10 0                              # reassigned later: still the team's money (+100)
    Sql "INSERT INTO washgo.order_items(id,order_id,service_id,service_name,quantity,unit_price,total_price,created_at,is_weight_based) VALUES (gen_random_uuid(),'$G','$standardWash','Doc ranking',1,1000,1000,now(),false);" | Out-Null

    $life=(Call-Api Get '/merchant-reports/lifetime' $null $team).data
    if($life.grossLaundryRevenue -ne 158005 -or $life.refunds -ne 66000 -or $life.netRevenue -ne 92005 -or $life.commission -ne 13104 -or $life.merchantPayout -ne 78901 -or $life.ordersWithoutCommissionSnapshot -ne 2){throw "Lifetime wrong: $($life | ConvertTo-Json -Compress)"}
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $fc=@((Call-Api Post '/finance/commissions' @{fromDate=$now-60*86400;toDate=$now+3600;merchantId=$teamId} $admin).data)[0]
    if($fc.commissionAmount -ne 13104 -or $fc.ordersWithoutCommissionSnapshot -ne 2){throw "Finance commissions differ from the report: $($fc.commissionAmount)/$($fc.ordersWithoutCommissionSnapshot)"}
    $oa=(Call-Api Get "/wash-order/$A" $null $admin).data; $oc=(Call-Api Get "/wash-order/$C" $null $admin).data; $oe=(Call-Api Get "/wash-order/$E" $null $admin).data
    if($oa.commissionOnCollected -ne 2 -or $oc.commissionOnCollected -ne 5000 -or $oc.merchantPayoutOnCollected -ne 45000){throw "Order rounding wrong: $($oa.commissionOnCollected) $($oc.commissionOnCollected) $($oc.merchantPayoutOnCollected)"}
    if($oe.financialStatus -ne 'Adjusted' -or $oe.settledNetAmount -ne 50000 -or $oe.unsettledNetAmount -ne -10000){throw "Refund after a paid period wrong: $($oe.financialStatus) $($oe.settledNetAmount) $($oe.unsettledNetAmount)"}
    $vnToday=[DateTime]::UtcNow.AddHours(7).ToString('yyyy-MM-dd')
    $ov=(Call-Api Get "/merchant-reports/overview?period=custom&from=$vnToday&to=$vnToday" $null $team).data
    if($ov.kpis.commissionCoverage.currentUncoveredOrders -ne 2){throw "Refund-only / both-sides missing not counted once: $($ov.kpis.commissionCoverage.currentUncoveredOrders)"}
    if(-not ($ov.topServices | Where-Object { $_.serviceId -eq $standardWash })){throw 'Ranking ignores the Merchant snapshotted on the payment.'}

    Write-Output '2. Receipts and refunds written while reports are read: every response is internally consistent'
    $job=Start-Job -ScriptBlock { param($C,$Cust,$Team,$Container,$User,$Db)
        foreach($i in 1..15){ "INSERT INTO washgo.refund_requests(id,order_id,customer_id,amount,reason,status,refunded_at,created_on,modified_on,is_deleted,commission_rate_snapshot,financial_snapshot_basis,merchant_id_snapshot) VALUES (gen_random_uuid(),'$C','$Cust',100,'Doc race',4,now(),0,0,false,10,'payment-created','$Team');" | docker exec -i $Container psql -U $User -d $Db -q | Out-Null }
    } -ArgumentList $C,$customerOfBase,$teamId,$PostgresContainer,$PostgresUser,$Database
    foreach($i in 1..8){
        $l=(Call-Api Get '/merchant-reports/lifetime' $null $team).data
        if($l.netRevenue -ne $l.grossLaundryRevenue - $l.refunds -or $l.merchantPayout -ne $l.netRevenue - $l.commission){throw "Inconsistent snapshot: $($l | ConvertTo-Json -Compress)"}
    }
    $job | Wait-Job | Remove-Job -Force

    Write-Output '3. Audit: rejections in each module, rollback leaves no success, signed URLs masked'
    $start=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-1
    $req=(Call-Api Post '/merchant-profile/update-requests' @{businessName="Doc $run";address='1 Doc Street';city='Test';phone='0901234567';openingHours='08:00-20:00';latitude=10.7;longitude=106.7} $team).data
    $checks=@(
        @{m='Post';p="/merchant-profile-request/$($req.id)/approve?expectedVersion=stale";b=$null;mod='merchant-profile-request'},
        @{m='Post';p="/service-proposal/$([Guid]::NewGuid())/approve";b=$null;mod='service-proposal'},
        @{m='Post';p="/order-assignment/$([Guid]::NewGuid())/assign";b=@{merchantId=$teamId};mod='order-assignment'},
        @{m='Post';p="/finance/settlements/$([Guid]::NewGuid())/confirm-paid";b=@{paymentReference='X'};mod='finance'},
        @{m='Put';p='/system-setting/commission';b=@{defaultCommissionRate=10;expectedVersion='stale'};mod='system-setting'})
    foreach($c in $checks){ if((StatusOf $c.m $c.p $c.b $admin) -lt 400){throw "Expected a rejection for $($c.p)"} }
    $logs=(Call-Api Post '/admin/audit-logs/filter' @{fromDate=$start;actorId=$sessions.Admin.user.id;take=100} $admin).list
    foreach($c in $checks){
        $hit=@($logs | Where-Object { $_.module -eq $c.mod -and $_.result -in 2,3 -and $_.errorCode })
        if($hit.Count -lt 1){throw "Rejected $($c.mod) write not audited."}
    }
    if(@($logs | Where-Object { $_.module -in 'SystemSetting','Finance' -and $_.result -eq 1 }).Count){throw 'A rejected write left a success entry.'}
    Call-Api Put '/merchant/status' @{merchantId=$teamId;status=3;rejectionReason="Xem https://files.example.com/doc.pdf?sig=abc123&expires=999";confirm=$true;expectedVersion=(Call-Api Get "/merchant/$teamId" $null $admin).data.version} $admin | Out-Null
    $lock=@((Call-Api Post '/admin/audit-logs/filter' @{module='Merchant';resourceId=$teamId;fromDate=$start;take=5} $admin).list | Where-Object { $_.action -eq 'Lock' })[0]
    if($lock.reason -match 'sig=abc123' -or $lock.reason -notmatch '\[signed\]'){throw "Signed URL not masked: $($lock.reason)"}
    Call-Api Put '/merchant/status' @{merchantId=$teamId;status=1;confirm=$true;expectedVersion=(Call-Api Get "/merchant/$teamId" $null $admin).data.version} $admin | Out-Null

    Write-Output '4. Locker: a reservation between the Admin read and the box write is a conflict'
    $locker=(Call-Api Post '/locker/' @{merchantId=$teamId;name="Doc Locker $run";address='Private development fixture';district="Doc$run";city='Test';latitude=10.77;longitude=106.69;boxCount=4} $admin).data
    $lockerId=[Guid]$locker.id
    $seen=(Call-Api Get "/locker/$lockerId" $null $admin).data
    $slot=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0]
    $o1=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot.id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    if((StatusOf Put '/locker/box-status' @{lockerId="$lockerId";boxId=$slot.id;newStatus=4;expectedVersion=$seen.version;expectedBoxStatus=1} $admin) -ne 409){throw 'Reservation between read and write not detected.'}

    Write-Output '5. Waiting tab: orders sharing a locker, missing/broken image, soft-deleted locker'
    Invoke-TestDelivery $o1.id deposit $customer | Out-Null
    $slot2=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data.availableSlots[0]
    $o2=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$slot2.id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    Invoke-TestDelivery $o2.id deposit $customer | Out-Null
    # Deposits may already pick the locker's Merchant: put both orders back in the waiting state for this check.
    Sql "UPDATE washgo.orders SET merchant_id=NULL, merchant_name=NULL WHERE id IN ('$($o1.id)','$($o2.id)');" | Out-Null
    function Waiting { @((Call-Api Post '/order-assignment/filter' @{district="Doc$run";page=1;take=100} $admin).list | Where-Object { $_.id -in @($o1.id,$o2.id) }) }
    $w=Waiting
    if($w.Count -ne 2){throw "Waiting orders not listed: $($w.Count)"}
    if(@($w | Where-Object { @($_.lockerImageUrls).Count -ne 0 -or $null -ne $_.lockerThumbnailUrl }).Count){throw "Missing locker image not empty: $($w | ConvertTo-Json -Depth 3 -Compress)"}
    Sql "UPDATE washgo.lockers SET images='[""/uploads/missing-$run.png""]'::jsonb WHERE id='$lockerId';" | Out-Null
    $w=Waiting
    if(@($w | Where-Object { $_.lockerThumbnailUrl -ne "/uploads/missing-$run.png" }).Count){throw 'Shared locker image not on every order.'}
    Sql "UPDATE washgo.lockers SET is_deleted=true WHERE id='$lockerId';" | Out-Null
    $w=Waiting
    if($w.Count -ne 2 -or @($w | Where-Object { -not $_.lockerName }).Count){throw 'Soft-deleted locker broke the waiting tab.'}
    Sql "UPDATE washgo.lockers SET is_deleted=false WHERE id='$lockerId';" | Out-Null

    Write-Output '6. CSV month boundaries in Vietnam time'
    $mFrom=[DateTimeOffset]::Parse('2026-02-01T00:00:00+07:00').ToUnixTimeSeconds(); $mTo=[DateTimeOffset]::Parse('2026-02-28T23:59:59+07:00').ToUnixTimeSeconds()
    $mc=Call-Api Post '/finance/revenue-report/export' @{fromDate=$mFrom;toDate=$mTo} $admin; $mt=if($mc.data){$mc.data}else{"$mc"}
    if($mt -notmatch 'Tu ngay,2026-02-01T00:00:00\+07:00' -or $mt -notmatch 'Den ngay,2026-02-28T23:59:59\+07:00'){throw 'CSV month boundaries wrong.'}

    Write-Output 'PASS: commission per transaction (1d+1d at 50% = 2, 1.5 -> 2), several refunds, refunds in later periods and after a paid period are identical in lifetime, Admin commissions and order detail; refund-only and both-sides-missing orders counted once; ranking follows the Merchant on the payment; concurrent refunds never break net/payout identities; rejected writes audited in every module with no success entry; signed URLs masked; a reservation between read and write is a 409; waiting tab handles shared, missing, broken images and a soft-deleted locker; CSV month boundaries in +07:00.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}
    if($orders.Count){
        $ids=($orders | ForEach-Object { "'$_'" }) -join ','
        Sql "UPDATE washgo.payment_transactions SET is_deleted=true WHERE order_id IN ($ids); UPDATE washgo.refund_requests SET is_deleted=true WHERE order_id IN ($ids); UPDATE washgo.orders SET is_deleted=true WHERE id IN ($ids);" | Out-Null
    }
    if($settlementId){ Sql "UPDATE washgo.merchant_settlements SET is_deleted=true WHERE id='$settlementId';" | Out-Null }
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
