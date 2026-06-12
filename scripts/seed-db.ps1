# seed-db.ps1 — Popola il DB locale con dati di test
# Crea 10 giocatori, 1 circolo Padel, varie partite (alcune confirmed, alcune pending)
# Tutti gli utenti hanno password: testgolp

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ApiBase = "http://localhost:5120"
$ApiProject = "$PSScriptRoot\..\src\Golp.Api"

# ── Helpers ─────────────────────────────────────────────────────────────────

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token = $null
    )
    $uri = "$ApiBase$Path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $params = @{
        Method  = $Method
        Uri     = $uri
        Headers = $headers
    }
    if ($Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 10) }

    try {
        return Invoke-RestMethod @params
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $responseBody = $_.ErrorDetails.Message
        Write-Error "  [$Method $Path] HTTP $statusCode : $responseBody"
        throw
    }
}

function Register-User {
    param([string]$Name, [string]$Email)
    $res = Invoke-Api -Method POST -Path "/auth/register" -Body @{
        name     = $Name
        email    = $Email
        password = "testgolp"
    }
    return $res.token
}

function Get-UserId {
    param([string]$Token)
    # Decode JWT payload (middle segment, base64url)
    $parts = $Token.Split(".")
    $payload = $parts[1]
    # Pad to multiple of 4
    $mod = $payload.Length % 4
    if ($mod -ne 0) { $payload += "=" * (4 - $mod) }
    $payload = $payload.Replace("-", "+").Replace("_", "/")
    $json = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload))
    $obj  = $json | ConvertFrom-Json
    # JwtService uses JwtRegisteredClaimNames.Sub → "sub"
    return $obj.sub
}

# ── Step 1: Apply migrations ─────────────────────────────────────────────────

Write-Host "`n[1/5] Applying EF migrations..." -ForegroundColor Cyan
Push-Location $ApiProject
try {
    dotnet ef database update
    if ($LASTEXITCODE -ne 0) { throw "Migration failed" }
} finally {
    Pop-Location
}
Write-Host "      Migrations applied." -ForegroundColor Green

# ── Step 2: Start API in background ─────────────────────────────────────────

Write-Host "`n[2/5] Starting API..." -ForegroundColor Cyan

$apiJob = Start-Job -ScriptBlock {
    param($proj)
    Set-Location $proj
    dotnet run --no-build 2>&1
} -ArgumentList (Resolve-Path $ApiProject)

# Wait until the API is ready (max 60s)
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    try {
        Invoke-RestMethod -Uri "$ApiBase/sports" -ErrorAction Stop | Out-Null
        $ready = $true
        break
    } catch { }
}
if (-not $ready) {
    Stop-Job $apiJob
    Remove-Job $apiJob
    throw "API did not start within 60 seconds"
}
Write-Host "      API ready at $ApiBase" -ForegroundColor Green

