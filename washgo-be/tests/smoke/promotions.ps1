param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: private locker/services/customers/shipper and promotion codes with a
# per-run prefix. Only fixture orders are touched by SQL. No real payment provider calls.
$sessions=@{}; $lockerId=$null; $promotionIds=@()
$prefix="SMK$([Guid]::NewGuid().ToString('N').Substring(0,6).ToUpper())"
$kgServiceId=[Guid]::NewGuid(); $itemServiceId=[Guid]::NewGuid()
$customerB=[Guid]::NewGuid(); $customerC=[Guid]::NewGuid()
$fixtureUser=[Guid]::NewGuid(); $fixtureShipper=[Guid]::NewGuid()

function Iso($Minutes) { (Get-Date).ToUniversalTime().AddMinutes($Minutes).ToString('o') }
function New-Promotion($Body) {
    $p=(Call-Api Post '/promotion/' $Body $admin).data
    $script:promotionIds+=[Guid]$p.id
    return $p
}
function Promo-Body($Suffix,$Type,$Value,[hashtable]$Extra=@{}) {
    $b=@{code="$prefix-$Suffix";description="Smoke $Suffix";discountType=$Type;discountValue=$Value
        minOrderAmount=0;startAt=(Iso -5);endAt=(Iso 1440)}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}
    return $b
}
function Kg-Items { ,@(@{serviceId="$kgServiceId";quantity=1}) }
function Free-Box {
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $slot=$locker.availableSlots | Select-Object -First 1
    if(!$slot){throw 'No private fixture slot available.'}
    return $slot.id
}
function New-PromoOrder($Token,$Code) {
    $body=@{lockerId="$lockerId";boxId=(Free-Box);paymentMethod=2;items=(Kg-Items);customerNote='Promotion smoke'}
    if($Code){$body.promotionCode=$Code}
    (Call-Api Post '/wash-order/' $body $Token).data
}
function Promo-State($Id) { (Call-Api Get "/promotion/$Id" $null $admin).data }
function Parallel-Create($Requests) {
    $jobs=@()
    try {
        foreach($request in $Requests){
            $jobs+=Start-Job -ScriptBlock {
                param($Url,$Token,$Body)
                try {
                    $r=Invoke-WebRequest $Url -UseBasicParsing -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $Token"} -Body $Body
                    [int]$r.StatusCode
                } catch {if(!$_.Exception.Response){throw};[int]$_.Exception.Response.StatusCode}
            } -ArgumentList $request.url,$request.token,$request.body
        }
        @($jobs|Wait-Job|Receive-Job)
    } finally {$jobs|Stop-Job -ErrorAction SilentlyContinue;$jobs|Remove-Job -Force -ErrorAction SilentlyContinue}
}

