$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) {
    throw 'Create .env from .env.example and choose a local password first.'
}
$passwordLine = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^POSTGRES_PASSWORD=' } | Select-Object -First 1
if (-not $passwordLine) { throw 'POSTGRES_PASSWORD is missing in .env.' }
$password = $passwordLine.Substring('POSTGRES_PASSWORD='.Length)
# Quote the password for the connection-string grammar, including embedded quotes.
$quotedPassword = '"' + $password.Replace('"', '""') + '"'
$connectionString = 'Host=127.0.0.1;Port=5433;Database=ledgerflow;Username=ledgerflow;Password=' + $quotedPassword
@{ 'ConnectionStrings:LedgerFlow' = $connectionString } | ConvertTo-Json -Compress |
    dotnet user-secrets set --project (Join-Path $projectRoot 'src\LedgerFlow.Api')
if ($LASTEXITCODE -ne 0) { throw 'Could not configure user secrets.' }
Write-Output 'Local database connection configured for Development. Password was not printed.'
