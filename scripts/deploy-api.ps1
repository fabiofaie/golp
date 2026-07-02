<#
.SYNOPSIS
  Build + zip Golp.Api per un ambiente (Production o Testing), pronto per upload su IIS.

.EXAMPLE
  ./scripts/deploy-api.ps1 -Env Production
  ./scripts/deploy-api.ps1 -Env Testing
  ./scripts/deploy-api.ps1 -Env Production -FirebaseServiceAccountKeyPath "C:\Users\<tu>\secrets\golp\serviceAccountKey.json"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Production", "Testing")]
    [string]$Env,

    # Path al serviceAccountKey.json (vedi docs/firebase-setup.md, step 4).
    # Se omesso, viene letto dai user-secrets di Golp.Api (Firebase:ServiceAccountKeyPath).
    [string]$FirebaseServiceAccountKeyPath
)

$FirebaseVapidPublicKey = "BO_2P0f2FoKqd6r5n5Vp5cvvXJFmyxRrmZFw-PGw1D_x9E55Z2TMe6euVAtYx0eYxSNAHsOiUzR18mx-XuXXlMo"

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

Write-Host "Iniezione Firebase:VapidPublicKey in web.config..." -ForegroundColor Cyan
$vapidNode = $xml.CreateElement("environmentVariable")
$vapidNode.SetAttribute("name", "Firebase__VapidPublicKey")
$vapidNode.SetAttribute("value", $FirebaseVapidPublicKey)
$envVarsNode.AppendChild($vapidNode) | Out-Null

if (-not $FirebaseServiceAccountKeyPath) {
    $secretLines = dotnet user-secrets list --project $project 2>$null
    $keyLine = $secretLines | Where-Object { $_ -match '^Firebase:ServiceAccountKeyPath\s*=\s*(.+)$' }
    if ($keyLine) {
        $FirebaseServiceAccountKeyPath = $Matches[1].Trim()
        Write-Host "Firebase:ServiceAccountKeyPath letto dai user-secrets: $FirebaseServiceAccountKeyPath" -ForegroundColor DarkCyan
    }
}

if ($FirebaseServiceAccountKeyPath) {
    if (-not (Test-Path $FirebaseServiceAccountKeyPath)) {
        throw "FirebaseServiceAccountKeyPath non trovato: $FirebaseServiceAccountKeyPath"
    }
    Write-Host "Iniezione Firebase:ServiceAccountJson in web.config..." -ForegroundColor Cyan
    $saJson = Get-Content -Raw $FirebaseServiceAccountKeyPath
    $saNode = $xml.CreateElement("environmentVariable")
    $saNode.SetAttribute("name", "Firebase__ServiceAccountJson")
    $saNode.SetAttribute("value", $saJson)
    $envVarsNode.AppendChild($saNode) | Out-Null
}
else {
    Write-Host "Firebase:ServiceAccountKeyPath non trovato (né parametro né user-secrets): push notification disabilitate in questo pacchetto." -ForegroundColor Yellow
}

$xml.Save($webConfigPath)

Write-Host "Creazione zip $zipPath..." -ForegroundColor Cyan
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force

Write-Host "Fatto: $zipPath" -ForegroundColor Green
Write-Host "Estrai il contenuto nella cartella fisica IIS del sito (golpapi o golpapitest), poi recycle app pool." -ForegroundColor Yellow