try {
    foreach($role in @('Admin','Merchant','Customer','Shipper')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=[Guid]$sessions.Merchant.user.id;$seedCustomer=[Guid]$sessions.Customer.user.id;$seedShipper=[Guid]$sessions.Shipper.user.id
    $emailB="promo-$($customerB.ToString('N'))@example.com";$emailC="promo-$($customerC.ToString('N'))@example.com"
    $shipperEmail="promo-$($fixtureUser.ToString('N'))@example.com"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','$emailB','full_name','Promotion Smoke Customer B','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerC','email','$emailC','full_name','Promotion Smoke Customer C','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerC'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureUser','email','$shipperEmail','full_name','Promotion Smoke Shipper','auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedShipper';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$fixtureShipper','user_id','$fixtureUser','status',1,'is_active',true))).* FROM washgo.shippers s WHERE user_id='$seedShipper';
INSERT INTO washgo.services SELECT (jsonb_populate_record(NULL::washgo.services,to_jsonb(s)||jsonb_build_object(
'id','$kgServiceId','code','PROMO_KG_$($kgServiceId.ToString('N'))','name','Promotion Smoke Package','unit','kg',
'base_price',100000,'price_per_item',NULL,'estimated_time',24,'is_active',true,'is_deleted',false))).* FROM washgo.services s WHERE merchant_id='$merchantId' AND unit='kg' AND is_deleted=false LIMIT 1;
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email=$emailB;password='Customer@123'} $null).data;$tokenB=$sessions.B.accessToken
    $sessions.C=(Call-Api Post '/auth/login' @{email=$emailC;password='Customer@123'} $null).data;$tokenC=$sessions.C.accessToken
    $sessions.Fixture=(Call-Api Post '/auth/login' @{email=$shipperEmail;password='Shipper@123'} $null).data;$shipper=$sessions.Fixture.accessToken
    $boxes=1..8 | ForEach-Object { @{boxNumber="$_";size=2} }
    $locker=(Call-Api Post '/locker/' @{
        merchantId="$merchantId";code="PX_$([Guid]::NewGuid().ToString('N'))";name='Promotion Smoke Locker'
        address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=$boxes
    } $admin).data;$lockerId=[Guid]$locker.id

    Write-Output '1. Admin authorization and create validation (UC 3.6.2)'
    Assert-Status 401 Post '/promotion/filter' @{} $null
    Assert-Status 403 Post '/promotion/filter' @{} $customer
    Assert-Status 403 Post '/promotion/' (Promo-Body 'X' 1 10) $merchant
    Assert-Status 400 Post '/promotion/' (Promo-Body 'BAD CODE' 1 10) $admin
    Assert-Status 400 Post '/promotion/' (Promo-Body 'P101' 1 101) $admin
    Assert-Status 400 Post '/promotion/' (Promo-Body 'ZERO' 2 0) $admin
    Assert-Status 400 Post '/promotion/' (Promo-Body 'TIME' 1 10 @{startAt=(Iso 60);endAt=(Iso 30)}) $admin
    Assert-Status 400 Post '/promotion/' (Promo-Body 'LIM' 1 10 @{usageLimit=1;usageLimitPerCustomer=2}) $admin
    $pct=New-Promotion (Promo-Body 'PCT10' 1 10 @{maxDiscountAmount=8000;minOrderAmount=50000;usageLimit=2;usageLimitPerCustomer=1})
    $fixBody=Promo-Body 'FIX' 2 30000; $fixBody.code=$fixBody.code.ToLower()
    $fix=New-Promotion $fixBody
    if($fix.code -ne "$prefix-FIX" -or $fix.status -ne 2){throw 'Code not normalized or status not Active.'}
    Assert-Status 400 Post '/promotion/' (Promo-Body 'FIX' 2 1000) $admin
    $min=New-Promotion (Promo-Body 'MIN' 2 10000 @{minOrderAmount=200000})
    $upc=New-Promotion (Promo-Body 'UPC' 2 10000 @{startAt=(Iso 1440);endAt=(Iso 2880)})
    $one=New-Promotion (Promo-Body 'ONE' 2 5000 @{usageLimit=1})
    $big=New-Promotion (Promo-Body 'BIG' 2 1000000)

    Write-Output '2. List filters by status and time (UC 3.6.1)'
    $active=Call-Api Post '/promotion/filter' @{keyword=$prefix;status=2;page=1;take=50} $admin
    if($active.total -ne 5 -or @($active.list|Where-Object{$_.code -eq "$prefix-UPC"}).Count){throw 'Active filter wrong.'}
    $upcoming=Call-Api Post '/promotion/filter' @{keyword=$prefix;status=1} $admin
    if($upcoming.total -ne 1 -or $upcoming.list[0].id -ne $upc.id){throw 'Upcoming filter wrong.'}
    $future=[DateTimeOffset]::UtcNow.AddHours(30).ToUnixTimeSeconds()
    $window=Call-Api Post '/promotion/filter' @{keyword=$prefix;fromDate=$future} $admin
    if($window.total -ne 1){throw 'Time-window filter wrong.'}

    Write-Output '3. Customer quote: available list, eligibility and errors (UC 1.2.1 steps 8-12)'
    Assert-Status 403 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items)} $admin
    $quote=(Call-Api Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items)} $customer).data
    if($quote.totalAmount -ne 100000 -or $quote.discountAmount -ne 0){throw 'Quote total wrong.'}
    $mine=@($quote.availablePromotions|Where-Object{$_.code -like "$prefix-*"})
    if($mine.Count -ne 5 -or ($mine|Where-Object{$_.code -eq "$prefix-UPC"})){throw 'Available list wrong.'}
    $minRow=$mine|Where-Object{$_.code -eq "$prefix-MIN"}
    if($minRow.isEligible -or !$minRow.ineligibleReason -or $minRow.estimatedDiscountAmount -ne 0){throw 'Min-order eligibility wrong.'}
    if(($mine|Where-Object{$_.code -eq "$prefix-PCT10"}).estimatedDiscountAmount -ne 8000){throw 'Percentage cap wrong.'}
    Assert-Status 400 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-MIN"} $customer
    Assert-Status 400 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-UPC"} $customer
    Assert-Status 400 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-NONE"} $customer
    $applied=(Call-Api Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode=" $($prefix.ToLower())-pct10 "} $customer).data
    if($applied.discountAmount -ne 8000 -or $applied.finalAmount -ne 92000 -or $applied.appliedPromotion.code -ne "$prefix-PCT10"){throw 'Applied quote wrong.'}
    $bigQuote=(Call-Api Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-BIG"} $customer).data
    if($bigQuote.finalAmount -ne 5000){throw 'Discount must leave the minimum payable amount.'}

    Write-Output '4. Order creation records usage; global and per-customer limits'
    $o1=New-PromoOrder $customer "$prefix-PCT10"
    if($o1.discountAmount -ne 8000 -or $o1.finalAmount -ne 92000 -or $o1.promotionCode -ne "$prefix-PCT10"){throw 'Order discount not stored.'}
    if((Promo-State $pct.id).usedCount -ne 1){throw 'Usage not counted.'}
    $freeBefore=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    Assert-Status 400 Post '/wash-order/' @{lockerId="$lockerId";boxId=(Free-Box);paymentMethod=2;items=(Kg-Items);promotionCode="$prefix-PCT10"} $customer
    $freeAfter=Sql "SELECT available_slots FROM washgo.lockers WHERE id='$lockerId';"
    if($freeBefore -ne $freeAfter){throw 'Rejected voucher order kept a reserved box.'}
    $o2=New-PromoOrder $tokenB "$prefix-PCT10"
    $state=Promo-State $pct.id
    if($state.usedCount -ne 2 -or $state.status -ne 4){throw 'Promotion not exhausted after global limit.'}
    Assert-Status 400 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-PCT10"} $tokenC
    Assert-Status 400 Post '/wash-order/' @{lockerId="$lockerId";boxId=(Free-Box);paymentMethod=2;items=(Kg-Items);promotionCode="$prefix-PCT10"} $tokenC

    Write-Output '5. Edit/delete rules for used codes (UC 3.6.3, 3.6.4)'
    $upd=Promo-Body 'PCT10' 1 15 @{id=$pct.id;maxDiscountAmount=8000;minOrderAmount=50000;usageLimit=1;usageLimitPerCustomer=1}
    $upd.expectedVersion=(Promo-State $pct.id).version
    Assert-Status 400 Put '/promotion/' $upd $admin
    $upd.usageLimit=3; $upd.code="$prefix-RENAMED"
    Assert-Status 400 Put '/promotion/' $upd $admin
    $upd.code="$prefix-PCT10"
    $updated=(Call-Api Put '/promotion/' $upd $admin).data
    if($updated.discountValue -ne 15 -or $updated.usedCount -ne 2 -or $updated.status -ne 2){throw 'Update lost used count or status.'}
    if((Call-Api Get "/wash-order/$($o1.id)" $null $customer).data.discountAmount -ne 8000){throw 'Editing a promotion changed an existing order.'}
    Assert-Status 400 Delete "/promotion/$($pct.id)?expectedVersion=$($updated.version)" $null $admin
    Call-Api Delete "/promotion/$($min.id)?expectedVersion=$((Promo-State $min.id).version)" $null $admin | Out-Null
    Assert-Status 404 Get "/promotion/$($min.id)" $null $admin
    $reuse=New-Promotion (Promo-Body 'MIN' 2 10000)
    if($reuse.code -ne "$prefix-MIN"){throw 'Deleted code could not be reused.'}

    Write-Output '6. Cancelling an order returns the voucher use'
    Call-Api Post "/wash-order/$($o1.id)/cancel" @{reason='Promotion smoke cancel'} $customer | Out-Null
    if((Promo-State $pct.id).usedCount -ne 1){throw 'Cancel did not release the usage.'}
    Call-Api Post "/wash-order/$($o1.id)/cancel" @{reason='Replay'} $customer | Out-Null
    if((Promo-State $pct.id).usedCount -ne 1){throw 'Cancel replay released usage twice.'}
    $again=(Call-Api Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-PCT10"} $customer).data
    if($again.discountAmount -ne 8000){throw 'Customer could not reuse the released voucher.'}

    Write-Output '7. Cancellation fee uses the pre-discount price; weighing keeps the discount'
    $late=New-PromoOrder $customer "$prefix-FIX"
    Sql "UPDATE washgo.orders SET created_on=EXTRACT(EPOCH FROM NOW())::bigint-1860,deposit_deadline=deposit_deadline-(created_on-(EXTRACT(EPOCH FROM NOW())::bigint-1860))*interval '1 second' WHERE id='$($late.id)';" | Out-Null
    $lateQuote=(Call-Api Get "/wash-order/$($late.id)/cancellation-quote" $null $customer).data
    if($lateQuote.basisAmount -ne 100000 -or $lateQuote.feeAmount -ne 30000){throw 'Cancellation basis must be TotalAmount before discount.'}
    $w=New-PromoOrder $customer "$prefix-FIX"
    Invoke-TestDelivery $w.id deposit $customer | Out-Null
    Call-Api Put '/wash-order/assign-shipper' @{orderId=$w.id;shipperId="$fixtureShipper"} $merchant | Out-Null
    Invoke-TestDelivery $w.id collect $shipper | Out-Null
    $weighed=(Call-Api Put "/wash-order/$($w.id)/actual-weight" @{weightKg=6.5} $merchant).data
    if($weighed.weightSurchargeAmount -ne 22500 -or $weighed.discountAmount -ne 30000 -or $weighed.finalAmount -ne 92500){throw 'Weighing lost the discount.'}

    Write-Output '8. Concurrent orders cannot exceed the usage limit'
    $bodyA=@{lockerId="$lockerId";boxId=(Free-Box);paymentMethod=2;items=(Kg-Items);promotionCode="$prefix-ONE"}
    $locker=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    $bodyB=@{lockerId="$lockerId";boxId=($locker.availableSlots|Where-Object{$_.id -ne $bodyA.boxId}|Select-Object -First 1).id
        paymentMethod=2;items=(Kg-Items);promotionCode="$prefix-ONE"}
    $results=Parallel-Create @(
        @{url="$BaseUrl/wash-order/";token=$customer;body=($bodyA|ConvertTo-Json -Depth 5 -Compress)},
        @{url="$BaseUrl/wash-order/";token=$tokenB;body=($bodyB|ConvertTo-Json -Depth 5 -Compress)}
    )
    if(@($results|Where-Object{$_ -eq 201}).Count -ne 1 -or @($results|Where-Object{$_ -eq 400}).Count -ne 1){throw "Race result wrong: $results"}
    if((Promo-State $one.id).usedCount -ne 1){throw 'Race over-consumed the promotion.'}
    $usageRows=Sql "SELECT count(*) FROM washgo.promotion_usages WHERE promotion_id='$($one.id)';"
    if([int]$usageRows -ne 1){throw 'Race created extra usage rows.'}

    Write-Output '9. Usage statistics and CSV export (UC 3.6.5)'
    $stats=Call-Api Get "/promotion/$($pct.id)/statistics" $null $admin
    if($stats.data.usedCount -ne 1 -or $stats.data.totalDiscountAmount -ne 8000 -or $stats.data.revenue -ne 92000 -or $stats.data.daily.Count -ne 1){throw 'Statistics wrong.'}
    $emptyStats=Call-Api Get "/promotion/$($upc.id)/statistics" $null $admin
    if($emptyStats.data.usedCount -ne 0 -or !$emptyStats.message -or $emptyStats.data.daily.Count -ne 0){throw 'Empty statistics message missing.'}
    $csv=Invoke-WebRequest "$BaseUrl/promotion/$($pct.id)/statistics/export" -UseBasicParsing -Headers @{Authorization="Bearer $admin"}
    $text=if($csv.Content -is [byte[]]){[Text.Encoding]::UTF8.GetString($csv.Content)}else{"$($csv.Content)"}
    if($csv.Headers['Content-Type'] -notmatch 'text/csv' -or $text -notmatch 'Ngay,SoLuotDung' -or $text -notmatch 'TONG,1,8000'){throw 'CSV export wrong.'}
    Assert-Status 403 Get "/promotion/$($pct.id)/statistics" $null $customer

    Write-Output '10. Toggle off hides the code from customers'
    Call-Api Put "/promotion/$($fix.id)/status" @{expectedVersion=(Promo-State $fix.id).version;isActive=$false} $admin | Out-Null
    Assert-Status 400 Post '/wash-order/apply-voucher' @{lockerId="$lockerId";items=(Kg-Items);promotionCode="$prefix-FIX"} $customer
    if((Promo-State $fix.id).status -ne 0){throw 'Toggle did not deactivate.'}

    Write-Output 'PASS: promotion CRUD/validation, filters, customer quote, order discount, usage limits, race, cancel release, pre-discount cancel fee, weighing, statistics and CSV export.'
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
    if($promotionIds.Count){
        $ids=($promotionIds|ForEach-Object{"'$_'"}) -join ','
        Sql "UPDATE washgo.promotions SET is_active=false,is_deleted=true WHERE id IN($ids);" | Out-Null
    }
    Sql "UPDATE washgo.shippers SET is_active=false WHERE id='$fixtureShipper'; UPDATE washgo.users SET is_active=false WHERE id IN('$customerB','$customerC','$fixtureUser'); UPDATE washgo.services SET is_deleted=true,is_active=false WHERE id IN('$kgServiceId','$itemServiceId');"|Out-Null
}
