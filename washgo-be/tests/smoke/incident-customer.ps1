param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: Customer self-service on incidents. A fixture order is marked Completed by SQL (status + history row);
# everything else goes through the API.
$sessions=@{}; $lockerId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$customerB=[Guid]::NewGuid()

Add-Type -AssemblyName System.Net.Http
function Upload-Png($Token) {
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)
        $bytes=New-Object byte[] 64
        [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | ForEach-Object -Begin {$i=0} -Process {$bytes[$i]=$_;$i++}
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $form.Add((New-Object System.Net.Http.ByteArrayContent(,$bytes)),'file','e.png')
        (($client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result) | ConvertFrom-Json).data.url
    } finally { $client.Dispose() }
}
function New-Order {
    $l=(Call-Api Get "/customer/lockers/$lockerId" $null $customer).data
    (Call-Api Post '/wash-order/' @{lockerId="$lockerId";boxId=$l.availableSlots[0].id;paymentMethod=2;items=@(@{serviceId="$serviceId";quantity=1})} $customer).data
}
function Complete($Id) {
    Sql @"
UPDATE washgo.orders SET status=12, merchant_id='$merchantId' WHERE id='$Id';
INSERT INTO washgo.order_status_history SELECT (jsonb_populate_record(NULL::washgo.order_status_history,to_jsonb(h)||jsonb_build_object(
'id','$([Guid]::NewGuid())','to_status',12,'from_status',11,'created_at',extract(epoch FROM now())::bigint,'note','Incident smoke completed'))).*
FROM washgo.order_status_history h WHERE order_id='$Id' LIMIT 1;
"@ | Out-Null
}
function Count-Type($Token,$Type,$LinkId) { @((Call-Api Get '/notification?take=100' $null $Token).list | Where-Object { $_.type -eq $Type -and $_.linkId -eq "$LinkId" }).Count }

