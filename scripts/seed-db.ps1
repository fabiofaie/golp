# seed-db.ps1 — Popola il DB locale con dati di demo
# Crea 20 giocatori, 1 circolo Padel, 40 partite confirmed + alcune pending
# Tutti gli utenti hanno password: demogolp

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
        password = "demogolp"
    }
    return $res.token
}

function Get-UserId {
    param([string]$Token)
    $parts = $Token.Split(".")
    $payload = $parts[1]
    $mod = $payload.Length % 4
    if ($mod -ne 0) { $payload += "=" * (4 - $mod) }
    $payload = $payload.Replace("-", "+").Replace("_", "/")
    $json = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload))
    $obj  = $json | ConvertFrom-Json
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

# ── Step 3: Register 20 players ──────────────────────────────────────────────

Write-Host "`n[3/5] Registering players..." -ForegroundColor Cyan

$players = @(
    @{ Name = "Luca Rossi";           Email = "luca.rossi@demo.golp" },
    @{ Name = "Marco Bianchi";        Email = "marco.bianchi@demo.golp" },
    @{ Name = "Sara Verdi";           Email = "sara.verdi@demo.golp" },
    @{ Name = "Giulia Ferrari";       Email = "giulia.ferrari@demo.golp" },
    @{ Name = "Alessandro Russo";     Email = "alessandro.russo@demo.golp" },
    @{ Name = "Francesca Romano";     Email = "francesca.romano@demo.golp" },
    @{ Name = "Matteo Conti";         Email = "matteo.conti@demo.golp" },
    @{ Name = "Valentina Ricci";      Email = "valentina.ricci@demo.golp" },
    @{ Name = "Davide Marino";        Email = "davide.marino@demo.golp" },
    @{ Name = "Chiara Greco";         Email = "chiara.greco@demo.golp" },
    @{ Name = "Simone Esposito";      Email = "simone.esposito@demo.golp" },
    @{ Name = "Laura Brun";           Email = "laura.brun@demo.golp" },
    @{ Name = "Federico Gallo";       Email = "federico.gallo@demo.golp" },
    @{ Name = "Alessia Colombo";      Email = "alessia.colombo@demo.golp" },
    @{ Name = "Roberto Fontana";      Email = "roberto.fontana@demo.golp" },
    @{ Name = "Elena Mancini";        Email = "elena.mancini@demo.golp" },
    @{ Name = "Giacomo Barbieri";     Email = "giacomo.barbieri@demo.golp" },
    @{ Name = "Martina Serra";        Email = "martina.serra@demo.golp" },
    @{ Name = "Antonio Pellegrini";   Email = "antonio.pellegrini@demo.golp" },
    @{ Name = "Beatrice Lombardi";    Email = "beatrice.lombardi@demo.golp" }
)

$tokens  = @{}
$userIds = @{}

foreach ($p in $players) {
    $token = Register-User -Name $p.Name -Email $p.Email
    $id    = Get-UserId -Token $token
    $tokens[$p.Email]  = $token
    $userIds[$p.Email] = $id
    Write-Host ("      {0,-26} id={1}" -f $p.Name, $id)
}

$t = $tokens
$u = $userIds

# ── Step 4: Create circle + join ─────────────────────────────────────────────

Write-Host "`n[4/5] Creating circle and memberships..." -ForegroundColor Cyan

$creatorToken = $t["luca.rossi@demo.golp"]
$circleRes = Invoke-Api -Method POST -Path "/circles" -Body @{
    name  = "Padel Club Roma"
    sport = "padel"
} -Token $creatorToken

$circleId = $circleRes.id
Write-Host ("      Circle 'Padel Club Roma' created - id={0}" -f $circleId)

foreach ($email in ($players | Select-Object -Skip 1 | ForEach-Object { $_.Email })) {
    Invoke-Api -Method POST -Path "/circles/$circleId/join" -Token $t[$email] | Out-Null
    Write-Host ("      {0} joined" -f $email)
}

