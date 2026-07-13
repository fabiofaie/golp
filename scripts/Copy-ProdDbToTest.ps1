<#
.SYNOPSIS
  Copia GolpDB (prod) su GolpDbTest, stesso Azure SQL server logico.

.DESCRIPTION
  Usa T-SQL "CREATE DATABASE ... AS COPY OF" — copia server-side, no export/import file.
  Se GolpDbTest esiste gia', la droppa prima (previa conferma).

.PARAMETER AdminPassword
  Password SQL admin (SecureString). Se omesso, prompt interattivo.

.EXAMPLE
  .\Copy-ProdDbToTest.ps1
#>

param(
    [Parameter(Mandatory = $false)]
    [SecureString]$AdminPassword,

    [string]$SourceDbName = "GolpDb",
    [string]$TargetDbName = "GolpDbTest"
)

$ErrorActionPreference = "Stop"

# Cablati dalle impostazioni Azure: solo server + user (NON segreti). Server e' lo stesso per entrambi i DB.
# NIENTE PASSWORD QUI DENTRO: questo file finisce nel repo. Password sempre da prompt o env var.
$ProdConnectionString = "Server=tcp:eqproject.database.windows.net,1433;Initial Catalog=GolpDb;User ID=satropposa;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

if ($ProdConnectionString -match '<server>|<admin-user>') {
    throw "Valorizza `$ProdConnectionString in cima allo script (server + user, NON password) prima di eseguirlo."
}

if ($ProdConnectionString -notmatch 'Server=tcp:([^,;.]+)') {
    throw "Impossibile estrarre il nome server dalla connection string configurata."
}
$ServerName = $Matches[1]

if ($ProdConnectionString -notmatch '(?:User ID|UID)=([^;]+)') {
    throw "Impossibile estrarre lo User ID dalla connection string configurata."
}
$AdminUser = $Matches[1]

if (-not $AdminPassword) {
    $secretFile = Join-Path $PSScriptRoot ".secrets\golp-sql-admin.enc"
    if (Test-Path $secretFile) {
        $AdminPassword = Get-Content $secretFile | ConvertTo-SecureString
    } elseif ($env:GOLP_SQL_ADMIN_PASSWORD) {
        $AdminPassword = ConvertTo-SecureString -String $env:GOLP_SQL_ADMIN_PASSWORD -AsPlainText -Force
    } else {
        Write-Host "Suggerimento: lancia .\Set-GolpSqlAdminPassword.ps1 una volta per non reinserirla ogni volta."
        $AdminPassword = Read-Host -Prompt "Password SQL admin per $AdminUser@$ServerName" -AsSecureString
    }
}
$plainPwd = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
)

$fqdn = "$ServerName.database.windows.net"
$masterConnStr = "Server=tcp:$fqdn,1433;Database=master;User ID=$AdminUser;Password=$plainPwd;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Server: $fqdn"
Write-Host "Source: $SourceDbName -> Target: $TargetDbName"

if (-not (Get-Module -ListAvailable -Name SqlServer)) {
    Write-Host "Modulo SqlServer non trovato, installo (CurrentUser)..."
    Install-Module -Name SqlServer -Scope CurrentUser -Force -AllowClobber
}
Import-Module SqlServer -ErrorAction Stop

# Verifica se il DB target esiste gia'
$checkQuery = "SELECT name FROM sys.databases WHERE name = '$TargetDbName'"
$existing = Invoke-Sqlcmd -ConnectionString $masterConnStr -Query $checkQuery

$targetTier = $null
if ($existing) {
    # Salva service tier di GolpDbTest PRIMA del drop: CREATE DATABASE AS COPY OF
    # eredita il tier del DB SORGENTE (prod), non quello del target esistente.
    $tierQuery = @"
SELECT
    d.edition,
    d.service_objective,
    d.database_id
FROM sys.database_service_objectives d
WHERE d.database_id = DB_ID('$TargetDbName')
"@
    $targetTier = Invoke-Sqlcmd -ConnectionString $masterConnStr -Query $tierQuery
    if ($targetTier) {
        Write-Host "Config consumo attuale di $TargetDbName da preservare: edition=$($targetTier.edition), service_objective=$($targetTier.service_objective)"
    } else {
        Write-Warning "Impossibile leggere il service tier attuale di $TargetDbName. Dopo la copia verifica manualmente le impostazioni di consumo."
    }

    $confirm = Read-Host "Il database '$TargetDbName' esiste gia'. Sovrascrivere? (drop + copy) [y/N]"
    if ($confirm -ne "y") {
        Write-Host "Annullato."
        exit 0
    }
    Write-Host "Drop di $TargetDbName in corso..."
    Invoke-Sqlcmd -ConnectionString $masterConnStr -Query "DROP DATABASE [$TargetDbName]" -QueryTimeout 120
}

Write-Host "Avvio copia $SourceDbName -> $TargetDbName (operazione server-side, puo' richiedere minuti)..."
$copyQuery = "CREATE DATABASE [$TargetDbName] AS COPY OF [$ServerName].[$SourceDbName]"
Invoke-Sqlcmd -ConnectionString $masterConnStr -Query $copyQuery -QueryTimeout 0

Write-Host "Copia avviata. Polling stato..."
do {
    Start-Sleep -Seconds 10
    $state = Invoke-Sqlcmd -ConnectionString $masterConnStr -Query "SELECT state_desc FROM sys.databases WHERE name = '$TargetDbName'"
    Write-Host "  stato: $($state.state_desc)"
} while ($state.state_desc -eq "COPYING")

if ($state.state_desc -eq "ONLINE") {
    Write-Host "Copia completata: $TargetDbName ONLINE."
} else {
    Write-Warning "Stato finale inatteso: $($state.state_desc)"
}

if ($targetTier) {
    Write-Host "Ripristino config consumo originale di $TargetDbName (edition=$($targetTier.edition), service_objective=$($targetTier.service_objective))..."
    $alterQuery = "ALTER DATABASE [$TargetDbName] MODIFY (EDITION = '$($targetTier.edition)', SERVICE_OBJECTIVE = '$($targetTier.service_objective)')"
    Invoke-Sqlcmd -ConnectionString $masterConnStr -Query $alterQuery -QueryTimeout 0

    Write-Host "Attendo che $TargetDbName torni ONLINE dopo il resize..."
    do {
        Start-Sleep -Seconds 10
        $resizeState = Invoke-Sqlcmd -ConnectionString $masterConnStr -Query "SELECT state_desc FROM sys.databases WHERE name = '$TargetDbName'"
        Write-Host "  stato: $($resizeState.state_desc)"
    } while ($resizeState.state_desc -ne "ONLINE")

    Write-Host "Config consumo ripristinata."
}
