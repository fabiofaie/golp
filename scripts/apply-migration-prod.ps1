# Applica migrations EF Core pendenti sul DB di Production (eqproject.database.windows.net / GolpDb).
# Uso: .\scripts\apply-migration-prod.ps1
# Chiede conferma esplicita prima di eseguire (operazione su DB prod).

$ErrorActionPreference = "Stop"

$connString = "Server=eqproject.database.windows.net;Database=GolpDb;User Id=golpuser;Password=S1.V3d3!M3gl10;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
$project = "src/Golp.Api"

Write-Host "Target: eqproject.database.windows.net / GolpDb (PRODUCTION)" -ForegroundColor Yellow
Write-Host "Migration pendente attesa: 20260702120304_AddSinglesSupport" -ForegroundColor Yellow

$confirm = Read-Host "Confermi applicazione migration su PROD? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Annullato." -ForegroundColor Red
    exit 1
}

Write-Host "`nMigrations pendenti prima dell'update:" -ForegroundColor Cyan
dotnet ef migrations list --project $project --connection $connString

Write-Host "`nApplico migrations..." -ForegroundColor Cyan
dotnet ef database update --project $project --connection $connString

Write-Host "`nDone. Verifica colonna IsSingle:" -ForegroundColor Green
sqlcmd -S eqproject.database.windows.net -d GolpDb -U golpuser -P 'S1.V3d3!M3gl10' -C -Q "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Matches' AND COLUMN_NAME='IsSingle'"
