param([string]$BaseUrl = 'http://localhost:5000', [string]$PostgresContainer = 'washgo_postgres')
$ErrorActionPreference = 'Stop'

function Call-Api($Method, $Path, $Body, $Token) {
    $args = @{ Method = $Method; Uri = "$BaseUrl$Path"; TimeoutSec = 20 }
    if ($Token) { $args.Headers = @{ Authorization = "Bearer $Token" } }
    if ($null -ne $Body) {
        $args.ContentType = 'application/json; charset=utf-8'
        $args.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10))
    }
    Invoke-RestMethod @args
}

function Assert-Status($Expected, $Method, $Path, $Body, $Token) {
    try { Call-Api $Method $Path $Body $Token | Out-Null }
    catch {
        if ([int]$_.Exception.Response.StatusCode -eq $Expected) { return }
        throw
    }
    throw "Expected HTTP $Expected for $Method $Path"
}

function Read-Failure($Failure) {
    $text = $Failure.ErrorDetails.Message
    if (!$text) {
        $reader = New-Object IO.StreamReader($Failure.Exception.Response.GetResponseStream())
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    $text | ConvertFrom-Json
}

function Assert-PhoneRejected($Path, $Body) {
    try { Call-Api Post $Path $Body $null | Out-Null }
    catch {
        if ([int]$_.Exception.Response.StatusCode -ne 400) { throw }
        $failure = Read-Failure $_
        if ($failure.message -notmatch '\u0111i\u1ec7n tho\u1ea1i') { throw 'Auth did not reject the phone before other registration steps.' }
        return
    }
    throw 'Auth accepted an invalid phone.'
}

function Sql($Statement) {
    $result = $Statement | docker exec -i $PostgresContainer psql -U postgres -d washgo_db -v ON_ERROR_STOP=1 -At
    if ($LASTEXITCODE -ne 0) { throw 'Development fixture SQL failed.' }
    $result
}

Add-Type -AssemblyName System.Net.Http
function Upload-Image($Token) {
    $client=New-Object System.Net.Http.HttpClient
    try {
        $client.DefaultRequestHeaders.Authorization=New-Object System.Net.Http.Headers.AuthenticationHeaderValue('Bearer',$Token)
        $bytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot '../../src/WashGo.Application/Features/AuthApplication/Templates/Assets/washgo-logo.png'))
        $form=New-Object System.Net.Http.MultipartFormDataContent
        $content=New-Object System.Net.Http.ByteArrayContent(,$bytes)
        $content.Headers.ContentType=[System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
        $form.Add($content,'file','avatar.png')
        return ($client.PostAsync("$BaseUrl/files/images",$form).Result.Content.ReadAsStringAsync().Result|ConvertFrom-Json).data.url
    } finally {$client.Dispose()}
}

# Run against a local development DB. Retains one identifiable test account.
# Public registration now needs an emailed OTP, so the fixture Customer is cloned
# from the seed account (same password) with a clean profile instead.
$email = "profile-smoke-$([Guid]::NewGuid().ToString('N'))@example.com"
$fixtureId = [Guid]::NewGuid()
$seedLogin = Call-Api Post '/auth/login' @{ email = 'customer@washgo.vn'; password = 'Customer@123' } $null
$seedId = $seedLogin.data.user.id
Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$fixtureId','email','$email','full_name','Profile Smoke Test','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true))).* FROM washgo.users u WHERE id='$seedId';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object(
'user_id','$fixtureId','address',NULL,'city',NULL,'total_spent',0,'loyalty_points',0))).* FROM washgo.customers c WHERE user_id='$seedId';
"@ | Out-Null
$account = Call-Api Post '/auth/login' @{ email = $email; password = 'Customer@123' } $null
$token = $account.data.accessToken
if (!$token) { throw 'Could not sign in the test Customer.' }

