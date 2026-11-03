param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: login messages (UC 1.1.2, 2.1.1, 4.1.1) and the empty locker list (UC 1.2.1 4a).
# Expected Vietnamese texts are base64 so this file stays ASCII for PowerShell 5.1.
$sessions=@{}
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$lockedCustomer=[Guid]::NewGuid(); $lockedShipperUser=[Guid]::NewGuid(); $lockedShipper=[Guid]::NewGuid()

function Text($B64) { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($B64)) }
$invalid=Text 'RW1haWwgaG/hurdjIG3huq10IGto4bqpdSBraMO0bmcgxJHDum5nLg=='
$lockedAdmin=Text 'VMOgaSBraG/huqNuIGPhu6dhIGLhuqFuIMSRw6MgYuG7iyBraMOzYS4gVnVpIGzDsm5nIGxpw6puIGjhu4cgQWRtaW4u'
$lockedMerchant=Text 'VMOgaSBraG/huqNuIGPhu6dhIGLhuqFuIMSRw6MgYuG7iyBraMOzYS4gVnVpIGzDsm5nIGxpw6puIGjhu4cgTWVyY2hhbnQu'
$noAccess=Text 'QuG6oW4ga2jDtG5nIGPDsyBxdXnhu4FuIHRydXkgY+G6rXAgdHJhbmcgbsOgeS4='
$noLocker=Text 'SGnhu4duIHThuqFpIGtow7RuZyBjw7MgbG9ja2VyIGto4bqjIGThu6VuZy4='

# Invoke-WebRequest keeps the error body readable (run-verbose wraps Invoke-RestMethod).
function Login-Result($Body) {
    try {
        $r=Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$BaseUrl/auth/login" -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes(($Body|ConvertTo-Json)))
        $json=[Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray())|ConvertFrom-Json
        return [pscustomobject]@{status=[int]$r.StatusCode;message=$json.message;data=$json.data}
    } catch {
        $resp=$_.Exception.Response
        $sr=New-Object IO.StreamReader($resp.GetResponseStream(),[Text.Encoding]::UTF8)
        $json=$sr.ReadToEnd()|ConvertFrom-Json
        return [pscustomobject]@{status=[int]$resp.StatusCode;message=$json.message;data=$null}
    }
}
function Expect($Result,$Status,$Message,$Label) {
    if($Result.status -ne $Status -or $Result.message -ne $Message){throw "$Label -> $($Result.status)"}
}

try {
    $seedCustomer=Sql "SELECT id FROM washgo.users WHERE email='customer@washgo.vn';"
    $seedShipperUser=Sql "SELECT id FROM washgo.users WHERE email='shipper@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$lockedCustomer','email','locked-c-$run@example.com','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',false))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$lockedCustomer'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$lockedShipperUser','email','locked-s-$run@example.com','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',false))).* FROM washgo.users u WHERE id='$seedShipperUser';
INSERT INTO washgo.shippers SELECT (jsonb_populate_record(NULL::washgo.shippers,to_jsonb(s)||jsonb_build_object(
'id','$lockedShipper','user_id','$lockedShipperUser','is_active',false))).* FROM washgo.shippers s WHERE user_id='$seedShipperUser';
"@ | Out-Null

    Write-Output '1. Wrong credentials (4a)'
    Expect (Login-Result @{email='admin@washgo.vn';password='Wrong@123'}) 400 $invalid 'Wrong password'
    Expect (Login-Result @{email="nobody-$run@example.com";password='Wrong@123'}) 400 $invalid 'Unknown email'

    Write-Output '2. Locked accounts (5a): Admin contact for Customer, Merchant contact for Shipper'
    Expect (Login-Result @{email="locked-c-$run@example.com";password='Customer@123'}) 400 $lockedAdmin 'Locked customer'
    Expect (Login-Result @{email="locked-s-$run@example.com";password='Shipper@123'}) 400 $lockedMerchant 'Locked shipper'
    # A wrong password never reveals that the account is locked.
    Expect (Login-Result @{email="locked-c-$run@example.com";password='Wrong@123'}) 400 $invalid 'Locked with wrong password'

    Write-Output '3. Portal role check (2.1.1 / 4.1.1 6a)'
    Expect (Login-Result @{email='merchant@washgo.vn';password='Merchant@123';portal='Shipper'}) 400 $noAccess 'Merchant on Shipper portal'
    $ok=Login-Result @{email='merchant@washgo.vn';password='Merchant@123';portal='merchant'}
    if($ok.status -ne 200 -or !$ok.data.accessToken){throw 'Merchant portal login failed.'}
    $sessions.Merchant=$ok.data
    $sessions.Customer=(Call-Api Post '/auth/login' @{email='customer@washgo.vn';password='Customer@123'} $null).data

    Write-Output '4. No available locker (1.2.1 4a)'
    $empty=Call-Api Get "/customer/lockers?keyword=nothing-$run" $null $sessions.Customer.accessToken
    if($empty.total -ne 0 -or $empty.message -ne $noLocker){throw 'Empty locker message wrong.'}

    Write-Output 'PASS: wrong credentials, locked Customer/Shipper messages, no lock disclosure on wrong password, portal role check, empty locker list message.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
}
