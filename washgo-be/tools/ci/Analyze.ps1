param([string]$BaseCommit)
$ErrorActionPreference = 'Stop'
$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Run analysis from a Git checkout.' }
if ($BaseCommit -and $BaseCommit -notmatch '^[0-9a-fA-F]{40}$') { throw 'Invalid base commit.' }

Push-Location $repoRoot
try {
    if ($BaseCommit) { git diff --check $BaseCommit HEAD -- washgo-be .github/workflows }
    else { git diff --check HEAD -- washgo-be .github/workflows }
    if ($LASTEXITCODE -ne 0) { throw 'Changed lines contain whitespace errors.' }

    $scripts = Get-ChildItem (Join-Path $repoRoot 'washgo-be/tests'), (Join-Path $repoRoot 'washgo-be/tools') -Filter '*.ps1' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|results)[\\/]' }
    foreach ($script in $scripts) {
        $parseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$null, [ref]$parseErrors) | Out-Null
        if ($parseErrors.Count) {
            $parseErrors | ForEach-Object { Write-Output "$($script.Name): $($_.Message)" }
            throw "PowerShell syntax check failed: $($script.Name)"
        }
    }
    Write-Output "PASS: diff whitespace and syntax of $($scripts.Count) PowerShell scripts."
}
finally { Pop-Location }
