param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: GET /customer/lockers radiusKm filter around the seed Merchant's store.
$sessions=@{}; $lockerIds=@()
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$lat=10.7769; $lng=106.7009

function Raw($Path,$Token){
    $request=@{UseBasicParsing=$true;Method='Get';Uri="$BaseUrl$Path";TimeoutSec=30}
    if($Token){ $request.Headers=@{Authorization="Bearer $Token"} }
    try { $r=Invoke-WebRequest @request; return @{Status=[int]$r.StatusCode;Json=($r.Content | ConvertFrom-Json)} }
    catch {
        if($null -eq $_.Exception.Response){throw}
        $text=$_.ErrorDetails.Message
        if(!$text){ try { $text=(New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { } }
        $json=if($text){ try { $text | ConvertFrom-Json } catch { $null } } else { $null }
        return @{Status=[int]$_.Exception.Response.StatusCode;Json=$json}
    }
}
function Check($Condition,$Message){ if(!$Condition){throw $Message} }
function Lockers([string]$Query,$Token){ Raw "/customer/lockers?keyword=Radius%20$run$Query" $Token }
function Names($Result){ @($Result.Json.list | ForEach-Object { $_.name.Substring($_.name.Length-1) }) -join '' }
function Expect-Code([string]$Query,[string]$Code){
    $r=Lockers $Query $null
    Check ($r.Status -eq 400 -and $r.Json.meta.code -eq $Code) "Expected 400 $Code for '$Query'; got $($r.Status) $($r.Json.meta.code)."
}

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $merchantId=$sessions.Merchant.user.id
    # One degree of latitude is about 111.195 km: B 0 km, A 1.112 km, C 3.002 km (shown 3.00), D 4.003 km.
    foreach($l in @(@{n='A';d=0.01},@{n='B';d=0},@{n='C';d=0.027},@{n='D';d=0.036})){
        $created=(Call-Api Post '/locker/' @{merchantId="$merchantId";name="Radius $run $($l.n)";address='Private development fixture';district='Test';city='Test';latitude=$lat+$l.d;longitude=$lng;boxCount=1} $admin).data
        $lockerIds+=$created.id
    }
    $at="&latitude=$lat&longitude=$lng"

    Write-Output '1. No radius: the full catalog is unchanged (by name without a position, nearest first with one)'
    $plain=Lockers ''
    Check ($plain.Status -eq 200 -and $plain.Json.total -eq 4 -and (Names $plain) -eq 'ABCD' -and $plain.Json.meta.sortBy -eq 'Name' -and $null -eq $plain.Json.meta.radiusKm) "Plain list wrong: $(Names $plain)."
    $near=Lockers $at
    Check ($near.Json.total -eq 4 -and (Names $near) -eq 'BACD' -and $near.Json.meta.sortBy -eq 'Distance') "Nearest list wrong: $(Names $near)."

    Write-Output '2. Radius filters on the unrounded distance before counting'
    $r3=Lockers "$at&radiusKm=3"
    Check ($r3.Json.total -eq 2 -and (Names $r3) -eq 'BA' -and $r3.Json.meta.radiusKm -eq 3) "3 km wrong: $(Names $r3) total $($r3.Json.total)."
    Check ([math]::Round($near.Json.list[2].distanceKm,2) -eq 3) 'C should display 3.00 km while lying outside 3 km.'
    $edge=Lockers "$at&radiusKm=1.11"
    Check ($edge.Json.total -eq 1 -and (Names $edge) -eq 'B') "A (1.112 km, shown 1.11) must be outside 1.11 km: $(Names $edge)."
    $r5=Lockers "$at&radiusKm=5&page=2&take=2"
    Check ($r5.Json.total -eq 4 -and $r5.Json.totalPages -eq 2 -and (Names $r5) -eq 'CD') "Paging inside 5 km wrong: $(Names $r5)."
    $r5a=Lockers "$at&radiusKm=5&page=1&take=2"
    Check ((Names $r5a) -eq 'BA' -and $r5a.Json.hasNextPage) "First page inside 5 km wrong: $(Names $r5a)."

    Write-Output '3. Empty radius is not widened; guests can search'
    $empty=Lockers "&latitude=11.5&longitude=$lng&radiusKm=10"
    Check ($empty.Status -eq 200 -and $empty.Json.total -eq 0 -and @($empty.Json.list).Count -eq 0 -and $empty.Json.meta.radiusKm -eq 10) 'Empty radius should return an empty page.'

    Write-Output '4. Stable error codes'
    Expect-Code '&radiusKm=3' 'LocationRequiredForRadius'
    Expect-Code "$at&radiusKm=0" 'InvalidRadius'
    Expect-Code "$at&radiusKm=-1" 'InvalidRadius'
    Expect-Code "$at&radiusKm=50.5" 'InvalidRadius'
    Expect-Code "&latitude=$lat&radiusKm=3" 'InvalidLocation'
    Expect-Code "&latitude=95&longitude=$lng" 'InvalidLocation'

    Write-Output 'PASS: radiusKm filters the whole eligible set on the unrounded distance before total/paging, nearest first, empty results stay empty, invalid radius/location return stable codes.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    foreach($id in $lockerIds){ Sql "UPDATE washgo.locker_slots SET is_deleted=true WHERE locker_id='$id'; UPDATE washgo.lockers SET status=3,is_deleted=true WHERE id='$id';" | Out-Null }
}
