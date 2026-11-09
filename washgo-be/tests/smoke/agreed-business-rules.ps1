param([string]$BaseUrl='http://localhost:5000',[string]$PostgresContainer='washgo_postgres',[string]$PostgresUser='postgres',[string]$Database='washgo_db')
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http
$sessions=@();$fixture=[Guid]::NewGuid();$orders=@();$incidents=@();$locker=$null;$settlementKeys=@()
function Upload-Proof($Token){
    $client=[Net.Http.HttpClient]::new();$form=[Net.Http.MultipartFormDataContent]::new()
    try {
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Token)
        $bytes=[byte[]]::new(64);[byte[]](0x89,0x50,0x4e,0x47,13,10,26,10)|ForEach-Object -Begin {$i=0} -Process {$bytes[$i++]=$_}
        $form.Add([Net.Http.ByteArrayContent]::new($bytes),'file','proof.png')
        ($client.PostAsync("$BaseUrl/files/images",$form).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json).data.url
    } finally {$form.Dispose();$client.Dispose()}
}
function New-FixtureOrder($Suffix){
    $id=[Guid]::NewGuid();$script:orders+=$id
    Sql @"
INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
'id','$id','order_code','AG_$($id.ToString('N'))','merchant_order_code','AG_$($id.ToString('N'))','merchant_id','$fixture','customer_id','$customerId','status',12,'payment_status',2,'final_amount',100000,'commission_rate_applied',20,'is_deleted',false))).* FROM washgo.orders o WHERE id='$template';
"@|Out-Null
    $id
}
function Add-Payment($Order,$Amount,$Date){
    $id=[Guid]::NewGuid()
    Sql "INSERT INTO washgo.payment_transactions(id,order_id,transaction_reference,payment_method,purpose,status,amount,completed_at,merchant_id_snapshot,commission_rate_snapshot,financial_snapshot_basis,created_on,modified_on,is_deleted) VALUES('$id','$Order','AG_$id',2,1,2,$Amount,'$Date','$fixture',20,'test-fixture',1,1,false);"|Out-Null
    $id
}
function Settle($Month){
    $from=[DateTimeOffset]::Parse("2026-$($Month.ToString('00'))-01T00:00:00Z")
    $body=@{merchantId="$fixture";fromDate=$from.ToUnixTimeSeconds();toDate=$from.AddMonths(1).AddSeconds(-1).ToUnixTimeSeconds()}
    $quote=(Call-Api Post '/finance/settlements/quote' $body $admin).data
    if(!$quote.canCreate){throw "Quote blocked: $($quote.blockingCode)"}
    $body.quoteToken=$quote.quoteToken;$body.operationId=[Guid]::NewGuid().ToString();$script:settlementKeys+=$body.operationId
    $saved=(Call-Api Post '/finance/settlements' $body $admin).data
    if((Call-Api Post '/finance/settlements' $body $admin).data.id -ne $saved.id){throw 'Settlement replay duplicated.'}
    $saved
}
try {
    foreach($role in @('Admin','Customer','Merchant')){
        $s=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data;$sessions+=$s
        Set-Variable -Name $role.ToLower() -Value $s.accessToken
        if($role -eq 'Admin'){$adminId=$s.user.id};if($role -eq 'Customer'){$customerId=$s.user.id};if($role -eq 'Merchant'){$source=$s.user.id}
    }
    $template=Sql 'SELECT id FROM washgo.orders LIMIT 1;'
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object('id','$fixture','email','agreed-$fixture@example.test','provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$source';
INSERT INTO washgo.merchants SELECT (jsonb_populate_record(NULL::washgo.merchants,to_jsonb(m)||jsonb_build_object('user_id','$fixture','tax_code',NULL,'business_name','Agreed rules fixture','commission_rate',20,'max_capacity',1,'rating',5,'status',1,'contract_end_date',NULL))).* FROM washgo.merchants m WHERE user_id='$source';
"@|Out-Null
    $m=(Call-Api Post '/auth/login' @{email="agreed-$fixture@example.test";password='Merchant@123'} $null).data;$sessions+=$m
    $profile=(Call-Api Get "/merchant/$fixture" $null $admin).data
    Call-Api Put '/merchant/' @{id="$fixture";expectedVersion=$profile.version;businessName=$profile.businessName;email=$profile.email;phone=$profile.phone;address=$profile.address;city=$profile.city;district=$profile.district;latitude=$profile.latitude;longitude=$profile.longitude;commissionRate=20;maxCapacity=1;contractStartDate='2026-01-01';contractEndDate='2026-12-31'} $admin|Out-Null
    $normalized=Sql "SELECT to_char(contract_end_date AT TIME ZONE 'Asia/Ho_Chi_Minh','YYYY-MM-DD HH24:MI') FROM washgo.merchants WHERE user_id='$fixture';"
    if($normalized -ne '2026-12-31 00:00'){throw 'Date-only merchant contract was not normalized to VN midnight.'}
    $locker=(Call-Api Post '/locker/' @{merchantId="$fixture";code="AG_$($fixture.ToString('N'))";name="Agreed $fixture";address='Development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxes=@(@{boxNumber='1';size=2})} $admin).data
    $waiting=New-FixtureOrder 'waiting'
    Sql "UPDATE washgo.orders SET status=2,merchant_id=NULL,locker_id='$($locker.id)',return_locker_id='$($locker.id)',deposited_at=NOW() WHERE id='$waiting';"|Out-Null
    $candidates=(Call-Api Get "/order-assignment/$waiting/candidates" $null $admin).data.merchants
    $candidate=$candidates|Where-Object merchantId -eq "$fixture"
    if(!$candidate -or $candidate.ratingForScore -ne 3 -or $candidate.ratingScore -ne 15 -or $candidate.algorithm -ne 'WeightedScore'){throw 'Unrated Merchant did not receive neutral score.'}
    $again=(Call-Api Get "/order-assignment/$waiting/candidates" $null $admin).data.merchants
    if(($candidates.merchantId -join ',') -ne ($again.merchantId -join ',')){throw 'Suggestion ordering is not deterministic.'}
    Sql "UPDATE washgo.merchants SET contract_end_date=(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh' WHERE user_id='$fixture';"|Out-Null
    if(!((Call-Api Get "/order-assignment/$waiting/candidates" $null $admin).data.merchants|Where-Object merchantId -eq "$fixture")){throw 'Contract blocked during its final VN day.'}
    $alerts=(Call-Api Get '/admin/alerts?type=10&take=100' $null $admin).data
    if($alerts.items.resourceId -notcontains "$fixture"){throw '30-day contract alert missing.'}
    Sql "UPDATE washgo.merchants SET contract_end_date=((now() AT TIME ZONE 'Asia/Ho_Chi_Minh')::date - 1)::timestamp AT TIME ZONE 'Asia/Ho_Chi_Minh' WHERE user_id='$fixture';"|Out-Null
    if((Call-Api Get "/order-assignment/$waiting/candidates" $null $admin).data.merchants|Where-Object merchantId -eq "$fixture"){throw 'Expired Merchant is still suggested.'}
    Assert-Status 400 Post "/order-assignment/$waiting/assign" @{merchantId="$fixture"} $admin
    if((Call-Api Get '/admin/alerts?type=11&take=100' $null $admin).data.items.resourceId -notcontains "$fixture"){throw 'Expired contract alert missing.'}
    $paidOrder=New-FixtureOrder 'finance'
    Call-Api Get "/wash-order/$paidOrder" $null $m.accessToken|Out-Null
    $janPayment=Add-Payment $paidOrder 100000 '2026-01-05T00:00:00Z'
    $jan=Settle 1
    if($jan.payoutAmount -ne 80000 -or $jan.status -ne 1){throw 'January settlement mismatch.'}
    if((Sql "SELECT settlement_id FROM washgo.payment_transactions WHERE id='$janPayment';") -ne $jan.id){throw 'Payment was not claimed by settlement.'}
    $refundId=[Guid]::NewGuid()
    Sql "INSERT INTO washgo.refund_requests(id,order_id,customer_id,amount,reason,status,refunded_at,merchant_id_snapshot,commission_rate_snapshot,financial_snapshot_basis,created_on,modified_on,is_deleted) VALUES('$refundId','$paidOrder','$customerId',100000,'Carry smoke',4,'2026-02-05T00:00:00Z','$fixture',20,'test-fixture',1,1,false);"|Out-Null
    $feb=Settle 2
    if($feb.payoutAmount -ne -80000 -or $feb.status -ne 3){throw 'Negative period was not carried forward.'}
    Assert-Status 400 Post "/finance/settlements/$($feb.id)/confirm-paid" @{reference='not-paid';expectedVersion=$feb.version} $admin
    Add-Payment $paidOrder 50000 '2026-03-05T00:00:00Z'|Out-Null
    $mar=Settle 3
    if($mar.periodPayoutAmount -ne 40000 -or $mar.carryForwardAmount -ne -80000 -or $mar.payoutAmount -ne -40000){throw 'March carry calculation mismatch.'}
    if((Call-Api Get "/finance/settlements/$($feb.id)" $null $admin).data.carriedToSettlementId -ne $mar.id){throw 'Debt source not consumed.'}
    Add-Payment $paidOrder 50000 '2026-04-05T00:00:00Z'|Out-Null
    $apr=Settle 4
    if($apr.payoutAmount -ne 0 -or $apr.status -ne 4 -or $apr.carryForwardAmount -ne -40000){throw 'Zero period did not close without payout.'}
    Add-Payment $paidOrder 100000 '2026-05-05T00:00:00Z'|Out-Null
    $may=Settle 5
    if($may.payoutAmount -ne 80000 -or $may.carryForwardAmount -ne 0){throw 'Debt was deducted twice.'}
    $immutable=(Call-Api Get "/finance/settlements/$($jan.id)/lines" $null $admin)
    if($immutable.list[0].collected -ne 100000 -or $immutable.list[0].refunded -ne 0){throw 'Old period snapshot changed.'}
    $noPayment=Settle 6
    if($noPayment.payoutAmount -ne 0 -or $noPayment.status -ne 4){throw 'Settled transactions were counted a second time.'}
    Add-Payment $paidOrder 100000 '2026-07-05T00:00:00Z'|Out-Null
    $julyBody=@{merchantId="$fixture";fromDate=[DateTimeOffset]::Parse('2026-07-01T00:00:00Z').ToUnixTimeSeconds();toDate=[DateTimeOffset]::Parse('2026-07-31T23:59:59Z').ToUnixTimeSeconds()}
    $julyBody.quoteToken=(Call-Api Post '/finance/settlements/quote' $julyBody $admin).data.quoteToken
    $jobs=@(foreach($attempt in 1..2){Start-Job -ScriptBlock {
        param($Url,$Token,$Body)
        try{Invoke-RestMethod -Method Post -Uri "$Url/finance/settlements" -Headers @{Authorization="Bearer $Token"} -ContentType 'application/json' -Body ($Body|ConvertTo-Json)|Out-Null;201}
        catch{[int]$_.Exception.Response.StatusCode}
    } -ArgumentList $BaseUrl,$admin,$julyBody})
    $codes=@($jobs|Receive-Job -Wait -AutoRemoveJob)
    if((($codes|Sort-Object)-join ',') -ne '201,409'){throw "Concurrent settlement returned $codes"}
    # A pending complaint is only flagged, never automatically resolved.
    $claimOrder=New-FixtureOrder 'claim';$claim=[Guid]::NewGuid();$incidents+=$claim
    Sql "INSERT INTO washgo.incidents(id,order_id,order_code,customer_id,customer_name,type,severity,description,image_urls,video_urls,status,compensation_level,item_value,declared_item_value,compensation_amount,compensation_status,created_on,modified_on,is_deleted,revision) VALUES('$claim','$claimOrder','AG_CLAIM','$customerId','Fixture',1,1,'Claim fixture',ARRAY[]::text[],ARRAY[]::text[],1,0,100000,100000,0,0,extract(epoch FROM now()-interval '49 hours')::bigint,1,false,0);"|Out-Null
    if((Call-Api Get '/admin/alerts?type=9&take=100' $null $admin).data.items.resourceId -notcontains "$claim"){throw 'Pending 48-hour alert missing.'}
    if((Call-Api Get "/incident/$claim" $null $admin).data.status -ne 1){throw 'SLA alert mutated incident.'}
    Assert-Status 400 Post '/incident/' @{orderId="$claimOrder";type=1;severity=1;description='No image';imageUrls=@();videoUrls=@();itemValue=100001000} $customer
    $proof=Upload-Proof $admin
    $view=(Call-Api Get "/incident/$claim" $null $admin).data;$settings=(Call-Api Get '/system-setting' $null $admin).data
    $resolved=(Call-Api Post "/incident/$claim/resolve" @{expectedVersion=$view.version;expectedSettingsVersion=$settings.version;resolutionNote='Agreed fixture';compensationLevel=1;itemValue=90000} $admin).data
    if($resolved.declaredItemValue -ne 100000 -or $resolved.itemValue -ne 90000){throw 'Admin overwrote customer declaration.'}
    Assert-Status 400 Post "/incident/$claim/confirm-compensation" @{expectedVersion=$resolved.version;reference=''} $admin
    $body=@{expectedVersion=$resolved.version;reference='BANK-AGREED';proofImageUrls=@($proof);operationId=[Guid]::NewGuid().ToString()}
    $paid=(Call-Api Post "/incident/$claim/confirm-compensation" $body $admin).data
    if($paid.compensationPaidBy -ne $adminId -or !$paid.compensationPaidAt -or $paid.compensationProofImageUrls[0] -ne $proof){throw 'Compensation proof/actor not persisted.'}
    Call-Api Post "/incident/$claim/confirm-compensation" $body $admin|Out-Null
    Assert-Status 409 Post "/incident/$claim/confirm-compensation" @{reference='different';proofImageUrls=@($proof)} $admin
    Assert-Status 400 Post "/incident/$claim/confirm-compensation" @{reference='BANK-AGREED';proofImageUrls=@('bad1','bad2','bad3','bad4')} $admin
    $customerPhoto=Upload-Proof $customer
    Sql @"
INSERT INTO washgo.order_status_history SELECT (jsonb_populate_record(NULL::washgo.order_status_history,to_jsonb(h)||jsonb_build_object('id',gen_random_uuid(),'order_id','$claimOrder','to_status',12,'from_status',11,'created_at',extract(epoch FROM now())::bigint))).* FROM washgo.order_status_history h LIMIT 1;
"@|Out-Null
    Assert-Status 400 Post '/incident/' @{orderId="$claimOrder";type=1;severity=1;description='Missing photo';itemValue=100000} $customer
    $newClaim=(Call-Api Post '/incident/' @{orderId="$claimOrder";type=1;severity=1;description='Customer new claim';imageUrls=@($customerPhoto);itemValue=150000} $customer).data
    $incidents+=$newClaim.id
    if($newClaim.declaredItemValue -ne 150000){throw 'Declared value missing on created claim.'}
    $rejected=(Call-Api Post "/incident/$($newClaim.id)/reject" @{reason='Fixture rejection';expectedVersion=$newClaim.version} $admin).data
    Call-Api Post "/incident/$($newClaim.id)/messages" @{content='Admin may still answer';expectedVersion=$rejected.version} $admin|Out-Null
    Assert-Status 400 Post "/incident/$($newClaim.id)/messages" @{content='Customer terminal thread blocked'} $customer
    $replacement=(Call-Api Post '/incident/' @{orderId="$claimOrder";type=1;severity=1;description='Replacement after rejection';imageUrls=@($customerPhoto);itemValue=150000} $customer).data
    $incidents+=$replacement.id
    if($replacement.id -eq $newClaim.id){throw 'Replacement reopened the old claim.'}
    Write-Output 'PASS: VN inclusive contract boundary/alerts/new-assignment guard, neutral deterministic score, negative/zero/positive periods, one-time source/debt claims, immutable history, pending SLA and compensation proof/actor.'
} catch {Write-Output $_.ScriptStackTrace; if($_.ErrorDetails.Message){try{Write-Output (($_.ErrorDetails.Message|ConvertFrom-Json).message)}catch{}};throw}
finally {
    foreach($claim in $incidents){Sql "DELETE FROM washgo.incident_messages WHERE incident_id='$claim'; DELETE FROM washgo.incidents WHERE id='$claim';"|Out-Null}
    Sql "DELETE FROM washgo.merchant_settlements WHERE merchant_id='$fixture';"|Out-Null
    foreach($id in $orders){Sql "DELETE FROM washgo.refund_requests WHERE order_id='$id'; DELETE FROM washgo.payment_transactions WHERE order_id='$id'; DELETE FROM washgo.orders WHERE id='$id';"|Out-Null}
    if($locker){Sql "DELETE FROM washgo.locker_merchants WHERE locker_id='$($locker.id)'; DELETE FROM washgo.locker_slots WHERE locker_id='$($locker.id)'; DELETE FROM washgo.lockers WHERE id='$($locker.id)';"|Out-Null}
    Sql "DELETE FROM washgo.merchants WHERE user_id='$fixture'; DELETE FROM washgo.users WHERE id='$fixture';"|Out-Null
    foreach($s in $sessions){try{Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken|Out-Null}catch{}}
}