try {
    foreach($role in @('Admin','Merchant','Customer')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$customer=$sessions.Customer.accessToken
    $merchantId=$sessions.Merchant.user.id
    $seedCustomer=$sessions.Customer.user.id
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','incident-b-$run@example.com','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
"@ | Out-Null
    $sessions.B=(Call-Api Post '/auth/login' @{email="incident-b-$run@example.com";password='Customer@123'} $null).data; $tokenB=$sessions.B.accessToken
    $locker=(Call-Api Post '/locker/' @{merchantId="$merchantId";name="Incident Locker $run";address='Private development fixture';district='Test';city='Test';latitude=10.77;longitude=106.69;boxCount=3} $admin).data
    $lockerId=[Guid]$locker.id
    $serviceId=(Call-Api Post '/customer/services/filter' @{page=1;take=1} $customer).list[0].id
    $o=New-Order; $pending=New-Order
    Complete $o.id
    $img=Upload-Png $customer; $imgB=Upload-Png $tokenB

    Write-Output '1. Eligibility by order'
    $e=(Call-Api Get "/incident/eligibility/$($o.id)" $null $customer).data
    $deadline=([DateTimeOffset]::Parse($e.reportDeadline)).ToUnixTimeSeconds(); $now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    if(!$e.canReport -or $e.reason -or [Math]::Abs($deadline-($now+3*86400)) -gt 120 -or $e.pendingIncidentId){throw 'Eligibility of a completed order wrong.'}
    $p=(Call-Api Get "/incident/eligibility/$($pending.id)" $null $customer).data
    if($p.canReport -or !$p.reason){throw 'Pending order should not be reportable.'}
    Assert-Status 404 Get "/incident/eligibility/$($o.id)" $null $tokenB

    Write-Output '2. Idempotent create by clientRequestId'
    $req="req-$run"
    $body=@{orderId=$o.id;type=1;severity=2;description='Ao bi rach';imageUrls=@($img);clientRequestId=$req}
    $first=Call-Api Post '/incident/' $body $customer
    $again=Call-Api Post '/incident/' $body $customer
    if($again.data.id -ne $first.data.id -or !$again.message -or (Sql "SELECT count(*) FROM washgo.incidents WHERE order_id='$($o.id)';") -ne '1'){throw 'Replay created a second incident.'}
    $id=$first.data.id
    if((Call-Api Get "/incident/mine/request/$req" $null $customer).data.id -ne $id){throw 'Lookup by request id failed.'}
    Assert-Status 404 Get "/incident/mine/request/$req" $null $tokenB
    Assert-Status 400 Post '/incident/' @{orderId=$o.id;type=1;severity=2;description='x';imageUrls=@($img);clientRequestId=('x'*65)} $customer
    $e2=(Call-Api Get "/incident/eligibility/$($o.id)" $null $customer).data
    if($e2.canReport -or $e2.pendingIncidentId -ne $id -or $e2.incidentIds[0] -ne $id){throw 'Eligibility with a pending incident wrong.'}

    Write-Output '3. Customer filters'
    if((Call-Api Post '/incident/mine/filter' @{statuses=@(1);keyword=$o.orderCode} $customer).total -ne 1){throw 'Status/keyword filter wrong.'}
    if((Call-Api Post '/incident/mine/filter' @{types=@(2);keyword=$o.orderCode} $customer).total -ne 0){throw 'Type filter wrong.'}
    Assert-Status 400 Post '/incident/mine/filter' @{sort='random'} $customer
    $emptyB=Call-Api Post '/incident/mine/filter' @{} $tokenB
    if($emptyB.total -ne 0 -or !$emptyB.message){throw 'Other customer sees incidents.'}

    Write-Output '4. Update while pending; another customer cannot'
    Assert-Status 400 Put "/incident/$id" @{type=1;severity=3;description='Rach nang'} $customer
    Assert-Status 400 Put "/incident/$id" @{type=1;severity=3;description='Rach nang';imageUrls=@($imgB)} $customer
    Assert-Status 404 Put "/incident/$id" @{type=1;severity=3;description='Rach nang';imageUrls=@($imgB)} $tokenB
    $u=(Call-Api Put "/incident/$id" @{type=1;severity=3;description='Rach nang o tay ao';imageUrls=@($img)} $customer).data
    if($u.severity -ne 3 -or $u.description -ne 'Rach nang o tay ao' -or !$u.customerUpdatedAt -or !$u.canEdit){throw 'Update not stored.'}
    if((Count-Type $admin 17 $id) -lt 1){throw 'Admin not told about the update.'}

    Write-Output '5. Exchange messages with extra evidence'
    $m1=Call-Api Post "/incident/$id/messages" @{content='Bo sung anh moi';imageUrls=@($img)} $customer
    if(!$m1.data.isMine -or $m1.data.senderRole -ne 'Customer' -or $m1.data.imageUrls[0] -ne $img){throw 'Customer message wrong.'}
    Assert-Status 400 Post "/incident/$id/messages" @{content=''} $customer
    Assert-Status 404 Post "/incident/$id/messages" @{content='x'} $tokenB
    Call-Api Post "/incident/$id/messages" @{content='Admin da tiep nhan'} $admin | Out-Null
    $notes=@((Call-Api Get '/notification?take=100' $null $customer).list | Where-Object { $_.type -eq 17 -and $_.linkId -eq "$id" })
    if($notes.Count -ne 1 -or $notes[0].messageKey -ne 'incidentMessage.customer'){throw 'Customer not told about the Admin reply.'}
    $thread=(Call-Api Get "/incident/$id/messages" $null $customer).data
    if($thread.Count -ne 2 -or $thread[0].senderRole -ne 'Customer' -or $thread[1].senderRole -ne 'Admin' -or $thread[1].isMine){throw 'Thread wrong.'}
    Assert-Status 404 Get "/incident/$id/messages" $null $tokenB

    Write-Output '6. Withdraw: idempotent, then locked; the order can be reported again'
    $w=Call-Api Post "/incident/$id/withdraw" @{reason='Da tu xu ly'} $customer
    if($w.data.status -ne 4 -or !$w.data.withdrawnAt -or $w.data.withdrawReason -ne 'Da tu xu ly' -or $w.data.canEdit){throw 'Withdraw failed.'}
    if(!(Call-Api Post "/incident/$id/withdraw" @{} $customer).message){throw 'Withdraw replay failed.'}
    Assert-Status 400 Put "/incident/$id" @{type=1;severity=3;description='x';imageUrls=@($img)} $customer
    Assert-Status 400 Post "/incident/$id/messages" @{content='x'} $customer
    Assert-Status 400 Post "/incident/$id/reject" @{reason='x'} $admin
    if(!(Call-Api Get "/incident/eligibility/$($o.id)" $null $customer).data.canReport){throw 'Withdrawn incident still blocks reporting.'}

    Write-Output 'PASS: report eligibility per order (deadline, pending incident, other customer 404), idempotent create by clientRequestId with lookup, customer filters, update while pending with evidence rules and Admin notice, message thread with evidence and notices both ways, withdraw (idempotent, locks edits and Admin review, order reportable again).'
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
