# Applica migrations EF Core pendenti sul DB di Test (eqproject.database.windows.net / GolpDbTest).
# Uso: .\scripts\apply-migration-test.ps1
# Chiede conferma esplicita prima di eseguire.

$ErrorActionPreference = "Stop"

$connString = "Server=eqproject.database.windows.net;Database=GolpDbTest;User Id=golpuser;Password=S1.V3d3!M3gl10;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$project = "src/Golp.Api"

Write-Host "Target: eqproject.database.windows.net / GolpDbTest (TEST)" -ForegroundColor Yellow

$confirm = Read-Host "Confermi applicazione migration su TEST? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Annullato." -ForegroundColor Red
    exit 1
}

Write-Host "`nMigrations pendenti prima dell'update:" -ForegroundColor Cyan
dotnet ef migrations list --project $project --connection $connString

Write-Host "`nApplico migrations..." -ForegroundColor Cyan
dotnet ef database update --project $project --connection $connString

Write-Host "`nMigrations applicate dopo l'update:" -ForegroundColor Green
dotnet ef migrations list --project $project --connection $connString
