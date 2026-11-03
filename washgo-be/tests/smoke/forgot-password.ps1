param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$PostgresContainer = 'washgo_postgres',
    [string]$PostgresUser = 'postgres',
    [string]$Database = 'washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly
$originalApiCall = ${function:Call-Api}
function Call-Api($Method, $Path, $Body, $Token) {
    for ($retry = 0; ; $retry++) {
        try { return & $originalApiCall $Method $Path $Body $Token }
        catch {
            if (!$_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 429 -or $retry -ge 12) { throw }
            if ($retry -eq 0) { Write-Host 'Waiting for the existing Auth rate limit (10 requests/minute)...' }
            Start-Sleep -Seconds 5
        }
    }
}

# LOCAL DEV ONLY. No real email is sent: seed a hashed OTP for a private test user.
# Existing users/passwords are never modified. Delete only this fixture in finally.
$target = if ($PSVersionTable.PSEdition -eq 'Core') { 'netstandard2.0' } else { 'net20' }
$assembly = Get-ChildItem "$env:USERPROFILE/.nuget/packages/bcrypt.net-next/*/lib/$target/BCrypt.Net-Next.dll" | Select-Object -Last 1
if (!$assembly) { throw 'Run dotnet restore first to obtain BCrypt.Net-Next.' }
Add-Type -Path $assembly.FullName
$fixtureId = [Guid]::NewGuid().ToString()
$email = "reset-smoke-$fixtureId@example.test"
$otherEmail = "reset-other-$fixtureId@example.test"
$otpCode = '143258'
$otpHash = [BCrypt.Net.BCrypt]::HashPassword($otpCode)
$oldPassword = 'SmokeOld@123'
$newPassword = 'SmokeNew@456'
$passwordHash = [BCrypt.Net.BCrypt]::HashPassword($oldPassword)

function New-FixtureOtp([int]$Purpose = 2) {
    $script:otpId = [Guid]::NewGuid().ToString()
    Sql "UPDATE washgo.otp_codes SET is_used=true WHERE email='$email';
INSERT INTO washgo.otp_codes (id,user_id,email,otp_code,purpose,expiry_date,is_used,attempt_count,max_attempts,created_at)
VALUES ('$otpId','$fixtureId','$email','$otpHash',$Purpose,NOW()+INTERVAL '5 minutes',false,0,5,NOW());" | Out-Null
}
function Verify-FixtureOtp([int]$Purpose = 2) {
    $r = (Call-Api Post '/auth/verify-otp' @{email=$email;code=$otpCode;purpose=$Purpose} $null).data
    if (!$r.verified -or $r.verificationToken.Length -ne 64 -or !$r.expiresAt) { throw 'Missing verification token/expiry.' }
    $r.verificationToken
}
function Reset-Body($Token, $EmailAddress = $email) {
    @{email=$EmailAddress;verificationToken=$Token;newPassword=$newPassword;confirmNewPassword=$newPassword}
}

try {
    Sql "INSERT INTO washgo.users (id,role_id,email,password_hash,auth_provider,full_name,is_active,created_at,updated_at)
SELECT '$fixtureId',id,'$email','$passwordHash',1,'Forgot password smoke',true,NOW(),NOW()
FROM washgo.roles WHERE name='Customer';" | Out-Null
    $session = (Call-Api Post '/auth/login' @{email=$email;password=$oldPassword} $null).data
    if (!$session.accessToken) { throw 'Fixture login failed.' }

    Write-Output '1. Missing token, wrong OTP and persistent attempt counter'
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body '') $null
    New-FixtureOtp
    Assert-Status 400 Post '/auth/verify-otp' @{email=$email;code='000000';purpose=2} $null
    if ((Sql "SELECT attempt_count FROM washgo.otp_codes WHERE id='$otpId';") -ne '1') { throw 'Failed OTP attempt was rolled back.' }
    $token = Verify-FixtureOtp
    Assert-Status 400 Post '/auth/verify-otp' @{email=$email;code=$otpCode;purpose=2} $null
    $stored = Sql "SELECT otp_code FROM washgo.otp_codes WHERE id='$otpId';"
    if (!$stored.StartsWith('verified-reset:') -or $stored.Contains($token)) { throw 'Token was not stored as a purpose-bound hash.' }

    Write-Output '2. Wrong email/token, mismatch and expiry do not change password'
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body ('A' * 64)) $null
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body $token $otherEmail) $null
    $mismatch = Reset-Body $token
    $mismatch.confirmNewPassword = 'Different@123'
    Assert-Status 400 Post '/auth/reset-password' $mismatch $null
    Sql "UPDATE washgo.otp_codes SET expiry_date=NOW()-INTERVAL '1 second' WHERE id='$otpId';" | Out-Null
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body $token) $null
    if (!(Call-Api Post '/auth/login' @{email=$email;password=$oldPassword} $null).succeeded) { throw 'Rejected request changed password.' }
    Sql "UPDATE washgo.otp_codes SET expiry_date=NOW()+INTERVAL '5 minutes' WHERE id='$otpId';" | Out-Null

    Write-Output '3. Reset succeeds once, revokes old session and changes login password'
    $result = Call-Api Post '/auth/reset-password' (Reset-Body $token) $null
    if (!$result.succeeded) { throw 'Password reset failed.' }
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body $token) $null
    Assert-Status 401 Get '/auth/me' $null $session.accessToken
    Assert-Status 400 Post '/auth/login' @{email=$email;password=$oldPassword} $null
    if (!(Call-Api Post '/auth/login' @{email=$email;password=$newPassword} $null).succeeded) { throw 'New password login failed.' }

    Write-Output '4. Registration token cannot reset password'
    New-FixtureOtp 1
    $registrationToken = Verify-FixtureOtp 1
    Assert-Status 400 Post '/auth/reset-password' (Reset-Body $registrationToken) $null

    Write-Output '5. Concurrent reset requests consume token exactly once'
    New-FixtureOtp
    $token = Verify-FixtureOtp
    $jobs = @()
    try {
        1..2 | ForEach-Object {
            $jobs += Start-Job -ScriptBlock {
                param($Url, $Body)
                try { [int](Invoke-WebRequest $Url -Method Post -ContentType 'application/json' -Body $Body -UseBasicParsing).StatusCode }
                catch { if (!$_.Exception.Response) { throw }; [int]$_.Exception.Response.StatusCode }
            } -ArgumentList "$BaseUrl/auth/reset-password", ((Reset-Body $token) | ConvertTo-Json -Compress)
        }
        $statuses = @($jobs | Wait-Job | Receive-Job)
        if (@($statuses | Where-Object { $_ -eq 200 }).Count -ne 1 -or @($statuses | Where-Object { $_ -eq 400 }).Count -ne 1) {
            throw "Expected exactly one success and one rejection; got $statuses"
        }
    } finally {
        $jobs | Stop-Job -ErrorAction SilentlyContinue
        $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
    }
    Write-Output 'PASS: forgot-password verification/reset, replay, isolation and concurrency.'
} finally {
    Sql "DELETE FROM washgo.otp_codes WHERE user_id='$fixtureId';
DELETE FROM washgo.refresh_tokens WHERE user_id='$fixtureId';
DELETE FROM washgo.users WHERE id='$fixtureId';" | Out-Null
}