try {
    Assert-Status 401 Get '/customer/me' $null $null
    Assert-Status 401 Put '/customer/me' @{ fullName = 'Anonymous Update' } $null
    $initial = Call-Api Get '/customer/me' $null $token
    if ($initial.data.id -ne $account.data.user.id) { throw 'Profile ownership mismatch.' }

    $seed = $seedLogin
    $seedBefore = Call-Api Get '/customer/me' $null $seed.data.accessToken
    try {
        $body = @{
            fullName = '  Customer Profile Test  '; phone = ' 0912345678 '
            address = '  123 Test Street  '; city = ' Test City '
            id = $seed.data.user.id; email = 'injected@example.com'; role = 'Admin'; totalSpent = 999999
        }
        $updated = Call-Api Put '/customer/me' $body $token
        $persisted = Call-Api Get '/customer/me' $null $token
        $auth = Call-Api Get '/auth/me' $null $token
        if (!$updated.succeeded -or $persisted.data.fullName -ne 'Customer Profile Test' -or
            $persisted.data.phone -ne '0912345678' -or $persisted.data.address -ne '123 Test Street' -or
            $persisted.data.PSObject.Properties.Name -contains 'district' -or $persisted.data.city -ne 'Test City' -or
            $auth.data.fullName -ne $persisted.data.fullName -or $auth.data.phone -ne $persisted.data.phone -or
            $auth.data.role -ne 'Customer' -or $persisted.data.email -ne $email -or $persisted.data.totalSpent -ne 0) {
            throw 'Profile persistence, normalization, or protected-field check failed.'
        }
        $seedAfter = Call-Api Get '/customer/me' $null $seed.data.accessToken
        if (($seedBefore.data | ConvertTo-Json -Compress) -ne ($seedAfter.data | ConvertTo-Json -Compress)) {
            throw 'Another Customer profile was changed.'
        }

        $valid = @{fullName='Valid Name';phone='0912345678';address='A';city='Test City'}
        foreach ($changes in @(
            @{fullName='x'}, @{phone=$null}, @{phone=''}, @{phone='   '},
            @{phone='091234567'}, @{phone='09123456789'}, @{phone='1912345678'},
            @{phone='abc0912345678'}, @{phone='+84912345678'}, @{phone='0912-345-678'},
            @{phone="09123`n45678"}, @{address=$null}, @{address='   '},
            @{address=('x' * 501)}, @{city=$null}, @{city='   '}, @{city=('x' * 101)},
            @{avatarUrl=(Upload-Image $seed.data.accessToken)}, @{avatarUrl='/uploads/../appsettings.json'}
        )) {
            $invalid = $valid.Clone()
            foreach ($key in $changes.Keys) { $invalid[$key]=$changes[$key] }
            Assert-Status 400 Put '/customer/me' $invalid $token
        }
        foreach ($required in @('phone','address','city')) {
            $missing = $valid.Clone(); $missing.Remove($required)
            Assert-Status 400 Put '/customer/me' $missing $token
        }

        $afterInvalid = Call-Api Get '/customer/me' $null $token
        if (($persisted.data | ConvertTo-Json -Compress) -ne ($afterInvalid.data | ConvertTo-Json -Compress)) {
            throw 'Invalid update changed the profile.'
        }
        # Avatar (UC 1.1.5): own upload saved, omitted keeps it, empty string clears it.
        $avatar = Upload-Image $token
        Call-Api Put '/customer/me' @{ fullName = 'Profile Smoke Test'; phone='0912345678'; address = '1 Kept Street'; city='Test City'; avatarUrl = $avatar } $token | Out-Null
        if ((Call-Api Get '/customer/me' $null $token).data.avatarUrl -ne $avatar) { throw 'Avatar not saved.' }
        $saved = Call-Api Put '/customer/me' @{ fullName = 'Profile Smoke Test'; phone = '0912345678'; address = ('x' * 500); city = ('y' * 100) } $token
        if (!$saved.message) { throw 'Update message missing.' }
        $cleared = Call-Api Get '/customer/me' $null $token
        if ($cleared.data.phone -ne '0912345678' -or $cleared.data.address.Length -ne 500 -or $cleared.data.avatarUrl -ne $avatar -or
            $cleared.data.city.Length -ne 100) { throw 'Maximum lengths or required field persistence failed.' }
        Call-Api Put '/customer/me' @{ fullName = 'Profile Smoke Test'; phone='0912345678'; address = '1 Kept Street'; city='Test City'; avatarUrl = '' } $token | Out-Null
        if ($null -ne (Call-Api Get '/customer/me' $null $token).data.avatarUrl) { throw 'Avatar not cleared.' }

        # Totals come from real orders (UC 1.1.4).
        $seedNow = (Call-Api Get '/customer/me' $null $seed.data.accessToken).data
        $dbOrders = Sql "SELECT count(*) FROM washgo.orders WHERE customer_id='$seedId' AND NOT is_deleted AND status<>99;"
        $dbSpent = Sql "SELECT COALESCE(SUM(final_amount),0) FROM washgo.orders WHERE customer_id='$seedId' AND NOT is_deleted AND payment_status=2;"
        if ($seedNow.totalOrders -ne [int]$dbOrders -or [decimal]$seedNow.totalSpent -ne [decimal]$dbSpent) { throw "Totals wrong: $($seedNow.totalOrders)/$dbOrders, $($seedNow.totalSpent)/$dbSpent" }
    }
    finally {
        Call-Api Post '/auth/logout' @{ refreshToken = $seed.data.refreshToken } $seed.data.accessToken | Out-Null
    }

    # Existing fixture email prevents real SMTP delivery even if validation regresses.
    foreach ($invalidPhone in @('091234567','09123456789','abc0912345678')) {
        $registration = @{email=$email;fullName='Phone Smoke';phone=$invalidPhone;password='PhoneTest@123';confirmPassword='PhoneTest@123'}
        Assert-PhoneRejected '/auth/register' $registration
        $registration.purpose=1
        Assert-PhoneRejected '/auth/send-otp' $registration
    }
    # A valid phone reaches the verification-token check instead of failing phone validation.
    try {
        Call-Api Post '/auth/register' @{email=$email;fullName='Phone Smoke';phone=' 0912345678 ';password='PhoneTest@123';confirmPassword='PhoneTest@123'} $null | Out-Null
        throw 'Registration unexpectedly bypassed OTP.'
    } catch {
        if (!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400) { throw }
        $failure=Read-Failure $_
        if ($failure.message -match '\u0111i\u1ec7n tho\u1ea1i') { throw 'Auth rejected a valid ten-digit phone.' }
    }

    $admin = Call-Api Post '/auth/login' @{ email = 'admin@washgo.vn'; password = 'Admin@123' } $null
    try {
        Assert-Status 403 Get '/customer/me' $null $admin.data.accessToken
        Assert-Status 403 Put '/customer/me' @{ fullName = 'Admin Attempt' } $admin.data.accessToken
    }
    finally {
        Call-Api Post '/auth/logout' @{ refreshToken = $admin.data.refreshToken } $admin.data.accessToken | Out-Null
    }
    Write-Output 'PASS: ten-digit phone, required address/city, length limits, no District, User synchronization, avatar ownership/save/keep/clear, totals and authorization.'
    Write-Output "Development test account retained: $email"
}
finally {
    Call-Api Post '/auth/logout' @{ refreshToken = $account.data.refreshToken } $token | Out-Null
}
