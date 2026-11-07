param(
    [string]$BaseUrl='http://localhost:5000',
    [string]$PostgresContainer='washgo_postgres',
    [string]$PostgresUser='postgres',
    [string]$Database='washgo_db'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -PostgresUser $PostgresUser -Database $Database -HelpersOnly

# LOCAL DEV ONLY: terms of service shown on the first login, stored per account (not per device).
# A fresh Customer is cloned from the seed Customer; a test terms version is published and removed afterwards.
$sessions=@{}; $testVersion=$null
$run=[Guid]::NewGuid().ToString('N').Substring(0,8)
$customerB=[Guid]::NewGuid(); $email="terms-c-$run@example.com"

function Login-Customer { $s=(Call-Api Post '/auth/login' @{email=$email;password='Customer@123'} $null).data; $sessions["C$([Guid]::NewGuid())"]=$s; $s }

try {
    foreach($role in @('Admin','Merchant')){
        $sessions[$role]=(Call-Api Post '/auth/login' @{email="$($role.ToLower())@washgo.vn";password="${role}@123"} $null).data
    }
    $admin=$sessions.Admin.accessToken
    $seedCustomer=Sql "SELECT id FROM washgo.users WHERE email='customer@washgo.vn';"
    Sql @"
INSERT INTO washgo.users SELECT (jsonb_populate_record(NULL::washgo.users,to_jsonb(u)||jsonb_build_object(
'id','$customerB','email','$email','phone',NULL,'auth_provider',1,'provider_id',NULL,'is_active',true,'last_login_at',NULL))).* FROM washgo.users u WHERE id='$seedCustomer';
INSERT INTO washgo.customers SELECT (jsonb_populate_record(NULL::washgo.customers,to_jsonb(c)||jsonb_build_object('user_id','$customerB'))).* FROM washgo.customers c WHERE user_id='$seedCustomer';
"@ | Out-Null

    Write-Output '1. Public policy: version, content, effective date and the rules in force'
    $pub=(Call-Api Get '/policy/current' $null $null).data
    if(!$pub.terms.version -or !$pub.terms.content -or !$pub.terms.effectiveFrom -or !$pub.serverNow){throw 'Public terms incomplete.'}
    $settings=(Call-Api Get '/system-setting' $null $admin).data
    if($pub.rules.depositWindowMinutes -ne $settings.depositWindowMinutes -or $pub.rules.cancelDepositedPercent -ne $settings.cancelDepositedPercent -or $pub.rules.storageFeePerDay -ne $settings.storageFeePerDay -or $pub.rules.unpaidStorageAfterDays -ne 7){throw 'Rules differ from system settings.'}
    $version=$pub.terms.version

    Write-Output '2. First login flags the account; the flag stays until the terms are accepted'
    $first=Login-Customer
    if(!$first.user.firstLogin -or !$first.user.requiresTermsAcceptance -or $first.user.termsVersion -ne $version){throw 'First login flags wrong.'}
    $second=Login-Customer
    if($second.user.firstLogin -or !$second.user.requiresTermsAcceptance){throw 'Second login flags wrong.'}
    $customer=$second.accessToken
    $view=(Call-Api Get '/policy/customer' $null $customer).data
    if($view.accepted -or !$view.requiresAcceptance -or $view.acceptedAt){throw 'Customer view before acceptance wrong.'}
    Assert-Status 403 Get '/policy/customer' $null $admin
    if($sessions.Merchant.user.requiresTermsAcceptance){throw 'Merchant asked to accept customer terms.'}

    Write-Output '3. Acknowledge: wrong version rejected, replay stores one row, every device sees it'
    Assert-Status 400 Post '/policy/customer/acknowledge' @{version="0.0-$run"} $customer
    $ack=Call-Api Post '/policy/customer/acknowledge' @{version=$version} $customer
    if(!$ack.message -or !$ack.data.accepted -or !$ack.data.acceptedAt -or $ack.data.acceptedVersion -ne $version){throw 'Acknowledge failed.'}
    Call-Api Post '/policy/customer/acknowledge' @{version=$version} $first.accessToken | Out-Null
    if((Sql "SELECT count(*) FROM washgo.terms_acceptances WHERE user_id='$customerB';") -ne '1'){throw 'Replay created another acceptance.'}
    if(!(Sql "SELECT ip_address FROM washgo.terms_acceptances WHERE user_id='$customerB';")){throw 'Audit IP missing.'}
    $third=Login-Customer
    if($third.user.requiresTermsAcceptance -or (Call-Api Get '/auth/me' $null $first.accessToken).data.requiresTermsAcceptance){throw 'Accepted terms still required on another session.'}

    Write-Output '4. New version from the Admin asks the Customer again'
    Assert-Status 403 Post '/policy/' @{version="t$run";title='x';content='x'} $customer
    Assert-Status 400 Post '/policy/' @{version=$version;title='Dup';content='Dup'} $admin
    Assert-Status 400 Post '/policy/' @{version="t$run";title='';content=''} $admin
    $testVersion="t$run"
    $pubNew=Call-Api Post '/policy/' @{version=$testVersion;title="Smoke terms $run";content='Smoke terms content'} $admin
    if(!$pubNew.message -or $pubNew.data.version -ne $testVersion){throw 'Publish failed.'}
    if(@((Call-Api Get '/policy/versions' $null $admin).data | Where-Object { $_.version -eq $testVersion }).Count -ne 1){throw 'Version list missing the new version.'}
    $again=(Call-Api Get '/policy/customer' $null $customer).data
    if($again.accepted -or $again.terms.version -ne $testVersion -or $again.acceptedVersion -ne $version){throw 'New version not required.'}
    if(!(Login-Customer).user.requiresTermsAcceptance){throw 'Login does not ask for the new version.'}

    Write-Output 'PASS: public terms with rules from system settings, first-login and requires-acceptance flags on login/me, Customer-only view, acknowledge by version (wrong version rejected, replay idempotent, audit IP), synced across sessions, Admin publishes a new version (validation, duplicate, 403 for Customer) and the Customer is asked again.'
}
finally {
    foreach($session in $sessions.Values){try{Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken|Out-Null}catch{Write-Warning 'Smoke session logout failed.'}}
    if($testVersion){ Sql "DELETE FROM washgo.terms_policies WHERE version='$testVersion';" | Out-Null }
    Sql "UPDATE washgo.users SET is_active=false WHERE id='$customerB';" | Out-Null
}
