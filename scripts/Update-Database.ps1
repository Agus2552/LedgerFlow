$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet ef database update --project src/Modules/Wallets/Wallets.Infrastructure --startup-project src/LedgerFlow.Api --context WalletsDbContext -- --environment Development
    if ($LASTEXITCODE -ne 0) { throw 'Database migration failed. Check Docker and local database configuration.' }
} finally {
    Pop-Location
}
