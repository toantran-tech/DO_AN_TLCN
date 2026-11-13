param(
    [string]$BaseUrl = 'http://localhost:5099',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$Database = 'washgo_db'
)
$ErrorActionPreference='Stop'
# This suite needs the separate ChatbotHost fixture executable, never a production mock switch.
try { Invoke-RestMethod "$BaseUrl/fixture/calls" -TimeoutSec 3 | Out-Null }
catch { throw 'Start tests/smoke/ChatbotHost with WASHGO_CHATBOT_SMOKE=1 on localhost:5099 first.' }
$ids=@([Guid]::NewGuid(),[Guid]::NewGuid(),[Guid]::NewGuid())
$suffix=[Guid]::NewGuid().ToString('N').Substring(0,12)
$orderId=[Guid]::NewGuid()
$sessions=@();$jobs=@()
function Sql($statement) {
    $value=$statement | docker exec -i $PostgresContainer psql -U postgres -d $Database -v ON_ERROR_STOP=1 -At
    if($LASTEXITCODE -ne 0){throw 'Chatbot fixture SQL failed.'}; $value
}
function Request($status,$method,$path,$body,$token) {
    $arg=@{Uri="$BaseUrl$path";Method=$method;TimeoutSec=45}
    if($token){$arg.Headers=@{Authorization="Bearer $token"}}
    if($null -ne $body){$arg.ContentType='application/json; charset=utf-8';$arg.Body=[Text.Encoding]::UTF8.GetBytes(($body|ConvertTo-Json -Depth 15))}
    try {$result=Invoke-RestMethod @arg;if($status -notin @(200,202)){throw "Unexpected success for $path"};return $result}
    catch {
        if(!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne $status){throw "Unexpected HTTP result for $method $path; expected $status. Raw body suppressed."}
        try{return ($_.ErrorDetails.Message|ConvertFrom-Json)}catch{return $null}
    }
}
function Send($text,$context=$null,$conversation=$null,$requestId=$null) {
    if(!$requestId){$requestId=[Guid]::NewGuid().ToString()}
    $path=if($conversation){"/customer/chatbot/conversations/$conversation/messages"}else{'/customer/chatbot/conversations/first-message'}
    Request 200 Post $path @{requestId=$requestId;message=$text;context=$context} $token
}
function Check($condition,$message){if(!$condition){throw $message}}
try {
    foreach($i in 0..2){
        $seed=if($i -eq 2){'admin@washgo.vn'}else{'customer@washgo.vn'}
        Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
 'id','$($ids[$i])','email','cb_${suffix}_$i@example.test','provider_id',NULL,'auth_provider',1,
 'phone',NULL,'full_name','Chatbot Smoke $i','is_active',true,'last_login_at',NULL))).*