# ── Step 5: Create and confirm 40 matches ─────────────────────────────────────

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
    param([string]$MatchId, [string[]]$AllEmails)
    # Skip first (creator auto-confirmed), confirm the other 3
    foreach ($email in $AllEmails | Select-Object -Skip 1) {
        Invoke-Api -Method POST -Path "/circles/$circleId/matches/$MatchId/confirm" `
            -Token $t[$email] | Out-Null
    }
}

# Helper: confirm all 4 players (creator = first in Team1)
function Confirm-All {
    param([string]$MatchId, [string[]]$Team1, [string[]]$Team2)
    Confirm-Match -MatchId $MatchId -AllEmails @($Team1[0], $Team1[1], $Team2[0], $Team2[1])
}

# 40 partite confirmed
$matchDefs = @(
    # T1p1                          T1p2                         T2p1                       T2p2                      Sets
    @{ T1=@("luca.rossi@demo.golp","marco.bianchi@demo.golp");       T2=@("sara.verdi@demo.golp","giulia.ferrari@demo.golp");      S=@(@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("alessandro.russo@demo.golp","francesca.romano@demo.golp"); T2=@("matteo.conti@demo.golp","valentina.ricci@demo.golp"); S=@(@{team1=7;team2=5},@{team1=4;team2=6},@{team1=6;team2=3}) },
    @{ T1=@("luca.rossi@demo.golp","sara.verdi@demo.golp");          T2=@("davide.marino@demo.golp","chiara.greco@demo.golp");     S=@(@{team1=3;team2=6},@{team1=6;team2=2},@{team1=7;team2=5}) },
    @{ T1=@("marco.bianchi@demo.golp","giulia.ferrari@demo.golp");   T2=@("alessandro.russo@demo.golp","matteo.conti@demo.golp");  S=@(@{team1=6;team2=4},@{team1=7;team2=6}) },
    @{ T1=@("francesca.romano@demo.golp","valentina.ricci@demo.golp"); T2=@("luca.rossi@demo.golp","chiara.greco@demo.golp");      S=@(@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("davide.marino@demo.golp","giulia.ferrari@demo.golp");   T2=@("marco.bianchi@demo.golp","sara.verdi@demo.golp");       S=@(@{team1=4;team2=6},@{team1=6;team2=3},@{team1=3;team2=6}) },
    @{ T1=@("simone.esposito@demo.golp","laura.brun@demo.golp");     T2=@("federico.gallo@demo.golp","alessia.colombo@demo.golp"); S=@(@{team1=6;team2=2},@{team1=6;team2=3}) },
    @{ T1=@("roberto.fontana@demo.golp","elena.mancini@demo.golp");  T2=@("giacomo.barbieri@demo.golp","martina.serra@demo.golp"); S=@(@{team1=7;team2=5},@{team1=6;team2=4}) },
    @{ T1=@("antonio.pellegrini@demo.golp","beatrice.lombardi@demo.golp"); T2=@("luca.rossi@demo.golp","marco.bianchi@demo.golp"); S=@(@{team1=3;team2=6},@{team1=4;team2=6}) },
    @{ T1=@("simone.esposito@demo.golp","federico.gallo@demo.golp"); T2=@("sara.verdi@demo.golp","valentina.ricci@demo.golp");     S=@(@{team1=6;team2=4},@{team1=6;team2=2}) },
    @{ T1=@("chiara.greco@demo.golp","laura.brun@demo.golp");        T2=@("alessia.colombo@demo.golp","beatrice.lombardi@demo.golp"); S=@(@{team1=6;team2=3},@{team1=5;team2=7},@{team1=6;team2=4}) },
    @{ T1=@("luca.rossi@demo.golp","simone.esposito@demo.golp");     T2=@("roberto.fontana@demo.golp","giacomo.barbieri@demo.golp"); S=@(@{team1=6;team2=1},@{team1=6;team2=3}) },
    @{ T1=@("marco.bianchi@demo.golp","elena.mancini@demo.golp");    T2=@("antonio.pellegrini@demo.golp","martina.serra@demo.golp"); S=@(@{team1=7;team2=6},@{team1=6;team2=4}) },
    @{ T1=@("giulia.ferrari@demo.golp","beatrice.lombardi@demo.golp"); T2=@("francesca.romano@demo.golp","laura.brun@demo.golp");  S=@(@{team1=4;team2=6},@{team1=6;team2=3},@{team1=4;team2=6}) },
    @{ T1=@("davide.marino@demo.golp","federico.gallo@demo.golp");   T2=@("alessia.colombo@demo.golp","chiara.greco@demo.golp");   S=@(@{team1=6;team2=2},@{team1=6;team2=4}) },
    @{ T1=@("valentina.ricci@demo.golp","martina.serra@demo.golp");  T2=@("sara.verdi@demo.golp","elena.mancini@demo.golp");       S=@(@{team1=6;team2=4},@{team1=3;team2=6},@{team1=7;team2=5}) },
    @{ T1=@("luca.rossi@demo.golp","beatrice.lombardi@demo.golp");   T2=@("simone.esposito@demo.golp","roberto.fontana@demo.golp"); S=@(@{team1=6;team2=3},@{team1=6;team2=2}) },
    @{ T1=@("marco.bianchi@demo.golp","giacomo.barbieri@demo.golp"); T2=@("antonio.pellegrini@demo.golp","federico.gallo@demo.golp"); S=@(@{team1=5;team2=7},@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("giulia.ferrari@demo.golp","laura.brun@demo.golp");      T2=@("chiara.greco@demo.golp","alessia.colombo@demo.golp");   S=@(@{team1=6;team2=4},@{team1=6;team2=3}) },
    @{ T1=@("francesca.romano@demo.golp","martina.serra@demo.golp"); T2=@("valentina.ricci@demo.golp","beatrice.lombardi@demo.golp"); S=@(@{team1=3;team2=6},@{team1=6;team2=4},@{team1=3;team2=6}) },
    @{ T1=@("davide.marino@demo.golp","elena.mancini@demo.golp");    T2=@("luca.rossi@demo.golp","giulia.ferrari@demo.golp");       S=@(@{team1=2;team2=6},@{team1=4;team2=6}) },
    @{ T1=@("simone.esposito@demo.golp","alessia.colombo@demo.golp"); T2=@("marco.bianchi@demo.golp","francesca.romano@demo.golp"); S=@(@{team1=6;team2=4},@{team1=7;team2=5}) },
    @{ T1=@("roberto.fontana@demo.golp","martina.serra@demo.golp");  T2=@("sara.verdi@demo.golp","chiara.greco@demo.golp");         S=@(@{team1=4;team2=6},@{team1=3;team2=6}) },
    @{ T1=@("giacomo.barbieri@demo.golp","beatrice.lombardi@demo.golp"); T2=@("matteo.conti@demo.golp","federico.gallo@demo.golp"); S=@(@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("antonio.pellegrini@demo.golp","laura.brun@demo.golp");  T2=@("valentina.ricci@demo.golp","elena.mancini@demo.golp");  S=@(@{team1=6;team2=4},@{team1=4;team2=6},@{team1=6;team2=3}) },
    @{ T1=@("luca.rossi@demo.golp","matteo.conti@demo.golp");        T2=@("simone.esposito@demo.golp","giacomo.barbieri@demo.golp"); S=@(@{team1=6;team2=2},@{team1=6;team2=4}) },
    @{ T1=@("marco.bianchi@demo.golp","sara.verdi@demo.golp");       T2=@("roberto.fontana@demo.golp","alessia.colombo@demo.golp"); S=@(@{team1=7;team2=5},@{team1=6;team2=3}) },
    @{ T1=@("giulia.ferrari@demo.golp","chiara.greco@demo.golp");    T2=@("laura.brun@demo.golp","martina.serra@demo.golp");        S=@(@{team1=6;team2=4},@{team1=6;team2=1}) },
    @{ T1=@("davide.marino@demo.golp","antonio.pellegrini@demo.golp"); T2=@("federico.gallo@demo.golp","beatrice.lombardi@demo.golp"); S=@(@{team1=3;team2=6},@{team1=6;team2=4},@{team1=4;team2=6}) },
    @{ T1=@("francesca.romano@demo.golp","elena.mancini@demo.golp"); T2=@("alessia.colombo@demo.golp","giacomo.barbieri@demo.golp"); S=@(@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("valentina.ricci@demo.golp","luca.rossi@demo.golp");     T2=@("matteo.conti@demo.golp","simone.esposito@demo.golp");    S=@(@{team1=6;team2=4},@{team1=4;team2=6},@{team1=7;team2=5}) },
    @{ T1=@("marco.bianchi@demo.golp","beatrice.lombardi@demo.golp"); T2=@("sara.verdi@demo.golp","roberto.fontana@demo.golp");    S=@(@{team1=6;team2=4},@{team1=6;team2=3}) },
    @{ T1=@("giulia.ferrari@demo.golp","antonio.pellegrini@demo.golp"); T2=@("chiara.greco@demo.golp","federico.gallo@demo.golp"); S=@(@{team1=7;team2=6},@{team1=6;team2=4}) },
    @{ T1=@("davide.marino@demo.golp","laura.brun@demo.golp");       T2=@("martina.serra@demo.golp","elena.mancini@demo.golp");     S=@(@{team1=6;team2=3},@{team1=3;team2=6},@{team1=6;team2=4}) },
    @{ T1=@("simone.esposito@demo.golp","giacomo.barbieri@demo.golp"); T2=@("luca.rossi@demo.golp","francesca.romano@demo.golp");  S=@(@{team1=4;team2=6},@{team1=3;team2=6}) },
    @{ T1=@("alessia.colombo@demo.golp","matteo.conti@demo.golp");   T2=@("valentina.ricci@demo.golp","antonio.pellegrini@demo.golp"); S=@(@{team1=6;team2=4},@{team1=5;team2=7},@{team1=6;team2=3}) },
    @{ T1=@("roberto.fontana@demo.golp","beatrice.lombardi@demo.golp"); T2=@("marco.bianchi@demo.golp","chiara.greco@demo.golp");  S=@(@{team1=3;team2=6},@{team1=4;team2=6}) },
    @{ T1=@("sara.verdi@demo.golp","giacomo.barbieri@demo.golp");    T2=@("giulia.ferrari@demo.golp","simone.esposito@demo.golp"); S=@(@{team1=6;team2=3},@{team1=6;team2=4}) },
    @{ T1=@("luca.rossi@demo.golp","elena.mancini@demo.golp");       T2=@("davide.marino@demo.golp","martina.serra@demo.golp");    S=@(@{team1=6;team2=4},@{team1=7;team2=5}) },
    @{ T1=@("federico.gallo@demo.golp","chiara.greco@demo.golp");    T2=@("antonio.pellegrini@demo.golp","laura.brun@demo.golp");  S=@(@{team1=6;team2=2},@{team1=6;team2=3}) }
)

$idx = 1
foreach ($m in $matchDefs) {
    $creator = $m.T1[0]
    $mid = New-Match -CreatorEmail $creator -Team1Emails $m.T1 -Team2Emails $m.T2 -Sets $m.S
    Confirm-All -MatchId $mid -Team1 $m.T1 -Team2 $m.T2
    Write-Host ("      Match {0,2}: {1}+{2} vs {3}+{4} → confirmed" -f `
        $idx,
        ($m.T1[0] -split '@')[0],
        ($m.T1[1] -split '@')[0],
        ($m.T2[0] -split '@')[0],
        ($m.T2[1] -split '@')[0])
    $idx++
}

