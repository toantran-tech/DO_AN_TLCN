param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: Customer inbox filters/counts, message keys for translation, and read/delete sync across devices.
# A fresh Customer is cloned; extra notifications are cloned from its real order notification with other types/dates.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$customerB=[Guid]::NewGuid(); $email="notify-c-$run@example.com"

function Find-Inbox([hashtable]$Body, $Token=$deviceA) { (Call-Api Post '/notification/filter' $Body $Token).data }
function Clone-Note($Source, $Type, $Title, $DaysAgo, $Read='false') {
    $id=[Guid]::NewGuid()
    Sql @"
INSERT INTO washgo.notifications SELECT (jsonb_populate_record(NULL::washgo.notifications,to_jsonb(n)||jsonb_build_object(
'id','$id','type',$Type,'title','$Title','content','Smoke $Title','message_key',NULL,'message_args',NULL,'is_read',$Read,
'created_on',extract(epoch FROM now() - interval '$DaysAgo days')::bigint))).* FROM washgo.notifications n WHERE id='$Source';
"@ | Out-Null
    return $id
}

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $seedMerchantId=[Guid]$sessions.Merchant.user.id
    $seedCustomer=Sql "SELECT id FROM washgo.users WHERE email='customer@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','$email','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
"@ | Out-Null
    $sessions.A=(Call-Api Post '/auth/login' @{email=$email;password='Customer@123'} $null).data; $deviceA=$sessions.A.accessToken
    $sessions.B=(Call-Api Post '/auth/login' @{email=$email;password='Customer@123'} $null).data; $deviceB=$sessions.B.accessToken

    $empty=Call-Api Post '/notification/filter' @{} $deviceA
    if($empty.data.total -ne 0 -or !$empty.message -or $empty.data.counts.Count -ne 5){throw 'Empty inbox message/counts missing.'}
    $locker=(Call-Api Post '/locker/' @{merchantId="$seedMerchantId";name="Notify Filter Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=1} $admin).data
    $lockerId=[Guid]$locker.id
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $deviceA).data
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $deviceA).list[0].id
    $order=(Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $deviceA).data
    $created=Sql "SELECT id FROM washgo.notifications WHERE user_id='$customerB' AND link_id='$($order.id)';"
    $pay=Clone-Note $created 6 "ZZPAY$run" 1
    $feedback=Clone-Note $created 12 "ZZFEED$run" 2
    $old=Clone-Note $created 29 "ZZOLD$run" 10

    Write-Output '1. Message key and arguments for translation; default key for older rows'
    $all=Find-Inbox @{take=50}
    $o=$all.items | Where-Object { $_.type -eq 1 }
    if($o.messageKey -ne 'orderCreated.customer' -or $o.messageArgs.orderCode -ne $order.orderCode -or $o.messageArgs.boxNumber -ne $order.boxNumber -or $o.group -ne 'order'){throw "Order message key/args wrong: $($o.messageKey)"}
    $p=$all.items | Where-Object { $_.id -eq "$pay" }
    if($p.messageKey -ne 'notification.paymentSucceeded' -or $p.group -ne 'payment'){throw 'Default key/group wrong.'}

    Write-Output '2. Counts by group cover the whole inbox'
    $c=@{}; foreach($row in $all.counts){ $c[$row.group]=$row }
    if($all.total -ne 4 -or $all.unreadTotal -ne 4 -or $c.order.total -ne 1 -or $c.payment.total -ne 1 -or $c.feedback.total -ne 1 -or $c.system.total -ne 1 -or $c.promotion.total -ne 0){throw 'Group counts wrong.'}

    Write-Output '3. Keyword, group, type, date range and sort'
    if((Find-Inbox @{keyword="zzpay$run"}).total -ne 1){throw 'Keyword (case-insensitive) wrong.'}
    $g=Find-Inbox @{groups=@('payment','feedback')}
    if($g.total -ne 2 -or @($g.items | Where-Object { $_.group -notin @('payment','feedback') }).Count -ne 0 -or $g.counts.Count -ne 5){throw 'Group filter wrong.'}
    if((Find-Inbox @{types=@(12)}).total -ne 1){throw 'Type filter wrong.'}
    $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    if((Find-Inbox @{fromDate=$now-5*86400;toDate=$now+60}).total -ne 3){throw 'Date range wrong.'}
    if((Find-Inbox @{sort='oldest'}).items[0].id -ne "$old"){throw 'Oldest sort wrong.'}
    Assert-Status 400 Post '/notification/filter' @{groups=@('marketing')} $deviceA
    Assert-Status 400 Post '/notification/filter' @{sort='random'} $deviceA
    Assert-Status 400 Post '/notification/filter' @{fromDate=$now;toDate=$now-60} $deviceA
    $none=Call-Api Post '/notification/filter' @{keyword="nothing-$run"} $deviceA
    if($none.data.total -ne 0 -or !$none.message){throw 'No-match message missing.'}
    if((Find-Inbox @{} $admin).items | Where-Object { $_.id -eq "$pay" }){throw 'Another account sees the Customer notifications.'}

    Write-Output '4. Read/delete on device A is pushed to device B (SignalR notificationState)'
    $out=Join-Path ([IO.Path]::GetTempPath()) "washgo-state-$run.txt"
    $listener=Start-Process node -ArgumentList "`"$(Join-Path $PSScriptRoot 'signalr-listen.cjs')`"",$BaseUrl,$deviceB,'20000','notificationState','3' -NoNewWindow -PassThru -RedirectStandardOutput $out
    $deadline=(Get-Date).AddSeconds(15)
    while(!(Test-Path $out) -or !((Get-Content $out -Raw) -match 'CONNECTED')){
        if((Get-Date) -gt $deadline -or $listener.HasExited){throw "Listener did not connect: $(Get-Content $out -Raw)"}
        Start-Sleep -Milliseconds 200
    }
    Call-Api Post "/notification/$pay/read" $null $deviceA | Out-Null
    Call-Api Delete "/notification/$feedback" $null $deviceA | Out-Null
    Call-Api Post '/notification/read-all' $null $deviceA | Out-Null
    if(!$listener.WaitForExit(20000)){$listener.Kill();throw 'Listener timed out.'}
    $events=@(Get-Content $out | Where-Object { $_ -like '{*' } | ForEach-Object { $_ | ConvertFrom-Json })
    Remove-Item $out -ErrorAction SilentlyContinue
    if($events.Count -ne 3 -or $events[0].action -ne 'read' -or $events[0].ids[0] -ne "$pay" -or $events[0].unreadCount -ne 3 -or
       $events[1].action -ne 'deleted' -or $events[1].unreadCount -ne 2 -or $events[2].action -ne 'readAll' -or $events[2].unreadCount -ne 0){throw "State events wrong: $($events | ConvertTo-Json -Compress)"}
    if((Find-Inbox @{} $deviceB).unreadTotal -ne 0){throw 'Device B still sees unread notifications.'}

    Write-Output 'PASS: messageKey/messageArgs/group on notifications (default key for older rows), whole-inbox counts per group, keyword/group/type/date filters, oldest sort, validation and no-match message, inbox isolation, read/delete/read-all pushed to the other device with the unread count.'
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
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$customerB';" | Out-Null
}