FROM washgo.users u WHERE email='$seed';
"@ | Out-Null
        if($i -lt 2){Sql "INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$($ids[$i])','loyalty_points',0,'total_orders',0,'total_spent',0))).* FROM washgo.customers c LIMIT 1;" | Out-Null}
        $password=if($i -eq 2){'Admin@123'}else{'Customer@123'}
        $sessions+=(Request 200 Post '/auth/login' @{email="cb_${suffix}_$i@example.test";password=$password} $null).data
    }
    $token=$sessions[0].accessToken;$other=$sessions[1].accessToken;$admin=$sessions[2].accessToken
    Check ($token -and $other -and $admin) 'Fixture login failed.'
    $root='/customer/chatbot/conversations'
    Request 401 Get $root $null $null|Out-Null
    Request 403 Get $root $null $admin|Out-Null
    $empty=Request 200 Get $root $null $token
    Check ($empty.data.items.Count -eq 0) 'New fixture customer should have no chats.'
    Request 400 Post "$root/first-message" @{requestId=[Guid]::NewGuid().ToString();message='';context=$null} $token|Out-Null
    Request 400 Post "$root/first-message" @{requestId=[Guid]::NewGuid().ToString();message='OTP: 123456'} $token|Out-Null
    Request 400 Post "$root/first-message" @{requestId=[Guid]::NewGuid().ToString();message='Near';context=@{location=@{latitude=10}}} $token|Out-Null
    Request 400 Post "$root/first-message" @{requestId=[Guid]::NewGuid().ToString();message=('x'*2001)} $token|Out-Null

    $req=[Guid]::NewGuid().ToString();$first=Send 'How WashGo works' $null $null $req
    $id=$first.data.conversationId
    Check ($first.data.status -eq 2 -and $first.data.answer) 'Completed reply missing.'
    $callsBefore=(Invoke-RestMethod "$BaseUrl/fixture/calls").calls
    $replay=Send 'How WashGo works' $null $null $req
    Check ($replay.data.messageId -eq $first.data.messageId -and (Invoke-RestMethod "$BaseUrl/fixture/calls").calls -eq $callsBefore) 'First-message replay called AI twice.'
    Request 409 Post "$root/first-message" @{requestId=$req;message='Different text'} $token|Out-Null
    foreach($path in @("$root/$id/messages","$root/$id/requests/$req")){Request 404 Get $path $null $other|Out-Null}
    Request 404 Delete "$root/$id" $null $other|Out-Null
    Request 404 Post "$root/$id/messages" @{requestId=[Guid]::NewGuid().ToString();message='hello'} $other|Out-Null
    foreach($i in 1..3){Send "Follow up $i" $null $id|Out-Null}
    $latest=Request 200 Get "$root/$id/messages?take=3" $null $token
    Check ($latest.data.hasMore -and $latest.data.items.Count -eq 3) 'Message lazy load missing.'
    $cursor=[Uri]::EscapeDataString($latest.data.nextCursor)
    $old=Request 200 Get "$root/$id/messages?take=3&cursor=$cursor" $null $token
    $seen=@($latest.data.items.id)+@($old.data.items.id)
    Check (($seen|Select-Object -Unique).Count -eq $seen.Count) 'Lazy load duplicated messages.'
    Request 400 Get "$root/$id/messages?cursor=broken" $null $token|Out-Null
    $a=Send 'New chat A';$b=Send 'New chat B'
    $page=Request 200 Get "$root`?take=2" $null $token
    $new=Send 'New chat arriving after snapshot'
    $next=Request 200 Get "$root`?take=2&cursor=$([Uri]::EscapeDataString($page.data.nextCursor))" $null $token
    Check ($next.data.items.id -notcontains $new.data.conversationId) 'Snapshot cursor included later conversation.'
    Request 400 Get "$root/$($a.data.conversationId)/messages?cursor=$cursor" $null $token|Out-Null

    $service=Send 'SERVICES_FIXTURE'
    Check ($service.data.cards.Count -gt 0 -and $service.data.cards[0].type -eq 'Service') 'Catalog tool missing.'
    $near=Send 'LOCKERS_FIXTURE' @{location=@{latitude=10.77;longitude=106.69}}
    Check ($near.data.sources.code -contains 'locker-catalog') 'Locker tool not invoked.'
    $noPosition=Send 'LOCKERS_FIXTURE'
    Check ($noPosition.data.cards.Count -eq 0) 'No location should require clarification.'
    $policy=Send 'POLICY_FIXTURE'
    Check ($policy.data.cards[0].type -eq 'Policy') 'Policy tool missing.'

    Sql @"
INSERT INTO washgo.orders SELECT (jsonb_populate_record(NULL::washgo.orders,to_jsonb(o)||jsonb_build_object(
 'id','$orderId','customer_id','$($ids[0])','order_code','WGCB$suffix','merchant_order_code',NULL,
 'status',1,'payment_status',1,'deposit_pin_code','817239','pickup_pin_code','761243','qr_code_string','PRIVATE_FIXTURE_QR',
 'is_deleted',false))).* FROM washgo.orders o ORDER BY created_on LIMIT 1;
"@|Out-Null
    if((Sql "SELECT COUNT(*) FROM washgo.orders WHERE id='$orderId';") -eq '1'){
        $order=Send 'ORDER_FIXTURE' @{selectedOrderId="$orderId"}
        Check ($order.data.cards[0].id -eq "$orderId") 'Owned order tool missing.'
        $json=$order|ConvertTo-Json -Depth 20
        Check ($json -notmatch '817239|761243|PRIVATE_FIXTURE_QR|depositPin|pickupPin') 'Order tool leaked access codes.'
        $savedToken=$token;$token=$other
        $denied=Send 'ORDER_FIXTURE' @{selectedOrderId="$orderId"}
        Check ($denied.data.cards.Count -eq 0) 'Order scope leaked to another customer.'
        $token=$savedToken
    }
    # Long histories must keep the newest context when the character budget is exceeded.
    Sql @"
