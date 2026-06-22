<#
.SYNOPSIS
  Build + zip Golp.Api per un ambiente (Production o Testing), pronto per upload su IIS.

.EXAMPLE
  ./scripts/deploy-api.ps1 -Env Production
  ./scripts/deploy-api.ps1 -Env Testing
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Production", "Testing")]
    [string]$Env
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\Golp.Api"
$outDir = Join-Path $root "publish\$Env"
$zipPath = Join-Path $root "publish\golpapi-$Env.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Publishing Golp.Api ($Env)..." -ForegroundColor Cyan
dotnet publish $project -c Release -o $outDir

$webConfigPath = Join-Path $outDir "web.config"
if (-not (Test-Path $webConfigPath)) {
    throw "web.config non trovato in $outDir - publish ASP.NET Core non l'ha generato?"
}

Write-Host "Iniezione ASPNETCORE_ENVIRONMENT=$Env in web.config..." -ForegroundColor Cyan
[xml]$xml = Get-Content $webConfigPath
$aspNetCoreNode = $xml.configuration.location."system.webServer".aspNetCore

$envVarsNode = $aspNetCoreNode.environmentVariables
if (-not $envVarsNode) {
    $envVarsNode = $xml.CreateElement("environmentVariables")
    $aspNetCoreNode.AppendChild($envVarsNode) | Out-Null
}

$envVarNode = $xml.CreateElement("environmentVariable")
$envVarNode.SetAttribute("name", "ASPNETCORE_ENVIRONMENT")
$envVarNode.SetAttribute("value", $Env)
$envVarsNode.AppendChild($envVarNode) | Out-Null

$xml.Save($webConfigPath)

Write-Host "Creazione zip $zipPath..." -ForegroundColor Cyan
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force

Write-Host "Fatto: $zipPath" -ForegroundColor Green
Write-Host "Estrai il contenuto nella cartella fisica IIS del sito (golpapi o golpapitest), poi recycle app pool." -ForegroundColor Yellow
