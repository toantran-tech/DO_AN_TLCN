param([string]$BaseUrl = 'http://localhost:5000')
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

# Run against a local development DB. Retains one identifiable test account.
$email = "profile-smoke-$([Guid]::NewGuid().ToString('N'))@example.com"
$password = "Smoke@$([Guid]::NewGuid().ToString('N'))1aA"
$account = Call-Api Post '/auth/register' @{ email = $email; password = $password; fullName = 'Profile Smoke Test' } $null
$token = $account.data.accessToken
if (!$token) { throw 'Could not register the test Customer.' }

try {
    Assert-Status 401 Get '/customer/me' $null $null
    Assert-Status 401 Put '/customer/me' @{ fullName = 'Anonymous Update' } $null
    $initial = Call-Api Get '/customer/me' $null $token
    if ($initial.data.id -ne $account.data.user.id) { throw 'Profile ownership mismatch.' }

    $seed = Call-Api Post '/auth/login' @{ email = 'customer@washgo.vn'; password = 'Customer@123' } $null
    $seedBefore = Call-Api Get '/customer/me' $null $seed.data.accessToken
    try {
        $body = @{
            fullName = '  Customer Profile Test  '; phone = '+84 912-345-678'
            address = '  123 Test Street  '; district = ' Test District '; city = ' Test City '
            id = $seed.data.user.id; email = 'injected@example.com'; role = 'Admin'; totalSpent = 999999
        }
        $updated = Call-Api Put '/customer/me' $body $token
        $persisted = Call-Api Get '/customer/me' $null $token
        $auth = Call-Api Get '/auth/me' $null $token
        if (!$updated.succeeded -or $persisted.data.fullName -ne 'Customer Profile Test' -or
            $persisted.data.phone -ne '84912345678' -or $persisted.data.address -ne '123 Test Street' -or
            $persisted.data.district -ne 'Test District' -or $persisted.data.city -ne 'Test City' -or
            $auth.data.fullName -ne $persisted.data.fullName -or $auth.data.phone -ne $persisted.data.phone -or
            $auth.data.role -ne 'Customer' -or $persisted.data.email -ne $email -or $persisted.data.totalSpent -ne 0) {
            throw 'Profile persistence, normalization, or protected-field check failed.'
        }
        $seedAfter = Call-Api Get '/customer/me' $null $seed.data.accessToken
        if (($seedBefore.data | ConvertTo-Json -Compress) -ne ($seedAfter.data | ConvertTo-Json -Compress)) {
            throw 'Another Customer profile was changed.'
        }

        foreach ($invalid in @(
            @{ fullName = 'x' }, @{ fullName = 'Valid Name'; phone = 'abc123456789' },
            @{ fullName = 'Valid Name'; address = ('x' * 501) },
            @{ fullName = 'Valid Name'; district = ('x' * 101) },
            @{ fullName = 'Valid Name'; city = ('x' * 101) }
        )) { Assert-Status 400 Put '/customer/me' $invalid $token }

        $afterInvalid = Call-Api Get '/customer/me' $null $token
        if (($persisted.data | ConvertTo-Json -Compress) -ne ($afterInvalid.data | ConvertTo-Json -Compress)) {
            throw 'Invalid update changed the profile.'
        }
        Call-Api Put '/customer/me' @{ fullName = 'Profile Smoke Test'; phone = ''; address = ''; district = ''; city = '' } $token | Out-Null
        $cleared = Call-Api Get '/customer/me' $null $token
        if ($null -ne $cleared.data.phone -or $null -ne $cleared.data.address -or
            $null -ne $cleared.data.district -or $null -ne $cleared.data.city) { throw 'Optional field clearing failed.' }
    }
    finally {
        Call-Api Post '/auth/logout' @{ refreshToken = $seed.data.refreshToken } $seed.data.accessToken | Out-Null
    }

    $admin = Call-Api Post '/auth/login' @{ email = 'admin@washgo.vn'; password = 'Admin@123' } $null
    try {
        Assert-Status 403 Get '/customer/me' $null $admin.data.accessToken
        Assert-Status 403 Put '/customer/me' @{ fullName = 'Admin Attempt' } $admin.data.accessToken
    }
    finally {
        Call-Api Post '/auth/logout' @{ refreshToken = $admin.data.refreshToken } $admin.data.accessToken | Out-Null
    }
    Write-Output 'PASS: Customer profile persistence, User synchronization, normalization, validation, ownership, protected fields, and 401/403 authorization.'
    Write-Output "Development test account retained: $email"
}
finally {
    Call-Api Post '/auth/logout' @{ refreshToken = $account.data.refreshToken } $token | Out-Null
}