INSERT INTO washgo.chat_messages(id,conversation_id,role,content,created_at,status,created_on,modified_on,is_deleted)
SELECT gen_random_uuid(),'$id',CASE WHEN i%2=1 THEN 1 ELSE 2 END,
 CASE WHEN i=1 THEN 'OLD_BUDGET' WHEN i=12 THEN 'LATEST_BUDGET' ELSE 'context' END||repeat('x',1950),
 now(),CASE WHEN i%2=1 THEN 2 ELSE NULL END,0,0,false FROM generate_series(1,12) i ORDER BY i;
"@|Out-Null
    Send 'CONTEXT_BUDGET_FIXTURE' $null $id|Out-Null
    $failureReq=[Guid]::NewGuid().ToString()
    $failed=Request 503 Post "$root/$id/messages" @{requestId=$failureReq;message='FAIL_FIXTURE'} $token
    $lookup=Request 200 Get "$root/$id/requests/$failureReq" $null $token
    Check ($lookup.data.status -eq 3 -and $lookup.data.errorCode -eq 'AiUnavailable') 'Failure/lookup mismatch.'
    $count=(Invoke-RestMethod "$BaseUrl/fixture/calls").calls
    Request 503 Post "$root/$id/messages" @{requestId=$failureReq;message='FAIL_FIXTURE'} $token|Out-Null
    Check ((Invoke-RestMethod "$BaseUrl/fixture/calls").calls -eq $count) 'Failed replay called AI again.'
    Request 503 Post "$root/$id/messages" @{requestId=[Guid]::NewGuid().ToString();message='BAD_TOOL_FIXTURE'} $token|Out-Null

    $parallel=[Guid]::NewGuid().ToString()
    foreach($i in 1..2){$jobs+=Start-Job -ScriptBlock {
        param($url,$jwt,$id,$req)
        try{Invoke-WebRequest -UseBasicParsing -Uri "$url/customer/chatbot/conversations/$id/messages" -Method Post -Headers @{Authorization="Bearer $jwt"} -ContentType 'application/json' -Body (@{requestId=$req;message='DELAY_FIXTURE'}|ConvertTo-Json)|Select-Object -ExpandProperty StatusCode}
        catch{[int]$_.Exception.Response.StatusCode}
    } -ArgumentList $BaseUrl,$token,$id,$parallel}
    $codes=@($jobs|Receive-Job -Wait -AutoRemoveJob);$jobs=@()
    Check (($codes -contains 200) -and ($codes|Where-Object {$_ -notin @(200,202)}).Count -eq 0) "Concurrent replay failed ($codes)."
    Check ((Sql "SELECT COUNT(*) FROM washgo.chat_messages WHERE conversation_id='$id' AND request_id='$parallel';") -eq '1') 'Duplicate user message.'
    Check ((Sql "SELECT COUNT(*) FROM washgo.chat_messages WHERE reply_to_message_id IN (SELECT id FROM washgo.chat_messages WHERE conversation_id='$id' AND request_id='$parallel');") -eq '1') 'Duplicate assistant reply.'

    # Do not resurrect a conversation deleted while a provider call is in flight.
    $deleteChat=Send 'Delete race fixture';$deleteId=$deleteChat.data.conversationId;$deleteReq=[Guid]::NewGuid().ToString()
    $job=Start-Job -ScriptBlock {
        param($url,$jwt,$id,$req)
        try{Invoke-WebRequest -UseBasicParsing -Uri "$url/customer/chatbot/conversations/$id/messages" -Method Post -Headers @{Authorization="Bearer $jwt"} -ContentType 'application/json' -Body (@{requestId=$req;message='DELAY_FIXTURE'}|ConvertTo-Json)|Select-Object -ExpandProperty StatusCode}
        catch{[int]$_.Exception.Response.StatusCode}
    } -ArgumentList $BaseUrl,$token,$deleteId,$deleteReq
    $jobs+= $job
    $observed=$false
    foreach($attempt in 1..80){
        if((Sql "SELECT COUNT(*) FROM washgo.chat_conversations WHERE id='$deleteId' AND active_message_id IS NOT NULL;") -eq '1'){$observed=$true;break}
        Start-Sleep -Milliseconds 100
    }
    Check $observed 'Did not observe in-flight chat request.'
    Request 409 Post "$root/$deleteId/messages" @{requestId=[Guid]::NewGuid().ToString();message='Second distinct request'} $token|Out-Null
    Request 200 Delete "$root/$deleteId" $null $token|Out-Null
    $deleteCode=Receive-Job $job -Wait -AutoRemoveJob;$jobs=@()
    Check ($deleteCode -eq 404) 'Deleted conversation returned AI result.'
    Check ((Sql "SELECT COUNT(*) FROM washgo.chat_messages WHERE conversation_id='$deleteId' AND role=2;") -eq '1') 'Deleted conversation gained an assistant reply.'

    # Retention hides expired content immediately; the same repository used by the worker physically purges it.
    $aged=Send 'Retention fixture';$agedId=$aged.data.conversationId
    Sql "UPDATE washgo.chat_messages SET created_at=now()-interval '91 days' WHERE conversation_id='$agedId'; UPDATE washgo.chat_conversations SET last_message_at=now()-interval '91 days' WHERE id='$agedId';" | Out-Null
    Request 404 Get "$root/$agedId/messages" $null $token|Out-Null
    Request 200 Post '/fixture/purge' @{} $null|Out-Null
    Check ((Sql "SELECT COUNT(*) FROM washgo.chat_conversations WHERE id='$agedId';") -eq '0') 'Expired conversation not purged.'

    # A stale lease is recovered by lookup; it does not call AI or create another response.
    $stale=[Guid]::NewGuid();$staleReq=[Guid]::NewGuid()
    Sql @"
