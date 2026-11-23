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
    & dotnet run --project (Join-Path $PSScriptRoot '../smoke/Payments/Payments.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Payment gateway outcome regression failed.' }
    & dotnet run --project (Join-Path $PSScriptRoot '../smoke/PaymentRecovery/PaymentRecovery.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Payment query/limit regression failed.' }
    & dotnet run --project (Join-Path $PSScriptRoot '../smoke/Loyalty/Loyalty.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Loyalty calculation regression failed.' }
    & dotnet run --project (Join-Path $PSScriptRoot '../smoke/WorkflowMetadata/WorkflowMetadata.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Workflow metadata regression failed.' }
    & dotnet run --project (Join-Path $PSScriptRoot '../smoke/CollectionDeadline/CollectionDeadline.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Collection deadline calendar regression failed.' }
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
    $loyaltySwagger = Invoke-RestMethod "$baseUrl/swagger/v1/swagger.json" -TimeoutSec 15
    foreach ($route in @('/customer/loyalty/balance', '/customer/loyalty/history', '/customer/loyalty/quote/{orderId}', '/system-setting/loyalty')) {
        if (!$loyaltySwagger.paths.PSObject.Properties[$route]) {
            throw "Loyalty endpoint not registered in Swagger: $route"
        }
    }
    # Swagger must send an actual multipart file, not just a Content-Type header.
    $openApi = Invoke-RestMethod "$baseUrl/swagger/v1/swagger.json"
    foreach ($route in @('/payment/orders/{orderId}/active-session', '/payment/orders/{orderId}/quote', '/payment/orders/{orderId}/verify-session', '/payment/verification-operations/{operationId}', '/admin/service-categories/filter', '/customer/service-categories', '/notification/outbox/summary', '/admin/accounts/{id}', '/admin/accounts/bulk', '/auth/reauthenticate')) {
        if (!$openApi.paths.PSObject.Properties[$route]) { throw "Missing payment recovery route: $route" }
    }
    $openApi | ConvertTo-Json -Depth 100 | Set-Content (Join-Path $results 'swagger.json')
    foreach ($route in @('/files/images', '/files/videos', '/files/documents/')) {
        $path = @($openApi.paths.PSObject.Properties | Where-Object { $_.Name.TrimEnd('/') -eq $route.TrimEnd('/') })
        if ($path.Count -ne 1) { throw "Missing or ambiguous Swagger upload route: $route" }
        $operation = $path[0].Value.post
        $multipart = $operation.requestBody.content.'multipart/form-data'.schema
        if (!$operation.requestBody.required -or $multipart.type -ne 'object' -or
            $multipart.properties.file.type -ne 'string' -or $multipart.properties.file.format -ne 'binary' -or
            'file' -notin $multipart.required) { throw "Invalid Swagger file upload contract: $route. Actual request body: $($operation.requestBody | ConvertTo-Json -Depth 12 -Compress)" }
        if ($route -eq '/files/documents/' -and ('kind' -notin $multipart.required -or !$multipart.properties.merchantId)) {
            throw 'Document multipart contract must include kind and optional merchantId.'
        }
    }
    Write-Output 'PASS: Swagger multipart file schema for image, video and document uploads.'

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
    & (Join-Path $PSScriptRoot '../smoke/locker-last-slot-race.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Last-slot concurrent reservation smoke failed.' }
    foreach ($suite in @('rating-keyword-privacy.ps1', 'review-publishing.ps1', 'storage-lifecycle.ps1', 'customer-locker-slots.ps1', 'customer-public-reviews-notifications.ps1', 'service-categories-workflow.ps1', 'order-assignment-return-statuses.ps1', 'order-progress.ps1', 'business-codes.ps1', 'category-classification.ps1', 'admin-accounts.ps1', 'account-status-codes.ps1', 'proposal-category-filter.ps1', 'shipper-task-capacity.ps1', 'locker-radius-filter.ps1', 'merchant-service-radius.ps1', 'collection-deadline.ps1', 'payment-result-redirect.ps1')) {
        & (Join-Path $PSScriptRoot "../smoke/$suite") -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
        if (!$?) { throw "API regression failed: $suite" }
    }
    & (Join-Path $PSScriptRoot '../smoke/payment-assignment-concurrency.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Payment/assignment concurrency regression failed.' }
    & (Join-Path $PSScriptRoot '../smoke/loyalty-points.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Loyalty wallet/payment/refund regression failed.' }
    & (Join-Path $PSScriptRoot '../smoke/payment-session-recovery.ps1') -BaseUrl $baseUrl -PostgresContainer $postgres -Database washgo_ci
    if (!$?) { throw 'Payment recovery HTTP regression failed.' }
    $fixtures = & docker exec $postgres psql -U postgres -d washgo_ci -At -c "SELECT count(*) FROM washgo.services WHERE code LIKE 'CATALOG_%';"
    if ($LASTEXITCODE -ne 0 -or "$fixtures".Trim() -ne '0') { throw 'API smoke left fixture services behind.' }
    Write-Output 'PASS: isolated PostgreSQL migrations/seed, real Backend HTTP, catalog visibility, multi-select filters, sorting, validation, pagination and fixture cleanup.'
    'PASS: ABP mapper/JWT, catalog, order lifecycle/weight, review publication, storage/redelivery/LK, dynamic categories, structured events/outbox, payment recovery, loyalty and role privacy; isolated database initialization/cleanup.' | Set-Content (Join-Path $results 'api-result.txt')
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