try {

# ── Step 3: Register 10 players ──────────────────────────────────────────────

Write-Host "`n[3/5] Registering players..." -ForegroundColor Cyan

$players = @(
    @{ Name = "Luca Rossi";         Email = "luca.rossi@test.golp" },
    @{ Name = "Marco Bianchi";      Email = "marco.bianchi@test.golp" },
    @{ Name = "Sara Verdi";         Email = "sara.verdi@test.golp" },
    @{ Name = "Giulia Ferrari";     Email = "giulia.ferrari@test.golp" },
    @{ Name = "Alessandro Russo";   Email = "alessandro.russo@test.golp" },
    @{ Name = "Francesca Romano";   Email = "francesca.romano@test.golp" },
    @{ Name = "Matteo Conti";       Email = "matteo.conti@test.golp" },
    @{ Name = "Valentina Ricci";    Email = "valentina.ricci@test.golp" },
    @{ Name = "Davide Marino";      Email = "davide.marino@test.golp" },
    @{ Name = "Chiara Greco";       Email = "chiara.greco@test.golp" }
)

$tokens = @{}
$userIds = @{}

foreach ($p in $players) {
    $token = Register-User -Name $p.Name -Email $p.Email
    $id    = Get-UserId -Token $token
    $tokens[$p.Email]  = $token
    $userIds[$p.Email] = $id
    Write-Host ("      {0,-22} id={1}" -f $p.Name, $id)
}

# Shortcuts
$t = $tokens   # $t["email"] → JWT
$u = $userIds  # $u["email"] → Guid

# ── Step 4: Create circle + join ─────────────────────────────────────────────

Write-Host "`n[4/5] Creating circle and memberships..." -ForegroundColor Cyan

$creatorToken = $t["luca.rossi@test.golp"]
$circleRes = Invoke-Api -Method POST -Path "/circles" -Body @{
    name  = "Padel Club Roma"
    sport = "padel"
} -Token $creatorToken

$circleId = $circleRes.id
Write-Host ("      Circle 'Padel Club Roma' created - id={0}" -f $circleId)

# Other 9 players join
foreach ($email in ($players | Select-Object -Skip 1 | ForEach-Object { $_.Email })) {
    Invoke-Api -Method POST -Path "/circles/$circleId/join" -Token $t[$email] | Out-Null
    Write-Host ("      {0} joined" -f $email)
}

# ── Step 5: Create and confirm matches ───────────────────────────────────────

Write-Host "`n[5/5] Recording matches..." -ForegroundColor Cyan

function New-Match {
    param(
        [string]$CreatorEmail,
        [string[]]$Team1Emails,
        [string[]]$Team2Emails,
        [object[]]$Sets
    )
    $body = @{
        team1 = @($u[$Team1Emails[0]], $u[$Team1Emails[1]])
        team2 = @($u[$Team2Emails[0]], $u[$Team2Emails[1]])
        sets  = $Sets
    }
    $res = Invoke-Api -Method POST -Path "/circles/$circleId/matches" `
        -Body $body -Token $t[$CreatorEmail]
    return $res.id
}

function Confirm-Match {
    param([string]$MatchId, [string[]]$ConfirmerEmails)
    foreach ($email in $ConfirmerEmails) {
        Invoke-Api -Method POST -Path "/circles/$circleId/matches/$MatchId/confirm" `
            -Token $t[$email] | Out-Null
    }
}

# ---- Match 1: Luca+Marco vs Sara+Giulia — confirmed (Luca crea = 1/4, altri 3 confermano)
Write-Host "      Match 1: Luca+Marco vs Sara+Giulia  [6-3, 6-4] → confirmed"
$m1 = New-Match `
    -CreatorEmail "luca.rossi@test.golp" `
    -Team1Emails  @("luca.rossi@test.golp", "marco.bianchi@test.golp") `
    -Team2Emails  @("sara.verdi@test.golp", "giulia.ferrari@test.golp") `
    -Sets @(@{team1=6;team2=3}, @{team1=6;team2=4})
Confirm-Match -MatchId $m1 -ConfirmerEmails @(
    "marco.bianchi@test.golp", "sara.verdi@test.golp", "giulia.ferrari@test.golp"
)

# ---- Match 2: Alessandro+Francesca vs Matteo+Valentina — confirmed
Write-Host "      Match 2: Alessandro+Francesca vs Matteo+Valentina  [7-5, 4-6, 6-3] → confirmed"
$m2 = New-Match `
    -CreatorEmail "alessandro.russo@test.golp" `
    -Team1Emails  @("alessandro.russo@test.golp", "francesca.romano@test.golp") `
    -Team2Emails  @("matteo.conti@test.golp", "valentina.ricci@test.golp") `
    -Sets @(@{team1=7;team2=5}, @{team1=4;team2=6}, @{team1=6;team2=3})
Confirm-Match -MatchId $m2 -ConfirmerEmails @(
    "francesca.romano@test.golp", "matteo.conti@test.golp", "valentina.ricci@test.golp"
)

# ---- Match 3: Luca+Sara vs Davide+Chiara — confirmed
Write-Host "      Match 3: Luca+Sara vs Davide+Chiara  [3-6, 6-2, 7-5] → confirmed"
$m3 = New-Match `
    -CreatorEmail "luca.rossi@test.golp" `
    -Team1Emails  @("luca.rossi@test.golp", "sara.verdi@test.golp") `
    -Team2Emails  @("davide.marino@test.golp", "chiara.greco@test.golp") `
    -Sets @(@{team1=3;team2=6}, @{team1=6;team2=2}, @{team1=7;team2=5})
Confirm-Match -MatchId $m3 -ConfirmerEmails @(
    "sara.verdi@test.golp", "davide.marino@test.golp", "chiara.greco@test.golp"
)

# ---- Match 4: Marco+Giulia vs Alessandro+Matteo — confirmed
Write-Host "      Match 4: Marco+Giulia vs Alessandro+Matteo  [6-4, 7-6] → confirmed"
$m4 = New-Match `
    -CreatorEmail "marco.bianchi@test.golp" `
    -Team1Emails  @("marco.bianchi@test.golp", "giulia.ferrari@test.golp") `
    -Team2Emails  @("alessandro.russo@test.golp", "matteo.conti@test.golp") `
    -Sets @(@{team1=6;team2=4}, @{team1=7;team2=6})
