param(
    [ValidateRange(1024,65535)][int]$Port = 5088,
    [string]$ProjectName = ('washgo-ci-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
)
$ErrorActionPreference = 'Stop'
if ($ProjectName -notmatch '^washgo-ci-[a-z0-9-]+$') { throw 'Use an isolated project name beginning with washgo-ci-.' }
$composeFile = Join-Path $PSScriptRoot 'compose.yml'
$results = Join-Path $PSScriptRoot 'results'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$compose = @('compose','--env-file',(Join-Path $PSScriptRoot 'ci.env'),'-p',$ProjectName,'-f',$composeFile)
$oldPort = $env:WASHGO_CI_PORT
$oldImage = $env:WASHGO_CI_IMAGE
$env:WASHGO_CI_PORT = "$Port"
$env:WASHGO_CI_IMAGE = "${ProjectName}:latest"
$baseUrl = "http://127.0.0.1:$Port"
$succeeded = $false
'RUNNING' | Set-Content (Join-Path $results 'api-result.txt')

try {
    & docker @compose up -d --build --wait --wait-timeout 180
    if ($LASTEXITCODE -ne 0) { throw 'CI Compose failed to start.' }
    $postgres = (& docker @compose ps -q postgres).Trim()
    if (!$postgres -or $LASTEXITCODE -ne 0) { throw 'CI PostgreSQL container missing.' }
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        try {
            $response = Invoke-WebRequest "$baseUrl/swagger/v1/swagger.json" -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) { $ready=$true; break }
        } catch { }
        Start-Sleep -Seconds 2
    }
    if (!$ready) { throw 'CI Backend did not become ready.' }
    # Startup currently logs migration/seed exceptions instead of failing the process.
    # Require actual tables/seed data so a Swagger-only response cannot pass CI.
    $seeded = & docker exec $postgres psql -U postgres -d washgo_ci -v ON_ERROR_STOP=1 -At -c "SELECT (SELECT count(*) FROM washgo.roles)>=4 AND (SELECT count(*) FROM washgo.services)>=3 AND EXISTS(SELECT 1 FROM washgo.users WHERE email='customer@washgo.vn');"
    if ($LASTEXITCODE -ne 0 -or "$seeded".Trim() -ne 't') { throw 'CI migrations or seed data are incomplete.' }
    & (Join-Path $PSScriptRoot '../smoke/mapping-compatibility.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'ABP mapper/JWT compatibility smoke failed.' }
    & (Join-Path $PSScriptRoot '../smoke/customer-service-filter.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Customer service filter smoke failed.' }
    & (Join-Path $PSScriptRoot '../smoke/actual-weight.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Order lifecycle/actual-weight smoke failed.' }
    foreach ($suite in @('rating-keyword-privacy.ps1', 'review-publishing.ps1')) {
        & (Join-Path $PSScriptRoot "../smoke/$suite") -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
        if (!$?) { throw "Review/privacy regression failed: $suite" }
    }
    $fixtures = & docker exec $postgres psql -U postgres -d washgo_ci -At -c "SELECT count(*) FROM washgo.services WHERE code LIKE 'CATALOG_%';"
    if ($LASTEXITCODE -ne 0 -or "$fixtures".Trim() -ne '0') { throw 'API smoke left fixture services behind.' }
    Write-Output 'PASS: isolated PostgreSQL migrations/seed, real Backend HTTP, catalog visibility, multi-select filters, sorting, validation, pagination and fixture cleanup.'
    'PASS: ABP mapper/JWT, catalog, order lifecycle/weight, review publication and role privacy; isolated database initialization/cleanup.' | Set-Content (Join-Path $results 'api-result.txt')
    $succeeded = $true
}
finally {
    & docker @compose logs --no-color *> (Join-Path $results 'containers.log')
    & docker @compose down --volumes --remove-orphans
    $cleanupExit = $LASTEXITCODE
    $env:WASHGO_CI_PORT = $oldPort
    $env:WASHGO_CI_IMAGE = $oldImage
    if (!$succeeded -or $cleanupExit -ne 0) { 'FAIL: see step output and containers.log.' | Set-Content (Join-Path $results 'api-result.txt') }
    if ($cleanupExit -ne 0) { throw 'CI cleanup failed; inspect the isolated Compose project.' }
}
