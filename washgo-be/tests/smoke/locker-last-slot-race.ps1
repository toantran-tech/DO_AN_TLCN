param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db',
    [ValidateRange(1, 20)][int]$Rounds = 5
)
$target = [Uri]$BaseUrl
if (-not $target.IsLoopback) { throw 'Use a local development/CI Backend only.' }
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
Add-Type -AssemblyName System.Net.Http

# Every write and cleanup is scoped to generated fixture IDs. Existing users,
# lockers, orders and prices are never changed; the source Customer hash stays in SQL.
$run = [Guid]::NewGuid().ToString('N')
$lockerId = [Guid]::NewGuid(); $slotId = [Guid]::NewGuid()
$customerIds = @([Guid]::NewGuid(), [Guid]::NewGuid())
$sessions = @(); $holdJob = $null; $http = $null
try {
    $template = (Sql "SELECT l.id FROM washgo.lockers l WHERE NOT l.is_deleted AND l.status=1 AND EXISTS (SELECT 1 FROM washgo.locker_slots s WHERE s.locker_id=l.id AND NOT s.is_deleted) AND EXISTS (SELECT 1 FROM washgo.locker_merchants lm JOIN washgo.merchants m ON m.user_id=lm.merchant_id WHERE lm.locker_id=l.id AND m.status=1) LIMIT 1;").Trim()
    if (!$template) { throw 'An active locker with an active Merchant is required.' }
    $template = [Guid]$template
    foreach ($i in 0..1) {
        $id = $customerIds[$i]; $email = "race-$run-$i@example.invalid"
        Sql @"
BEGIN;
INSERT INTO washgo.users
SELECT (jsonb_populate_record(NULL::washgo.users, to_jsonb(u) || jsonb_build_object(
 'id','$id','email','$email','full_name','Slot Race Customer $i',
 'auth_provider',1,'provider_id',NULL,'is_active',true,'last_login_at',NULL))).*
FROM washgo.users u WHERE email='customer@washgo.vn';
INSERT INTO washgo.customers
SELECT (jsonb_populate_record(NULL::washgo.customers, to_jsonb(c) || jsonb_build_object(
 'user_id','$id','loyalty_points',0,'total_orders',0,'total_spent',0))).*
FROM washgo.customers c JOIN washgo.users u ON u.id=c.user_id WHERE u.email='customer@washgo.vn';
COMMIT;
"@ | Out-Null
        $session = (Call-Api Post '/auth/login' @{email=$email;password='Customer@123';portal='Customer'} $null).data
        if (!$session.accessToken -or "$($session.user.id)" -ne "$id") { throw 'Fixture login failed.' }
        $sessions += $session
    }
    Sql @"
BEGIN;
INSERT INTO washgo.lockers
SELECT (jsonb_populate_record(NULL::washgo.lockers, to_jsonb(l) || jsonb_build_object(
 'id','$lockerId','code','RACE_$run','name','Last slot race fixture','images',NULL,
 'total_slots',1,'available_slots',1,'is_deleted',false,'status',1))).*
FROM washgo.lockers l WHERE id='$template';
INSERT INTO washgo.locker_slots
SELECT (jsonb_populate_record(NULL::washgo.locker_slots, to_jsonb(s) || jsonb_build_object(
 'id','$slotId','locker_id','$lockerId','slot_number',1,'status',1,'is_deleted',false))).*
FROM washgo.locker_slots s WHERE locker_id='$template' AND NOT is_deleted LIMIT 1;
INSERT INTO washgo.locker_merchants
SELECT (jsonb_populate_record(NULL::washgo.locker_merchants, to_jsonb(lm) || jsonb_build_object(
 'id',gen_random_uuid(),'locker_id','$lockerId'))).*
FROM washgo.locker_merchants lm WHERE locker_id='$template';
COMMIT;
"@ | Out-Null
    $service = (Call-Api Post '/customer/services/filter' @{page=1;take=1} $sessions[0].accessToken).list[0]
    if (!$service) { throw 'An approved service is required.' }
    $item = @{serviceId=$service.id;quantity=1}
    if ($service.unit -eq 'kg') { $item.weightKg=1 }
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $http = New-Object System.Net.Http.HttpClient($handler)
    $http.Timeout = [TimeSpan]::FromSeconds(30)
    for ($round=1; $round -le $Rounds; $round++) {
        foreach ($session in $sessions) {
            $view = (Call-Api Get "/customer/lockers/$lockerId" $null $session.accessToken).data
            if ($view.availableSlotCount -ne 1 -or @($view.availableSlots).Count -ne 1) { throw 'Fixture must have exactly one free slot.' }
        }
        $requests = @(); $responses = @()
        try {
            foreach ($session in $sessions) {
                $json = @{lockerId="$lockerId";boxId="$slotId";items=@($item);paymentMethod=2;customerNote="Last-slot race $run round $round"} | ConvertTo-Json -Depth 8
                $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, "$BaseUrl/wash-order/")
                $request.Headers.Authorization = New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer', $session.accessToken)
                $request.Content = New-Object System.Net.Http.StringContent($json, [Text.Encoding]::UTF8, 'application/json')
                $requests += $request
            }
            # Hold only the fixture locker briefly. Both HTTP requests are started
            # before release, forcing a real overlap instead of sequential retries.
            $marker = "RACE_HOLD_${run}_$round"
            $holdJob = Start-Job -ScriptBlock {
                param($Container,$User,$Db,$Locker,$Marker)
                docker exec $Container psql -U $User -d $Db -v ON_ERROR_STOP=1 -At -c "/* $Marker */ BEGIN; SELECT id FROM washgo.lockers WHERE id='$Locker' FOR UPDATE; SELECT pg_sleep(4); COMMIT;" | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Fixture lock failed.' }
            } -ArgumentList $PostgresContainer,$PostgresUser,$Database,"$lockerId",$marker
            $held = $false
            for ($attempt=0; $attempt -lt 60; $attempt++) {
                if ((Sql "SELECT count(*) FROM pg_stat_activity WHERE wait_event='PgSleep' AND query LIKE '%$marker%';").Trim() -eq '1') { $held=$true; break }
                Start-Sleep -Milliseconds 100
            }
            if (!$held) { throw 'Could not synchronize fixture row lock.' }
            $first = $http.SendAsync($requests[0])
            $second = $http.SendAsync($requests[1])
            Start-Sleep -Milliseconds 200
            if ($first.IsCompleted -or $second.IsCompleted) { throw 'Requests did not overlap behind the fixture lock.' }
            $responses = @($first.GetAwaiter().GetResult(), $second.GetAwaiter().GetResult())
            $codes = @($responses | ForEach-Object { [int]$_.StatusCode })
            if (($codes | Sort-Object) -join ',' -ne '201,400') {
                $messages = $responses | ForEach-Object { $_.Content.ReadAsStringAsync().GetAwaiter().GetResult() }
                throw "Expected one 201 and one 400; received $codes. Responses: $messages"
            }
            $stats = (Sql "SELECT (SELECT count(*) FROM washgo.orders WHERE locker_id='$lockerId' AND status=1 AND NOT is_deleted),available_slots,(SELECT status FROM washgo.locker_slots WHERE id='$slotId'),(SELECT count(*) FROM washgo.orders WHERE locker_id='$lockerId' AND NOT is_deleted) FROM washgo.lockers WHERE id='$lockerId';").Trim()
            if ($stats -ne "1|0|3|$round") { throw "Order/slot/counter mismatch: $stats" }
            $winner = if ($codes[0] -eq 201) { 0 } else { 1 }
            $order = ($responses[$winner].Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).data
            if ("$($order.customerId)" -ne "$($customerIds[$winner])") { throw 'Winning order has the wrong Customer.' }
            Write-Output "Round $round PASS: two distinct Customers overlapped; HTTP $($codes[0])/$($codes[1]); one order, slot Reserved, available=0."
            # Cancel through the actual API: the same fixture slot must be released once.
            $null = Call-Api Post "/wash-order/$($order.id)/cancel" @{reason='Last-slot race fixture cleanup'} $sessions[$winner].accessToken
            $restored = (Sql "SELECT available_slots,(SELECT status FROM washgo.locker_slots WHERE id='$slotId') FROM washgo.lockers WHERE id='$lockerId';").Trim()
            if ($restored -ne '1|1') { throw "Cancellation did not release the fixture once: $restored" }
        }
        finally {
            if ($holdJob) { $holdJob | Receive-Job -Wait -AutoRemoveJob | Out-Null; $holdJob=$null }
            foreach ($response in $responses) { $response.Dispose() }
            foreach ($request in $requests) { $request.Dispose() }
        }
    }
    Write-Output "PASS: $Rounds synchronized races; exactly one winner per last slot; no duplicate order or counter decrement; API cancellation restores the slot."
}
finally {
    if ($holdJob) { $holdJob | Receive-Job -Wait -AutoRemoveJob | Out-Null }
    if ($http) { $http.Dispose() }
    foreach ($session in $sessions) {
        try { $null = Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken }
        catch { Write-Warning 'Could not revoke a fixture session.' }
    }
    # Remove only generated fixtures, in FK order. No real order is touched.
    $ids = ($customerIds | ForEach-Object { "'$_'" }) -join ','
    Sql @"
BEGIN;
DELETE FROM washgo.notifications WHERE user_id IN ($ids);
DELETE FROM washgo.orders WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_merchants WHERE locker_id='$lockerId';
DELETE FROM washgo.locker_slots WHERE locker_id='$lockerId';
DELETE FROM washgo.lockers WHERE id='$lockerId';
DELETE FROM washgo.customers WHERE user_id IN ($ids);
DELETE FROM washgo.users WHERE id IN ($ids);
COMMIT;
"@ | Out-Null
}
