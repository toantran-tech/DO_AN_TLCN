param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$MailpitUrl='http://localhost:8025',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
. "$PSScriptRoot/shipper-fixtures.ps1"
Add-Type -AssemblyName System.Net.Http

# LOCAL DEV ONLY: idempotent shipper creation, credential delivery as data, stale-update protection.
$sessions=@{}; $teamId=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$http=New-Object System.Net.Http.HttpClient
function New-Request($Method,$Path,$Body,$Token,$Headers){
    $req=New-Object System.Net.Http.HttpRequestMessage ([System.Net.Http.HttpMethod]::new($Method)),"$BaseUrl$Path"
    $req.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue 'Bearer',$Token
    if($Headers){ foreach($k in $Headers.Keys){ [void]$req.Headers.TryAddWithoutValidation($k,[string]$Headers[$k]) } }
    if($null -ne $Body){ $req.Content=New-Object System.Net.Http.StringContent (($Body|ConvertTo-Json -Depth 10),[Text.Encoding]::UTF8,'application/json') }
    $req
}
function Read-Response($Response){ $t=$Response.Content.ReadAsStringAsync().Result; @{ Status=[int]$Response.StatusCode; Json=$(if($t){$t|ConvertFrom-Json}else{$null}) } }
function Send($Method,$Path,$Body,$Token,$Headers){ Read-Response ($http.SendAsync((New-Request $Method $Path $Body $Token $Headers)).Result) }
function Mails($Email){ (Invoke-RestMethod -Method Get -Uri "$MailpitUrl/api/v1/search?query=$([uri]::EscapeDataString("to:$Email"))").messages.Count }
function With($H,$Values){ $c=$H.Clone(); foreach($k in $Values.Keys){$c[$k]=$Values[$k]}; $c }
function Body($Email){ @{fullName="Safe $run";phone=('09'+(Get-Random -Minimum 10000000 -Maximum 99999999));email=$Email;vehicleType=1;vehiclePlate='59-X1 123.45'} }

try {
    $sessions.Team=New-FixtureMerchant $run; $team=$sessions.Team.accessToken; $teamId=$sessions.Team.user.id

    Write-Output '1. Create with clientRequestId: replay returns the same shipper, no second account or email'
    $k=[Guid]::NewGuid(); $email="safe-$run@example.com"; $body=(Body $email)+@{clientRequestId=$k}
    $first=Send Post '/shipper/' $body $team
    if($first.Status -ne 201 -or [int]$first.Json.data.credentialDelivery -ne 1 -or !$first.Json.data.credentialEmailSent -or !$first.Json.data.credentialSentAt){throw "Create failed: $($first.Status) $($first.Json.message)"}
    [void](Read-TempPassword $email)
    $again=Send Post '/shipper/' $body $team
    if($again.Status -ne 200 -or !$again.Json.meta.replayed -or $again.Json.data.id -ne $first.Json.data.id -or !$again.Json.data.credentialEmailSent){throw "Replay failed: $($again.Status) $($again.Json.message)"}
    Start-Sleep -Seconds 1
    if((Mails $email) -ne 1){throw 'Replay sent another email.'}
    $other=Send Post '/shipper/' ((Body "safe2-$run@example.com")+@{clientRequestId=$k}) $team
    if($other.Status -ne 409 -or $other.Json.meta.code -ne 'ClientRequestIdReused'){throw "Reused key: $($other.Status)"}

    Write-Output '2. Idempotency-Key header and two concurrent sends create one account'
    $k2=[Guid]::NewGuid(); $email2="safe-c-$run@example.com"; $b2=Body $email2
    $t1=$http.SendAsync((New-Request Post '/shipper/' $b2 $team @{'Idempotency-Key'="$k2"}))
    $t2=$http.SendAsync((New-Request Post '/shipper/' $b2 $team @{'Idempotency-Key'="$k2"}))
    [void][System.Threading.Tasks.Task]::WaitAll(@($t1,$t2))
    $r1=Read-Response $t1.Result; $r2=Read-Response $t2.Result
    if((@($r1.Status,$r2.Status) | Sort-Object) -join ',' -ne '200,201' -or $r1.Json.data.id -ne $r2.Json.data.id){throw "Concurrent create: $($r1.Status)/$($r2.Status) $($r1.Json.message) $($r2.Json.message)"}
    if([int](Sql "SELECT count(*) FROM washgo.users WHERE email='$email2';") -ne 1){throw 'Concurrent create made two accounts.'}

    Write-Output '3. Update rejects a stale revision (body or If-Match) with the current data'
    $s=(Call-Api Get "/shipper/$($first.Json.data.id)" $null $team).data
    $edit=@{fullName="Safe $run edited";phone=$s.phone;email=$s.email;vehicleType=1;vehiclePlate=$s.vehiclePlate;isActive=$true}
    $ok=Send Put "/shipper/$($s.id)" (With $edit @{expectedRevision=$s.revision}) $team
    if($ok.Status -ne 200 -or $ok.Json.data.revision -le $s.revision){throw "Update failed: $($ok.Status) $($ok.Json.message)"}
    $stale=Send Put "/shipper/$($s.id)" (With $edit @{fullName='Lost update';expectedRevision=$s.revision}) $team
    if($stale.Status -ne 409 -or $stale.Json.meta.code -ne 'RevisionMismatch' -or $stale.Json.data.fullName -ne "Safe $run edited"){throw "Stale update: $($stale.Status)"}
    $staleHdr=Send Put "/shipper/$($s.id)" (With $edit @{fullName='Lost update'}) $team @{'If-Match'="`"$($s.revision)`""}
    if($staleHdr.Status -ne 409){throw 'If-Match ignored.'}
    $cur=$ok.Json.data.revision
    if((Send Put "/shipper/$($s.id)" $edit $team @{'If-Match'="`"$cur`""}).Status -ne 200){throw 'Current If-Match rejected.'}
    if((Call-Api Get "/shipper/$($s.id)" $null $team).data.fullName -ne "Safe $run edited"){throw 'Stale update was applied.'}

    Write-Output 'PASS: clientRequestId/Idempotency-Key create replays the same shipper without a second account or email, reused key for other data 409, two concurrent sends make one account (201 + 200); credential delivery returned as data (status/sent/time, never the password); update with stale expectedRevision or If-Match gives 409 with current data and changes nothing; revision increases on update.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    Remove-FixtureMerchant $teamId
    $http.Dispose()
}
