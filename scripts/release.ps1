<#
.SYNOPSIS
  Pipeline CI/CD manuale: git pull, test, build, copia zip su VM, deploy IIS.

.EXAMPLE
  ./scripts/release.ps1 -Env Production
  ./scripts/release.ps1 -Env Testing
  ./scripts/release.ps1 -Env Production -FirebaseServiceAccountKeyPath "C:\secrets\golp\serviceAccountKey.json"
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Production", "Testing")]
    [string]$Env,

    [string]$FirebaseServiceAccountKeyPath
)

$ErrorActionPreference = "Stop"

# --- Config ---
$vmUser    = "quandomaltanasceva"
$vmIp      = "4.245.228.135"
$sshKey    = "$env:USERPROFILE\.ssh\golp_deploy"
$vmZipDir  = "K:/TempDeploy"

$root      = Split-Path -Parent $PSScriptRoot
$scriptDir = $PSScriptRoot
$startTime = Get-Date

function Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Fail([string]$msg) {
    Write-Host ""
    Write-Host "ERRORE: $msg" -ForegroundColor Red
    exit 1
}

# --- Step 1: git pull ---
Step "git pull (branch master)"
Push-Location $root
try {
    $branch = git rev-parse --abbrev-ref HEAD
    if ($branch -ne "master" -and $branch -ne "main") {
        Write-Host "Branch corrente: $branch (non master/main) - continuo comunque" -ForegroundColor Yellow
    }
    git pull origin $branch
    if ($LASTEXITCODE -ne 0) { Fail "git pull fallito" }
    $commitHash = git rev-parse --short HEAD
    $commitMsg  = git log -1 --pretty=format:"%s"
    Write-Host "Commit: $commitHash - $commitMsg" -ForegroundColor Gray
} finally {
    Pop-Location
}

# --- Step 2: dotnet test ---
Step "dotnet test"
$testProject = Join-Path $root "src\Golp.Tests"
dotnet test $testProject --configuration Release
if ($LASTEXITCODE -ne 0) { Fail "Test falliti - deploy bloccato" }
Write-Host "Test OK" -ForegroundColor Green

# --- Step 3: build API ---
Step "Build API ($Env)"
if ($FirebaseServiceAccountKeyPath) {
    & "$scriptDir\deploy-api.ps1" -Env $Env -FirebaseServiceAccountKeyPath $FirebaseServiceAccountKeyPath
} else {
    & "$scriptDir\deploy-api.ps1" -Env $Env
}
if ($LASTEXITCODE -ne 0) { Fail "Build API fallita" }

# --- Step 4: build Frontend ---
Step "Build Frontend ($Env)"
& "$scriptDir\deploy-frontend.ps1" -Env $Env
if ($LASTEXITCODE -ne 0) { Fail "Build Frontend fallita" }

# --- Step 5: copia zip su VM ---
Step "Copia zip su VM ($vmIp)"
$apiZip = Join-Path $root "publish\golpapi-$Env.zip"
$feZip  = Join-Path $root "publish\golp-frontend-$Env.zip"

scp -i $sshKey $apiZip "${vmUser}@${vmIp}:${vmZipDir}/golpapi-${Env}.zip"
if ($LASTEXITCODE -ne 0) { Fail "SCP API zip fallito" }

scp -i $sshKey $feZip "${vmUser}@${vmIp}:${vmZipDir}/golp-frontend-${Env}.zip"
if ($LASTEXITCODE -ne 0) { Fail "SCP Frontend zip fallito" }

Write-Host "Zip copiati su VM" -ForegroundColor Green

# --- Step 6: deploy su IIS (esegui bat sulla VM) ---
Step "Deploy IIS sulla VM"
ssh -i $sshKey "${vmUser}@${vmIp}" "K:\TempDeploy\deploy-on-server.bat $Env"
if ($LASTEXITCODE -ne 0) { Fail "Deploy IIS fallito" }

Write-Host "Deploy IIS completato" -ForegroundColor Green

$elapsed = [math]::Round(((Get-Date) - $startTime).TotalSeconds)
Write-Host ""
Write-Host "Release $Env completata in ${elapsed}s" -ForegroundColor Green
