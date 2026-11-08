param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: Admin cancels a pending order whose deposit deadline passed (quote, filter, guard, box release, replay).
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$standardWash='b1111111-bbbb-1111-bbbb-111111111111'
$http=New-Object System.Net.Http.HttpClient
function Send($Method,$Path,$Body,$Token){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    if($null -ne $Body){ $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json') }
    $r=$http.SendAsync($req).Result; $t=$r.Content.ReadAsStringAsync().Result
    @{ Status=[int]$r.StatusCode; Json=$(if($t){$t|ConvertFrom-Json}else{$null}) }
}

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken;$merchant=$sessions.Merchant.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$($sessions.Merchant.user.id)";name="Expired Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=2} $admin).data
    $lockerId=[Guid]$locker.id
    $o=@()
    foreach($i in 0..1){
        $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
        $o+=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId=$standardWash;quantity=1})} $customer).data
    }
    Sql "UPDATE washgo.orders SET merchant_order_code='EXP$run'||RIGHT(order_code,8) WHERE locker_id='$lockerId';" | Out-Null
    $kw="exp$run"

    Write-Output '1. Not yet expired: quote says no, cancel refused'
    $q=(Call-Api Get "/wash-order/$($o[0].id)/admin-cancellation-quote" $null $admin).data
    if($q.canCancel -or $q.reasonCode -ne 'NotExpired' -or !$q.depositDeadline -or !$q.serverNow){throw 'Quote for a live order wrong.'}
    $x=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason='Qua han'} $admin
    if($x.Status -ne 400 -or $x.Json.meta.code -ne 'NotExpired'){throw "Live order cancelled: $($x.Status)"}

    Write-Output '2. Expired: filter/summary, allowedActions, quote'
    Sql "UPDATE washgo.orders SET deposit_deadline=now() AT TIME ZONE 'utc' - interval '1 hour', created_on=created_on-7200 WHERE id='$($o[0].id)';" | Out-Null
    $list=Call-Api Post '/wash-order/filter' @{keyword=$kw;overdueType='DepositExpired';page=1;take=10} $admin
    $sum=(Call-Api Post '/wash-order/summary' @{keyword=$kw;overdueType='DepositExpired'} $admin).data
    if($list.total -ne 1 -or $list.list[0].id -ne $o[0].id -or $sum.total -ne 1 -or $sum.statusCounts.pending -ne 1){throw "Overdue filter wrong: $($list.total)/$($sum.total)"}
    if((Call-Api Post '/wash-order/summary' @{keyword=$kw} $admin).data.total -ne 2){throw 'Summary without filter wrong.'}
    $d=(Call-Api Get "/wash-order/$($o[0].id)" $null $admin).data
    if(!$d.allowedActions.canCancelExpiredDeposit -or $d.allowedActions.cancelExpiredDepositReasonCode){throw 'allowedActions wrong.'}
    $q=(Call-Api Get "/wash-order/$($o[0].id)/admin-cancellation-quote" $null $admin).data
    if(!$q.canCancel -or $q.paidAmount -ne 0 -or $q.feeOutstanding -ne $q.feeAmount -or $q.revision -ne $d.revision){throw "Quote wrong: $($q | ConvertTo-Json -Compress)"}

    Write-Output '3. Validation, scope, quote/revision changes'
    Assert-Status 400 Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason=' '} $admin
    Assert-Status 403 Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason='x'} $merchant
    Assert-Status 403 Get "/wash-order/$($o[0].id)/admin-cancellation-quote" $null $customer
    $x=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason='Qua han';expectedFeeAmount=($q.feeAmount+1000)} $admin
    if($x.Status -ne 409 -or $x.Json.meta.code -ne 'QuoteChanged'){throw "Changed quote accepted: $($x.Status)"}
    $x=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason='Qua han';expectedRevision=($d.revision-1)} $admin
    if($x.Status -ne 409 -or $x.Json.meta.code -ne 'RevisionMismatch'){throw 'Stale revision accepted.'}

    Write-Output '4. Cancel: box released, history, notification; replay and reused key'
    $k=[Guid]::NewGuid()
    $body=@{reason='Khach khong gui do trong thoi han';expectedFeeAmount=$q.feeAmount;expectedRevision=$d.revision;operationId=$k}
    $c1=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" $body $admin
    $c2=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" $body $admin
    if($c1.Status -ne 200 -or $c1.Json.data.status -ne 99 -or $c1.Json.data.cancelFeeAmount -ne $q.feeAmount -or $c2.Status -ne 200 -or !$c2.Json.meta.replayed){throw "Cancel/replay wrong: $($c1.Status)/$($c2.Status) $($c1.Json.message)"}
    $c3=Send Post "/wash-order/$($o[0].id)/cancel-expired-deposit" @{reason='Ly do khac';operationId=$k} $admin
    if($c3.Status -ne 409 -or $c3.Json.meta.code -ne 'OperationIdReused'){throw 'Reused key accepted.'}
    $box=Sql "SELECT s.status FROM washgo.locker_slots s JOIN washgo.orders o ON o.locker_slot_id=s.id WHERE o.id='$($o[0].id)';"
    if($box -ne '1'){throw "Box not released: $box"}
    $other=Sql "SELECT s.status FROM washgo.locker_slots s JOIN washgo.orders o ON o.locker_slot_id=s.id WHERE o.id='$($o[1].id)';"
    if($other -ne '3'){throw "Another order's reserved box changed: $other"}
    $tl=(Call-Api Get "/wash-order/$($o[0].id)" $null $admin).data.timeline
    if(@($tl | Where-Object { $_.status -eq 99 -and $_.actorRole -eq 'Admin' }).Count -ne 1){throw 'Cancel recorded twice or missing.'}
    $notes=@((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.linkId -eq $o[0].id -and $_.type -eq 10 })
    if($notes.Count -ne 1){throw "Customer notified $($notes.Count) times."}
    if((Call-Api Get "/wash-order/$($o[0].id)/admin-cancellation-quote" $null $admin).data.reasonCode -ne 'NotPending'){throw 'Cancelled order still cancellable.'}

    Write-Output 'PASS: live order not cancellable (NotExpired); overdueType=DepositExpired filter on list and summary; allowedActions.canCancelExpiredDeposit; read-only quote with deadline/serverNow/fee/paid/refund/outstanding/revision; reason required, Admin only; changed fee 409 QuoteChanged, stale revision 409; cancel releases only this order''s reserved box, records Admin history once, notifies the customer once; replay with the same operationId does not cancel again, reused key 409.'
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
    $http.Dispose()
}
