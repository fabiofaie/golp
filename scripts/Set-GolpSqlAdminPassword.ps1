<#
.SYNOPSIS
  Salva la password SQL admin cifrata (DPAPI, legata a utente+PC) per Copy-ProdDbToTest.ps1.
  Da lanciare UNA VOLTA. Il file cifrato non e' leggibile da altri utenti/PC.

.EXAMPLE
  .\Set-GolpSqlAdminPassword.ps1
#>

$ErrorActionPreference = "Stop"

$secretDir = Join-Path $PSScriptRoot ".secrets"
if (-not (Test-Path $secretDir)) {
    New-Item -ItemType Directory -Path $secretDir | Out-Null
}
$secretFile = Join-Path $secretDir "golp-sql-admin.enc"

$pwd1 = Read-Host -Prompt "Password SQL admin" -AsSecureString
$pwd1 | ConvertFrom-SecureString | Out-File -FilePath $secretFile -Encoding utf8 -NoNewline

Write-Host "Password salvata (cifrata) in $secretFile"
Write-Host "Leggibile solo da utente Windows corrente su questo PC."
