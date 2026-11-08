param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://127.0.0.1:18025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# Run ONLY with merchant-create.compose.yml: all mail stays in the dedicated local sink.
$envJson=docker inspect washgo_backend --format '{{json .Config.Env}}'
if($LASTEXITCODE -ne 0){throw 'Backend container not available.'}
$backendEnv=$envJson|ConvertFrom-Json
if($backendEnv -notcontains 'Email__Smtp__Host=mailpit-merchant' -or
   $backendEnv -notcontains 'Email__Smtp__Port=1025' -or
   $backendEnv -notcontains 'Email__Smtp__EnableSsl=false' -or
   $backendEnv -notcontains 'Email__Smtp__Username=' -or $backendEnv -notcontains 'Email__Smtp__Password='){
    throw 'Refusing to send credentials: start the dedicated local SMTP override first.'
}
if(([uri]$MailpitUrl).Host -notin @('localhost','127.0.0.1')){throw 'Mailpit must be local.'}
$run=[Guid]::NewGuid().ToString('N').Substring(0,12)
$prefix="merchant-create-$run"
$session=$null; $customer=$null; $triggerCreated=$false; $mailStopped=$false
$chaos=Invoke-RestMethod "$MailpitUrl/api/v1/chaos"

function Body($Suffix,[hashtable]$Extra=@{}) {
    $body=@{email="$prefix-$Suffix@example.test";businessName="Create fixture $Suffix $run";address='1 Test Street'
        city='Test';district='Test';phone='0912345678';latitude=10.77;longitude=106.69;clientRequestId=[Guid]::NewGuid().ToString()}
    foreach($key in $Extra.Keys){$body[$key]=$Extra[$key]}
    $body
}
function Mail-Count($Email) {
    $messages=Invoke-RestMethod "$MailpitUrl/api/v1/search?query=$([uri]::EscapeDataString("to:$Email"))"
    # Mailpit total is the entire inbox; messages_count is the filtered search result.
    [int]$messages.messages_count
}
function Parallel-Create($Bodies) {
    $requests=@(foreach($b in $Bodies){@{method='Post';path='/merchant/';body=$b}})
    Parallel-Write $requests
}
function Parallel-Write($Requests) {
    $jobs=@()
    try {
        foreach($request in $Requests){
            $jobs+=Start-Job -ScriptBlock {
                param($url,$token,$payload,$method,$path)
                try {
                    $res=Invoke-RestMethod -Method $method -Uri "$url$path" -Headers @{Authorization="Bearer $token"} -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($payload))
                    [pscustomobject]@{code=[int]$res.statusCode;id=$res.data.id;version=$res.data.version}
                }catch{[pscustomobject]@{code=[int]$_.Exception.Response.StatusCode;id=$null;version=$null}}
            } -ArgumentList $BaseUrl,$admin,($request.body|ConvertTo-Json -Depth 10),$request.method,$request.path
        }
        @($jobs|Receive-Job -Wait)
    }finally{foreach($j in $jobs){Remove-Job $j -Force -ErrorAction SilentlyContinue}}
}
function Assert-Conflict($Body,$Code) {
    try{Call-Api Post '/merchant/' $Body $admin|Out-Null;throw 'Expected a conflict.'}
    catch{
        if([int]$_.Exception.Response.StatusCode -ne 409){throw}
        $detail=$_.ErrorDetails.Message
        if(!$detail){$reader=[IO.StreamReader]::new($_.Exception.Response.GetResponseStream());$detail=$reader.ReadToEnd();$reader.Dispose()}
        if(($detail|ConvertFrom-Json).meta.code -ne $Code){throw "Expected conflict code $Code"}
    }
}
function Assert-NoSecrets($Response) {
    if(($Response|ConvertTo-Json -Depth 15) -match '"(?:password|passwordHash|temporaryPassword)"'){
        throw 'Credential data was exposed in an API response.'
    }
}
try {
    $clearChaos=@{Sender=@{ErrorCode=451;Probability=0};Recipient=@{ErrorCode=550;Probability=0};Authentication=@{ErrorCode=535;Probability=0}}
    Invoke-RestMethod -Method Put -Uri "$MailpitUrl/api/v1/chaos" -ContentType application/json -Body ($clearChaos|ConvertTo-Json -Depth 4)|Out-Null
    $session=(Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data;$admin=$session.accessToken
    $customer=(Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data
    Assert-Status 401 Post '/merchant/' (Body 'unauthorized') $null
    Assert-Status 403 Post '/merchant/' (Body 'forbidden') $customer.accessToken
    Assert-Status 400 Post '/merchant/' (Body 'invalid-key' @{clientRequestId=[Guid]::Empty.ToString()}) $admin

    Write-Output '1. Concurrent retry with one key creates one account and one email'
    $same=Body 'same'
    $pair=Parallel-Create @($same,$same)
    if((($pair.code|Sort-Object)-join ',') -ne '201,201' -or @($pair.id|Select-Object -Unique).Count -ne 1){throw "Same-key race: $($pair.code)"}
    $created=(Call-Api Get "/merchant/$($pair[0].id)" $null $admin).data
    if($created.credentialDelivery -ne 1 -or !$created.credentialEmailSent -or !$created.credentialSentAt){throw 'SMTP success not persisted.'}
    if((Mail-Count $same.email) -ne 1){throw 'Concurrent retry sent more than one email.'}
    $lookup=(Call-Api Get "/merchant/operations/$($same.clientRequestId)" $null $admin).data
    if($lookup.result.data.id -ne $created.id -or $lookup.result.data.credentialDelivery -ne 1){throw 'Create lookup result incorrect.'}
    Assert-NoSecrets $lookup
    $replayed=Call-Api Post '/merchant/' $same $admin; Assert-NoSecrets $replayed
    if($replayed.data.id -ne $created.id -or (Mail-Count $same.email) -ne 1){throw 'Retry created or emailed again.'}
    $changed=$same.Clone();$changed.businessName='Different payload'
    Assert-Conflict $changed 'OperationIdReused'
    if((Sql "SELECT count(*) FROM washgo.audit_logs WHERE resource_id='$($created.id)' AND module='Merchant' AND action='Create';") -ne '1'){throw 'Create audit duplicated.'}
    if((Sql "SELECT response_json::text ~* 'temporaryPassword|passwordHash' FROM washgo.merchant_write_operations WHERE id='$($same.clientRequestId)';") -ne 'f'){throw 'Operation stored credentials.'}
    $mail=Invoke-RestMethod "$MailpitUrl/api/v1/search?query=$([uri]::EscapeDataString("to:$($same.email)"))"
    if($mail.messages[0].subject -notmatch 'Merchant'){throw 'Merchant credentials used the wrong subject.'}

    Write-Output '2. Different keys race for the same email / tax code'
    $email1=Body 'email-race';$email2=$email1.Clone();$email2.clientRequestId=[Guid]::NewGuid().ToString()
    $pair=Parallel-Create @($email1,$email2)
    $emailCount=Mail-Count $email1.email
    if((($pair.code|Sort-Object)-join ',') -ne '201,409' -or $emailCount -ne 1){throw "Email race returned $($pair.code), emails=$emailCount"}
    $tax=(Get-Random -Minimum 1000000000 -Maximum 1999999999).ToString()
    $tax1=Body 'tax-a' @{taxCode=$tax};$tax2=Body 'tax-b' @{taxCode=$tax}
    $pair=Parallel-Create @($tax1,$tax2)
    if((($pair.code|Sort-Object)-join ',') -ne '201,409'){throw 'Tax race did not reject one request.'}
    if((Sql "SELECT count(*) FROM washgo.merchants WHERE tax_code='$tax';") -ne '1'){throw 'Tax race left duplicates.'}

    Write-Output '3. Create racing an Admin email edit keeps one owner and does not deadlock'
    $targetEmail="$prefix-edit-race@example.test"
    $edit=$same.Clone();$edit.Remove('clientRequestId');$edit.id=$created.id;$edit.expectedVersion=$created.version
    $edit.email=$targetEmail;$edit.businessName='Edited during create race'
    $competing=Body 'edit-race'
    $pair=Parallel-Write @(@{method='Put';path='/merchant/';body=$edit},@{method='Post';path='/merchant/';body=$competing})
    $codes=($pair.code|Sort-Object)-join ','
    if($codes -notin @('200,409','201,400','201,409')){throw "Create/edit race returned $codes"}
    if((Sql "SELECT count(*) FROM washgo.users WHERE email='$targetEmail';") -ne '1'){throw 'Email has more than one owner after create/edit race.'}
    $oldReplay=(Call-Api Post '/merchant/' $same $admin).data
    if($oldReplay.businessName -ne $same.businessName -or (Mail-Count $same.email) -ne 1){throw 'Create replay changed the profile or resent credentials after an edit.'}

    Write-Output '4. Database unique violations are safe 409 and roll back the account'
    $forced=Body 'forced'
    $forcedTax=Body 'forced-tax'
    $delimiter='$$'
    Sql @"
CREATE FUNCTION washgo.smoke_identity_$run() RETURNS trigger LANGUAGE plpgsql AS $delimiter
BEGIN
IF TG_TABLE_NAME='users' THEN
  IF NEW.email='$($forced.email)' THEN NEW.email='$targetEmail'; END IF;
ELSIF TG_TABLE_NAME='merchants' THEN
  IF NEW.business_name='$($forcedTax.businessName)' THEN NEW.tax_code='$tax'; END IF;
END IF;
RETURN NEW; END;
$delimiter;
"@ | Out-Null
    # The delimiter is emitted without interpolation; only the dedicated fixture email is affected.
    Sql "CREATE TRIGGER smoke_identity_$run BEFORE INSERT ON washgo.users FOR EACH ROW EXECUTE FUNCTION washgo.smoke_identity_$run();" | Out-Null
    Sql "CREATE TRIGGER smoke_identity_tax_$run BEFORE INSERT ON washgo.merchants FOR EACH ROW EXECUTE FUNCTION washgo.smoke_identity_$run();" | Out-Null
    $triggerCreated=$true
    Assert-Conflict $forced 'EmailAlreadyUsed'
    if((Sql "SELECT count(*) FROM washgo.users WHERE email='$($forced.email)';") -ne '0'){throw 'Unique conflict left a partial account.'}
    Assert-Status 404 Get "/merchant/operations/$($forced.clientRequestId)" $null $admin
    Assert-Conflict $forcedTax 'TaxCodeAlreadyUsed'
    if((Sql "SELECT count(*) FROM washgo.users WHERE email='$($forcedTax.email)';") -ne '0'){throw 'Tax conflict left a partial login.'}
    Assert-Status 404 Get "/merchant/operations/$($forcedTax.clientRequestId)" $null $admin

    Write-Output '5. Definite recipient rejection keeps the account and does not auto-resend'
    $clearChaos.Recipient.Probability=100
    Invoke-RestMethod -Method Put -Uri "$MailpitUrl/api/v1/chaos" -ContentType application/json -Body ($clearChaos|ConvertTo-Json -Depth 4)|Out-Null
    $fail=Body 'failed'
    $failed=(Call-Api Post '/merchant/' $fail $admin).data
    if($failed.credentialDelivery -ne 2 -or $failed.credentialEmailSent -or $failed.credentialSentAt){throw 'Recipient rejection not classified Failed.'}
    $clearChaos.Recipient.Probability=0
    Invoke-RestMethod -Method Put -Uri "$MailpitUrl/api/v1/chaos" -ContentType application/json -Body ($clearChaos|ConvertTo-Json -Depth 4)|Out-Null
    $retry=(Call-Api Post '/merchant/' $fail $admin).data
    if($retry.id -ne $failed.id -or $retry.credentialDelivery -ne 2 -or (Mail-Count $fail.email) -ne 0){throw 'Retry after delivery failure recreated or resent credentials.'}

    Write-Output '6. Transport failure is Unknown; retry after SMTP recovers does not resend'
    docker stop washgo_merchant_mailpit | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not stop the dedicated SMTP fixture.'};$mailStopped=$true
    $unknownBody=Body 'unknown'
    $unknown=(Call-Api Post '/merchant/' $unknownBody $admin).data
    if($unknown.credentialDelivery -ne 0 -or $unknown.credentialEmailSent){throw 'Transport uncertainty was reported as Sent/definite failure.'}
    docker start washgo_merchant_mailpit | Out-Null;$mailStopped=$false
    for($i=0;$i -lt 20;$i++){try{Invoke-RestMethod "$MailpitUrl/api/v1/chaos"|Out-Null;break}catch{Start-Sleep -Milliseconds 200}}
    $again=(Call-Api Post '/merchant/' $unknownBody $admin).data
    if($again.id -ne $unknown.id -or (Mail-Count $unknownBody.email) -ne 0){throw 'Unknown outcome caused a second delivery attempt.'}
    Write-Output 'PASS: same-key retry/concurrency, email/tax uniqueness, rollback/409, no credential leaks, correct Merchant email, Sent/Failed/Unknown and no resend.'
}
finally {
    if($mailStopped){docker start washgo_merchant_mailpit|Out-Null}
    try{Invoke-RestMethod -Method Put -Uri "$MailpitUrl/api/v1/chaos" -ContentType application/json -Body ($chaos|ConvertTo-Json -Depth 4)|Out-Null}catch{Write-Warning 'Restore Mailpit chaos manually.'}
    Sql "DROP TRIGGER IF EXISTS smoke_identity_$run ON washgo.users; DROP TRIGGER IF EXISTS smoke_identity_tax_$run ON washgo.merchants; DROP FUNCTION IF EXISTS washgo.smoke_identity_$run();"|Out-Null
    Sql @"
DELETE FROM washgo.merchant_write_operations WHERE merchant_id IN (SELECT id FROM washgo.users WHERE email LIKE '$prefix-%@example.test');
DELETE FROM washgo.merchants WHERE user_id IN (SELECT id FROM washgo.users WHERE email LIKE '$prefix-%@example.test');
DELETE FROM washgo.users WHERE email LIKE '$prefix-%@example.test';
"@|Out-Null
    foreach($s in @($session,$customer)){if($s.accessToken){try{Call-Api Post '/auth/logout' @{refreshToken=$s.refreshToken} $s.accessToken|Out-Null}catch{Write-Warning 'Could not revoke a smoke session.'}}}
}