Confirm-Match -MatchId $m4 -ConfirmerEmails @(
    "giulia.ferrari@test.golp", "alessandro.russo@test.golp", "matteo.conti@test.golp"
)

# ---- Match 5: Francesca+Valentina vs Luca+Chiara — confirmed
Write-Host "      Match 5: Francesca+Valentina vs Luca+Chiara  [6-3, 6-4] → confirmed"
$m5 = New-Match `
    -CreatorEmail "francesca.romano@test.golp" `
    -Team1Emails  @("francesca.romano@test.golp", "valentina.ricci@test.golp") `
    -Team2Emails  @("luca.rossi@test.golp", "chiara.greco@test.golp") `
    -Sets @(@{team1=6;team2=3}, @{team1=6;team2=4})
Confirm-Match -MatchId $m5 -ConfirmerEmails @(
    "valentina.ricci@test.golp", "luca.rossi@test.golp", "chiara.greco@test.golp"
)

# ---- Match 6: Davide+Giulia vs Marco+Sara — confirmed (Team2 wins)
Write-Host "      Match 6: Davide+Giulia vs Marco+Sara  [4-6, 6-3, 3-6] → confirmed (T2)"
$m6 = New-Match `
    -CreatorEmail "davide.marino@test.golp" `
    -Team1Emails  @("davide.marino@test.golp", "giulia.ferrari@test.golp") `
    -Team2Emails  @("marco.bianchi@test.golp", "sara.verdi@test.golp") `
    -Sets @(@{team1=4;team2=6}, @{team1=6;team2=3}, @{team1=3;team2=6})
Confirm-Match -MatchId $m6 -ConfirmerEmails @(
    "giulia.ferrari@test.golp", "marco.bianchi@test.golp", "sara.verdi@test.golp"
)

# ---- Match 7: Luca+Matteo vs Francesca+Alessandro — pending (solo 2/4 confermano)
Write-Host "      Match 7: Luca+Matteo vs Francesca+Alessandro  [6-2, 3-6, 6-1] → pending (2/4)"
$m7 = New-Match `
    -CreatorEmail "luca.rossi@test.golp" `
    -Team1Emails  @("luca.rossi@test.golp", "matteo.conti@test.golp") `
    -Team2Emails  @("francesca.romano@test.golp", "alessandro.russo@test.golp") `
    -Sets @(@{team1=6;team2=2}, @{team1=3;team2=6}, @{team1=6;team2=1})
Confirm-Match -MatchId $m7 -ConfirmerEmails @("matteo.conti@test.golp")

# ---- Match 8: Chiara+Valentina vs Giulia+Davide — pending (solo il creatore = 1/4)
Write-Host "      Match 8: Chiara+Valentina vs Giulia+Davide  [7-5, 6-4] → pending (1/4)"
$m8 = New-Match `
    -CreatorEmail "chiara.greco@test.golp" `
    -Team1Emails  @("chiara.greco@test.golp", "valentina.ricci@test.golp") `
    -Team2Emails  @("giulia.ferrari@test.golp", "davide.marino@test.golp") `
    -Sets @(@{team1=7;team2=5}, @{team1=6;team2=4})
# nessuna conferma aggiuntiva → rimane 1/4

} finally {
    # ── Cleanup: stop API ────────────────────────────────────────────────────
    Write-Host "`nStopping API..." -ForegroundColor DarkGray
    Stop-Job  $apiJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -ErrorAction SilentlyContinue
}

# ── Summary ──────────────────────────────────────────────────────────────────

Write-Host @"

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 SEED COMPLETATO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 Circolo  : Padel Club Roma  (id=$circleId)
 Giocatori: 10
 Partite  : 8  (6 confirmed, 2 pending)
 Password : testgolp  (uguale per tutti)

 Utenti:
   luca.rossi@test.golp       (owner del circolo)
   marco.bianchi@test.golp
   sara.verdi@test.golp
   giulia.ferrari@test.golp
   alessandro.russo@test.golp
   francesca.romano@test.golp
   matteo.conti@test.golp
   valentina.ricci@test.golp
   davide.marino@test.golp
   chiara.greco@test.golp
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"@ -ForegroundColor Green
