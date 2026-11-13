param([string]$ReportsDirectory = (Join-Path $PSScriptRoot '../../tests/ci/results/security'))
$ErrorActionPreference = 'Stop'
$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git checkout required.' }
New-Item -ItemType Directory -Path $ReportsDirectory -Force | Out-Null
$reports = (Resolve-Path $ReportsDirectory).Path
$leakReport = Join-Path $reports 'gitleaks.json'
# Do not leave an old PASS report if the scanner cannot run.
'[]' | Set-Content $leakReport
& docker run --rm -v "${repoRoot}:/repo:ro" -v "${reports}:/reports" `
    -e GIT_CONFIG_COUNT=1 -e GIT_CONFIG_KEY_0=safe.directory -e GIT_CONFIG_VALUE_0=/repo `
    ghcr.io/gitleaks/gitleaks:v8.30.0@sha256:691af3c7c5a48b16f187ce3446d5f194838f91238f27270ed36eef6359a574d9 `
    git /repo --redact --no-banner --log-opts=--all --report-format=json --report-path=/reports/gitleaks.json
$leakExit = $LASTEXITCODE
if ($leakExit -notin @(0,1)) { throw "Secret scanner could not complete (exit $leakExit)." }

$project = Join-Path $repoRoot 'washgo-be/WashGo.HttpApi.Host/WashGo.HttpApi.Host.csproj'
$auditPath = Join-Path $reports 'nuget-audit.json'
& dotnet list $project package --vulnerable --include-transitive --format json > $auditPath
if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability audit could not complete.' }
$audit = Get-Content -Raw $auditPath | ConvertFrom-Json
if ($audit.logs | Where-Object { $_.level -eq 'error' }) { throw 'NuGet audit returned an error; see report.' }
if (!$audit.projects -or @($audit.projects).Count -eq 0) { throw 'NuGet audit returned no project results.' }
$blocking = @()
foreach ($entry in $audit.projects) {
    foreach ($framework in $entry.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            foreach ($vulnerability in $package.vulnerabilities) {
                if ($vulnerability.severity -in @('High','Critical')) {
                    $blocking += "$($package.id) $($package.resolvedVersion): $($vulnerability.severity)"
                }
            }
        }
    }
}
if ($leakExit -eq 1) { Write-Output 'FAIL: secret findings exist; report values are redacted.' }
if ($blocking.Count) { $blocking | Sort-Object -Unique | Write-Output }
if ($leakExit -ne 0 -or $blocking.Count) { throw 'Security gate failed. Fix findings; do not disable the scan to turn CI green.' }
Write-Output 'PASS: Git secret scan and NuGet audit (no High/Critical dependency findings).'
