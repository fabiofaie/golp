<#
.SYNOPSIS
  Menu interattivo per build + zip di API/Frontend, Production/Testing.

.EXAMPLE
  ./scripts/deploy.ps1
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

function Show-Menu {
    Write-Host ""
    Write-Host "Cosa vuoi buildare e zippare?" -ForegroundColor Cyan
    Write-Host "  1) API - Production    (golpapi)"
    Write-Host "  2) API - Testing       (golpapitest)"
    Write-Host "  3) Frontend - Production (golp)"
    Write-Host "  4) Frontend - Testing    (golptest)"
    Write-Host "  5) Tutti e 4"
    Write-Host "  0) Esci"
    Write-Host ""
}

function Invoke-ApiDeploy([string]$Env) {
    & "$scriptDir\deploy-api.ps1" -Env $Env
}

function Invoke-FrontendDeploy([string]$Env) {
    & "$scriptDir\deploy-frontend.ps1" -Env $Env
}

Show-Menu
$choice = Read-Host "Scelta"

switch ($choice) {
    "1" { Invoke-ApiDeploy "Production" }
    "2" { Invoke-ApiDeploy "Testing" }
    "3" { Invoke-FrontendDeploy "Production" }
    "4" { Invoke-FrontendDeploy "Testing" }
    "5" {
        Invoke-ApiDeploy "Production"
        Invoke-ApiDeploy "Testing"
        Invoke-FrontendDeploy "Production"
        Invoke-FrontendDeploy "Testing"
    }
    "0" { Write-Host "Annullato." -ForegroundColor Yellow }
    default { Write-Host "Scelta non valida." -ForegroundColor Red }
}
