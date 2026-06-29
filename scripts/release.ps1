<#
.SYNOPSIS
  Pipeline CI/CD manuale: git pull, test, build, copia zip su VM, deploy IIS, notifica email.

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
$vmUser     = "quandomaltanasceva"
$vmIp       = "4.245.228.135"
$sshKey     = "$env:USERPROFILE\.ssh\golp_deploy"
$vmZipDir   = "K:/TempDeploy"

$smtpHost   = "smtp.sharexls.it"
$smtpPort   = 587
$smtpUser   = "no-reply@sharexls.it"
$smtpPass   = "Fr4nc14C0rt4"
$smtpFrom   = "no-reply@sharexls.it"
$emailTo    = "faietaf@gmail.com"

$root       = Split-Path -Parent $PSScriptRoot
$scriptDir  = $PSScriptRoot
$startTime  = Get-Date

function Send-Notification([string]$subject, [string]$body) {
    try {
        $secPass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
        $cred    = New-Object System.Management.Automation.PSCredential($smtpUser, $secPass)
        Send-MailMessage `
            -SmtpServer $smtpHost -Port $smtpPort `
            -Credential $cred -UseSsl:$false `
            -From $smtpFrom -To $emailTo `
            -Subject $subject -Body $body -Encoding UTF8
        Write-Host "Email inviata: $subject" -ForegroundColor Gray
    } catch {
        Write-Host "WARN: email non inviata: $_" -ForegroundColor Yellow
    }
}

function Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function Fail([string]$msg) {
    Write-Host ""
    Write-Host "ERRORE: $msg" -ForegroundColor Red
    $elapsed = [math]::Round(((Get-Date) - $startTime).TotalSeconds)
    Send-Notification "[GOLP] Deploy FALLITO - $Env" @"
Deploy fallito per ambiente: $Env
Durata: ${elapsed}s

Errore:
$msg
"@
    exit 1
}

# --- Step 1: git pull ---
Step "git pull (branch master)"
Push-Location $root
try {
    $branch = git rev-parse --abbrev-ref HEAD
    if ($branch -ne "master" -and $branch -ne "main") {
        Write-Host "Branch corrente: $branch (non master/main) — continuo comunque" -ForegroundColor Yellow
    }
    git pull origin $branch 2>&1 | Write-Host
    if ($LASTEXITCODE -ne 0) { Fail "git pull fallito" }
    $commitHash = git rev-parse --short HEAD
    $commitMsg  = git log -1 --pretty=format:"%s"
    Write-Host "Commit: $commitHash — $commitMsg" -ForegroundColor Gray
} finally {
    Pop-Location
}

# --- Step 2: dotnet test ---
Step "dotnet test"
$testProject = Join-Path $root "src\Golp.Tests"
dotnet test $testProject --configuration Release --no-build:$false 2>&1 | Write-Host
if ($LASTEXITCODE -ne 0) { Fail "Test falliti — deploy bloccato" }
Write-Host "Test OK" -ForegroundColor Green

# --- Step 3: build API ---
Step "Build API ($Env)"
$apiArgs = @("-Env", $Env)
if ($FirebaseServiceAccountKeyPath) {
    $apiArgs += @("-FirebaseServiceAccountKeyPath", $FirebaseServiceAccountKeyPath)
}
& "$scriptDir\deploy-api.ps1" @apiArgs
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
$remoteCmd = "cmd /c K:\TempDeploy\deploy-on-server.bat $Env"

ssh -i $sshKey "${vmUser}@${vmIp}" $remoteCmd
if ($LASTEXITCODE -ne 0) { Fail "Deploy IIS fallito" }

Write-Host "Deploy IIS completato" -ForegroundColor Green

# --- Step 7: notifica successo ---
$elapsed = [math]::Round(((Get-Date) - $startTime).TotalSeconds)
Send-Notification "[GOLP] Deploy OK - $Env" @"
Deploy completato con successo!

Ambiente : $Env
Commit   : $commitHash — $commitMsg
Durata   : ${elapsed}s
"@

Write-Host ""
Write-Host "Release $Env completata in ${elapsed}s" -ForegroundColor Green
