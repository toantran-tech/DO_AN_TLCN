param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: server-issued business codes (SV/DM/LK/KV + 4 digits), immutable on edit, shared allocator with
# collision fallback and CodeSpaceExhausted, locker address fields optional with field-level validation errors.
$sessions=@{}; $run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$services=@(); $categories=@(); $lockers=@(); $areas=@(); $fixtureIssuedAt='2000-01-01T00:00:00Z'
Add-Type -AssemblyName System.Net.Http

function Raw($Method,$Path,$Body,$Token){
    $request=@{UseBasicParsing=$true;Method=$Method;Uri="$BaseUrl$Path";Headers=@{Authorization="Bearer $Token"};TimeoutSec=30}
    if($null -ne $Body){ $request.ContentType='application/json; charset=utf-8'; $request.Body=[Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10)) }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $json=if($_.ErrorDetails.Message){ try { $_.ErrorDetails.Message | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function Service-Body([hashtable]$Extra=@{}){
    $b=@{operationId=[Guid]::NewGuid().ToString();name="Code Smoke $run $([Guid]::NewGuid().ToString('N').Substring(0,6))";unit='item';unitPrice=20000;pricePerItem=20000;serviceType=1;estimatedDurationHours=24}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b
}
function Locker-Body([hashtable]$Extra=@{}){
    $b=@{merchantIds=@("$($sessions.Merchant.user.id)");name="Code Locker $run $([Guid]::NewGuid().ToString('N').Substring(0,6))";address='Private development fixture';city='Test';latitude=10.77;longitude=106.69;boxCount=2}
    foreach($k in $Extra.Keys){$b[$k]=$Extra[$k]}; $b
}
function Assert-Code($Value,$Prefix,$What){ if("$Value" -cnotmatch "^$Prefix[0-9]{4}$"){throw "$What code '$Value' is not $Prefix + 4 digits."} }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken;$merchant=$sessions.Merchant.accessToken

    Write-Output '1. Service: issued SVdddd, client code ignored, replay keeps the code, immutable on edit'
    $body=Service-Body
    $s=Raw Post '/wash-service/' $body $admin
    if($s.Status -notin 200,201){throw "Service create failed: $($s.Status) $($s.Json.message)"}
    $svc=$s.Json.data; $services+=$svc.id
    Assert-Code $svc.code 'SV' 'Service'
    $again=(Call-Api Post '/wash-service/' $body $admin).data
    if($again.id -ne $svc.id -or $again.code -ne $svc.code){throw 'Service create replay changed id/code.'}
    $legacy=(Call-Api Post '/wash-service/' (Service-Body @{code="LEGACY_$run"}) $admin).data; $services+=$legacy.id
    Assert-Code $legacy.code 'SV' 'Service sent with a client code'
    if($legacy.code -eq $svc.code){throw 'Two services got the same code.'}
    $edit=@{operationId=[Guid]::NewGuid().ToString();id=$svc.id;expectedVersion=$svc.version;name="$($svc.name) edited";unit='item';unitPrice=21000;pricePerItem=21000;serviceType=1;estimatedDurationHours=24}
    $u=(Call-Api Put '/wash-service/' $edit $admin).data
    if($u.code -ne $svc.code -or $u.unitPrice -ne 21000){throw 'Edit without code changed the code or did not save.'}
    $bad=Raw Put '/wash-service/' (@{operationId=[Guid]::NewGuid().ToString();id=$svc.id;expectedVersion=$u.version;code='SV9999';name=$u.name;unit='item';unitPrice=21000;pricePerItem=21000;serviceType=1;estimatedDurationHours=24}) $admin
    if($bad.Status -ne 400 -or $bad.Json.meta.code -ne 'CodeImmutable'){throw "Changed service code not rejected: $($bad.Status) $($bad.Json.meta.code)"}
    $same=Raw Put '/wash-service/' (@{operationId=[Guid]::NewGuid().ToString();id=$svc.id;expectedVersion=$u.version;code=$svc.code.ToLower();name=$u.name;unit='item';unitPrice=21000;pricePerItem=21000;serviceType=1;estimatedDurationHours=24}) $admin
    if($same.Status -ne 200 -or $same.Json.data.code -ne $svc.code){throw 'Sending the current code on edit was rejected.'}
    if((Sql "SELECT code FROM washgo.services WHERE id='$($svc.id)';") -ne $svc.code){throw 'Stored service code changed.'}

    Write-Output '2. Concurrent creates never share a code'
    $client=New-Object System.Net.Http.HttpClient
    $tasks=@(1..4 | ForEach-Object {
        $msg=New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post,"$BaseUrl/wash-service/")
        $msg.Headers.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$admin)
        $msg.Content=New-Object System.Net.Http.StringContent(((Service-Body) | ConvertTo-Json),[Text.Encoding]::UTF8,'application/json')
        $client.SendAsync($msg)
    })
    [void][System.Threading.Tasks.Task]::WaitAll($tasks)
    $made=@($tasks | ForEach-Object { ($_.Result.Content.ReadAsStringAsync().Result | ConvertFrom-Json).data })
    $client.Dispose()
    foreach($m in $made){ if($m.id){ $services+=$m.id; Assert-Code $m.code 'SV' 'Concurrent service' } }
    if(@($made | Where-Object { $_.id }).Count -ne 4 -or @($made.code | Select-Object -Unique).Count -ne 4){throw "Concurrent creates: $(@($made.code) -join ',')"}

    Write-Output '3. Category: issued DMdddd, edit keeps it, a different code is rejected'
    $cb=@{operationId=[Guid]::NewGuid().ToString();name="Code Category $run";isActive=$true;sortOrder=10}
    $cat=(Call-Api Post '/admin/service-categories/' $cb $admin).data; $categories+=$cat.id
    Assert-Code $cat.code 'DM' 'Category'
    if((Call-Api Post '/admin/service-categories/' $cb $admin).data.code -ne $cat.code){throw 'Category replay changed the code.'}
    $ce=(Call-Api Put "/admin/service-categories/$($cat.id)" @{operationId=[Guid]::NewGuid().ToString();expectedVersion=$cat.version;name="Code Category $run edited";isActive=$true;sortOrder=11} $admin).data
    if($ce.code -ne $cat.code -or $ce.sortOrder -ne 11){throw 'Category edit without code wrong.'}
    $cbad=Raw Put "/admin/service-categories/$($cat.id)" @{operationId=[Guid]::NewGuid().ToString();expectedVersion=$ce.version;code='DM0001';name=$ce.name;isActive=$true;sortOrder=11} $admin
    if($cbad.Status -ne 400 -or $cbad.Json.meta.code -ne 'CodeImmutable'){throw "Changed category code not rejected: $($cbad.Status)"}

    Write-Output '4. Locker: no code/ward/district needed, issued LKdddd, field-level errors, legacy address kept'
    $lk=Raw Post '/locker/' (Locker-Body) $admin
    if($lk.Status -notin 200,201){throw "Locker create failed: $($lk.Status) $($lk.Json.message)"}
    $locker=$lk.Json.data; $lockers+=$locker.id
    Assert-Code $locker.code 'LK' 'Locker'
    if($locker.ward -or $locker.district){throw 'Locker got a ward/district that was not sent.'}
    $noCity=Raw Post '/locker/' (Locker-Body @{city=''}) $admin
    if($noCity.Status -ne 400 -or !$noCity.Json.errors.city -or $noCity.Json.message -match 'phường|quận' -or $noCity.Json.errors.code){throw "Missing city not reported on its own field: $($noCity.Json | ConvertTo-Json -Compress -Depth 4)"}
    $many=Raw Post '/locker/' (Locker-Body @{address='';latitude=200;description=('x'*2001)}) $admin
    if($many.Status -ne 400 -or !$many.Json.errors.address -or !$many.Json.errors.latitude -or !$many.Json.errors.description -or $many.Json.errors.city){throw "Field errors wrong: $(@($many.Json.errors.PSObject.Properties.Name) -join ',')"}
    Sql "UPDATE washgo.lockers SET ward='Legacy Ward', district='Legacy District' WHERE id='$($locker.id)';" | Out-Null
    $ld=(Call-Api Get "/locker/$($locker.id)" $null $admin).data
    $le=(Call-Api Put '/locker/' (Locker-Body @{id=$locker.id;expectedVersion=$ld.version;name=$ld.name;address='Moved fixture';status=1}) $admin).data
    if($le.code -ne $locker.code -or $le.address -ne 'Moved fixture' -or $le.ward -ne 'Legacy Ward' -or $le.district -ne 'Legacy District'){throw "Locker edit lost code/legacy address: $($le.code) $($le.ward) $($le.district)"}
    $ld=(Call-Api Get "/locker/$($locker.id)" $null $admin).data
    $lbad=Raw Put '/locker/' (Locker-Body @{id=$locker.id;expectedVersion=$ld.version;name=$ld.name;code='LK0000';status=1}) $admin
    if($lbad.Status -ne 400 -or $lbad.Json.meta.code -ne 'CodeImmutable' -or !$lbad.Json.errors.code){throw "Changed locker code not rejected: $($lbad.Status)"}

    Write-Output '5. Service proposal approval uses the same SV allocator'
    $pname="Code Proposal $run"
    $p=(Call-Api Post '/merchant-service/proposals' @{name=$pname;description='Fixture';unitPrice=50000;estimatedDurationHours=24;serviceType=1;unit='kg';reason='Fixture'} $merchant).data
    $ok=(Call-Api Post "/service-proposal/$($p.id)/approve?expectedVersion=$($p.version)" $null $admin).data
    $services+=$ok.serviceId
    Assert-Code (Sql "SELECT code FROM washgo.services WHERE id='$($ok.serviceId)';") 'SV' 'Approved proposal service'

    Write-Output '6. Service area: KVdddd; one free number left is still found; a full space gives CodeSpaceExhausted'
    $area=(Call-Api Post '/service-area/' @{name="Code Area $run";district='Test';city='Test';radiusKm=2;isActive=$true} $admin).data; $areas+=$area.id
    Assert-Code $area.code 'KV' 'Service area'
    $abad=Raw Put '/service-area/' @{id=$area.id;name=$area.name;code='KV0001';district='Test';city='Test';radiusKm=2;isActive=$true} $admin
    if($abad.Status -ne 400 -or $abad.Json.meta.code -ne 'CodeImmutable'){throw "Changed area code not rejected: $($abad.Status)"}
    # Reserve every free KV number with fixture claims, then free exactly one.
    Sql "INSERT INTO washgo.business_code_claims(code,prefix,resource_id,issued_at) SELECT 'KV'||lpad(n::text,4,'0'),'KV',gen_random_uuid(),'$fixtureIssuedAt' FROM generate_series(0,9999) n ON CONFLICT DO NOTHING;" | Out-Null
    $last=Sql "DELETE FROM washgo.business_code_claims WHERE code=(SELECT code FROM washgo.business_code_claims WHERE prefix='KV' AND issued_at='$fixtureIssuedAt' ORDER BY code DESC LIMIT 1) RETURNING code;"
    $last="$(@($last)[0])".Trim()
    $only=(Call-Api Post '/service-area/' @{name="Code Area Last $run";district='Test';city='Test';radiusKm=2;isActive=$true} $admin).data; $areas+=$only.id
    if($only.code -ne $last){throw "The only free code $last was not found (got $($only.code))."}
    $full=Raw Post '/service-area/' @{name="Code Area Full $run";district='Test';city='Test';radiusKm=2;isActive=$true} $admin
    if($full.Status -ne 409 -or $full.Json.meta.code -ne 'CodeSpaceExhausted'){throw "Exhausted space not reported: $($full.Status) $($full.Json.meta.code)"}
    if((Sql "SELECT count(*) FROM washgo.service_areas WHERE name='Code Area Full $run';") -ne '0'){throw 'Exhausted allocation left a record.'}

    Write-Output 'PASS: services (Admin and approved proposals), categories, lockers and service areas get server codes SV/DM/LK/KV + 4 digits; client codes ignored on create, edit keeps the stored code and rejects another one (CodeImmutable); create replay returns the same code; concurrent creates get distinct codes; the last free number is still allocated and a full space answers 409 CodeSpaceExhausted without a partial record; locker create needs no code/ward/district, validation errors are per field, edit keeps legacy ward/district.'
}
finally {
    Sql "DELETE FROM washgo.business_code_claims WHERE issued_at='$fixtureIssuedAt';" | Out-Null
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $services | Where-Object { $_ }){ Sql "UPDATE washgo.services SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
    foreach($id in $categories){ Sql "UPDATE washgo.service_categories SET is_deleted=true, is_active=false WHERE id='$id';" | Out-Null }
    foreach($id in $lockers){ Sql "UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$id'; UPDATE washgo.lockers SET is_deleted=true, status=3, available_slots=0 WHERE id='$id';" | Out-Null }
    foreach($id in $areas){ Sql "UPDATE washgo.service_areas SET is_active=false WHERE id='$id';" | Out-Null }
}
