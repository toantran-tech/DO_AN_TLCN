param(
    [string]$BaseUrl = 'http://localhost:5088',
    [string]$PostgresContainer,
    [string]$Database = 'washgo_ci'
)
. "$PSScriptRoot/order-security.ps1" -BaseUrl $BaseUrl -PostgresContainer $PostgresContainer -Database $Database -HelpersOnly

# Read-only seeded fixtures: exercise the ABP ObjectMapper at runtime, not just compilation.
$session = $null
try {
    $session = (Call-Api Post '/auth/login' @{email='admin@washgo.vn';password='Admin@123'} $null).data
    if (!$session.accessToken -or $session.user.role -ne 'Admin') { throw 'JWT login regression.' }
    $token = $session.accessToken
    $merchantId = 'd3333333-3333-3333-3333-333333333333'
    $merchants = Call-Api Post '/merchant/filter' @{page=1;take=20} $token
    if (!$merchants.succeeded -or $merchants.list.id -notcontains $merchantId) { throw 'Merchant list mapping failed.' }
    foreach ($fixture in @(
        @{path="/merchant/$merchantId";id=$merchantId},
        @{path='/locker/a1111111-aaaa-1111-aaaa-111111111111';id='a1111111-aaaa-1111-aaaa-111111111111'},
        @{path='/wash-service/b1111111-bbbb-1111-bbbb-111111111111';id='b1111111-bbbb-1111-bbbb-111111111111'}
    )) {
        $view = Call-Api Get $fixture.path $null $token
        if (!$view.succeeded -or $view.data.id -ne $fixture.id) { throw "Runtime mapping failed: $($fixture.path)" }
    }
    $me = (Call-Api Get '/auth/me' $null $token).data
    if ($me.role -ne 'Admin') { throw 'JWT claim validation regression.' }
    $session = (Call-Api Post '/auth/refresh-token' @{refreshToken=$session.refreshToken} $null).data
    if (!$session.accessToken -or $session.user.id -ne $me.id) { throw 'JWT refresh regression.' }
    Call-Api Get '/auth/me' $null $session.accessToken | Out-Null
    Write-Output 'PASS: ABP mapping of Merchant list/detail, nested Locker boxes and WashService; JWT login/validation/refresh.'
}
finally {
    if ($session.accessToken) {
        Call-Api Post '/auth/logout' @{refreshToken=$session.refreshToken} $session.accessToken | Out-Null
    }
}