INSERT INTO washgo.chat_messages(id,conversation_id,role,content,created_at,request_id,fingerprint,status,created_on,modified_on,is_deleted)
VALUES('$stale','$id',1,'stale fixture',now(),'$staleReq','fixture',1,0,0,false);
UPDATE washgo.chat_conversations SET active_message_id='$stale',lease_until=now()-interval '1 minute' WHERE id='$id';
"@|Out-Null
    $expired=Request 200 Get "$root/$id/requests/$staleReq" $null $token
    Check ($expired.data.errorCode -eq 'ChatTurnExpired') 'Stale lease not recovered.'
    Request 200 Delete "$root/$id" $null $token|Out-Null
    Request 404 Get "$root/$id/messages" $null $token|Out-Null
    # Removed chats still count toward usage; fixture host uses a 60/minute threshold.
    Sql @"
INSERT INTO washgo.chat_messages(id,conversation_id,role,content,created_at,created_on,modified_on,is_deleted)
SELECT gen_random_uuid(),'$id',1,'quota fixture',now(),0,0,false FROM generate_series(1,60);
"@|Out-Null
    Request 429 Post "$root/first-message" @{requestId=[Guid]::NewGuid().ToString();message='quota check'} $token|Out-Null
    'PASS: scopes, cursor lazy load, data sources/privacy, replay, busy/delete races, provider failure, stale lease, 90-day purge and quota after deletion.'
}
finally {
    foreach($job in $jobs){Stop-Job $job -ErrorAction SilentlyContinue;Remove-Job $job -Force -ErrorAction SilentlyContinue}
    foreach($session in $sessions){if($session.accessToken){try{Request 200 Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{}}}
    $set=($ids|ForEach-Object{"'$_'"})-join ','
    Sql @"
BEGIN;
DELETE FROM washgo.chat_conversations WHERE customer_id IN ($set);
DELETE FROM washgo.orders WHERE id='$orderId';
DELETE FROM washgo.refresh_tokens WHERE user_id IN ($set);
DELETE FROM washgo.customers WHERE user_id IN ($set);
DELETE FROM washgo.users WHERE id IN ($set);
COMMIT;
"@|Out-Null
}