# 2 partite pending
Write-Host "      Match 41: luca+matteo vs francesca+roberto → pending (2/4)"
$mp1 = New-Match -CreatorEmail "luca.rossi@demo.golp" `
    -Team1Emails @("luca.rossi@demo.golp","matteo.conti@demo.golp") `
    -Team2Emails @("francesca.romano@demo.golp","roberto.fontana@demo.golp") `
    -Sets @(@{team1=6;team2=2},@{team1=3;team2=6},@{team1=6;team2=1})
Invoke-Api -Method POST -Path "/circles/$circleId/matches/$mp1/confirm" -Token $t["matteo.conti@demo.golp"] | Out-Null

Write-Host "      Match 42: chiara+simone vs alessia+elena → pending (1/4)"
New-Match -CreatorEmail "chiara.greco@demo.golp" `
    -Team1Emails @("chiara.greco@demo.golp","simone.esposito@demo.golp") `
    -Team2Emails @("alessia.colombo@demo.golp","elena.mancini@demo.golp") `
    -Sets @(@{team1=7;team2=5},@{team1=6;team2=4}) | Out-Null

} finally {
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
 Giocatori: 20
 Partite  : 42  (40 confirmed, 2 pending)
 Password : demogolp  (uguale per tutti)

 Utenti (email @demo.golp):
   luca.rossi          (owner del circolo)
   marco.bianchi
   sara.verdi
   giulia.ferrari
   alessandro.russo
   francesca.romano
   matteo.conti
   valentina.ricci
   davide.marino
   chiara.greco
   simone.esposito
   laura.brun
   federico.gallo
   alessia.colombo
   roberto.fontana
   elena.mancini
   giacomo.barbieri
   martina.serra
   antonio.pellegrini
   beatrice.lombardi
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"@ -ForegroundColor Green
