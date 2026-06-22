<#
.SYNOPSIS
  Build + zip frontend Angular per un ambiente (Production o Testing), pronto per upload su IIS.

.EXAMPLE
  ./scripts/deploy-frontend.ps1 -Env Production
  ./scripts/deploy-frontend.ps1 -Env Testing
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Production", "Testing")]
    [string]$Env
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $root "frontend\golp-app"
$distDir = Join-Path $appDir "dist\golp-app\browser"
$zipPath = Join-Path $root "publish\golp-frontend-$Env.zip"

Push-Location $appDir
try {
    if ($Env -eq "Production") {
        Write-Host "Building frontend (production)..." -ForegroundColor Cyan
        npm run build
    }
    else {
        Write-Host "Building frontend (testing)..." -ForegroundColor Cyan
        ng build --configuration testing
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $distDir)) {
    throw "Output build non trovato in $distDir"
}

$publishDir = Join-Path $root "publish"
if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Creazione zip $zipPath..." -ForegroundColor Cyan
Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -Force

Write-Host "Fatto: $zipPath" -ForegroundColor Green
Write-Host "Estrai il contenuto nella cartella fisica IIS del sito (golp o golptest)." -ForegroundColor Yellow
