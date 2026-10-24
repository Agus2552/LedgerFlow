# LedgerFlow

A learning-focused modular monolith for a simulated wallet API. No real money or bank integrations.

## Current scope

ASP.NET Core API with liveness/readiness probes, Swagger, a Postman collection,
PostgreSQL Compose configuration, and a wallet creation/retrieval vertical slice.
See [the architecture decision](docs/adr/0001-foundation.md).

Stage 02 introduced an immutable USD `Money` value object and unit tests in
a dependency-free SharedKernel. Amounts use integer cents, with exact decimal
conversion and overflow checks. Zero and negative values are representable;
transfer positivity will be enforced by the transfer operation. See
[the money decision](docs/adr/0003-money-representation.md).
Wallet models ownership, USD currency and an Active-to-Closed lifecycle. Ledger
models balances, positive postings and immutable balanced journal entries with
reversals. Transfers models positive payment intent and terminal outcomes.
Stage 03 adds POST /wallets and GET /wallets/{id}, application handlers, EF Core
mapping and a migration in the wallets schema. It creates wallet metadata only.
Atomic transfer execution, balances through the API, and security remain future work.
See [the domain model](docs/architecture/stage-02-domain-model.md) and
[the consistency decision](docs/adr/0005-ledger-and-transfer-aggregates.md).

## Prerequisites and execution

.NET SDK 10.0.400 (or a later patch in the same feature band), Docker Desktop with
Linux containers, and Docker Compose are required. Open LedgerFlow.slnx in Visual Studio.

From this directory in PowerShell:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Edit .env and choose a local development password before starting PostgreSQL.
docker compose up -d --wait
./scripts/Set-LocalDatabase.ps1
./scripts/Update-Database.ps1
docker compose exec -T postgres psql -U ledgerflow -d ledgerflow -c 'SELECT 1;'
dotnet build
dotnet test --no-build
dotnet run --project src/LedgerFlow.Api --launch-profile http
```

In another terminal:

```powershell
Invoke-WebRequest http://localhost:5121/health/live
Invoke-WebRequest http://localhost:5121/health/ready
```

Expected response: HTTP 200, body `Healthy`. This checks API liveness only;
it does not check database connectivity. Readiness executes `SELECT 1` and returns
200 `Healthy` or 503 `Unhealthy`. This verifies connectivity, not business schema correctness.
PostgreSQL is exposed on localhost port 5433 to avoid common local port conflicts.

The setup script copies the existing `.env` password into .NET user secrets for
the API's Development environment. User secrets are local configuration, not an
encrypted production secret store. In other environments provide
`ConnectionStrings__LedgerFlow` through the environment or a secret provider.
ASP.NET Core does not automatically load Docker's `.env` file.
If the named volume already exists, changing POSTGRES_PASSWORD does not change
the database user's password; keep the configuration aligned with the initialized database.

Stop the API with Ctrl+C. Stop PostgreSQL with `docker compose down`; its named
volume preserves data. Do not commit .env or local learning notes.

The database script restores the repository-local dotnet-ef tool and explicitly
applies migrations. API startup does not modify the schema. Tests require Docker:
wallet HTTP tests create and dispose an isolated PostgreSQL Testcontainer, apply
the same migration and leave your development data alone. Unit tests can run without
Docker using `dotnet test tests/LedgerFlow.UnitTests`.

## Wallet endpoints

```http
POST /wallets
Content-Type: application/json

{"ownerId":"11111111-1111-4111-8111-111111111111","currency":"USD"}
```

Returns 201, a Location header and `{id, ownerId, currency, status}`. Follow Location
with GET to retrieve the wallet (200); an unknown ID returns 404. Invalid owner or
currency returns 400 validation Problem Details. Transient database failures return
generic 503 Problem Details. Multiple wallets per owner/currency are allowed.

This is a local learning API: the endpoints are not authenticated yet, ownerId is
client-supplied, owner existence is not verified, and POST is not idempotent.
Repeated POSTs create separate wallets. No Ledger account, funding, balance or
closure is exposed. Do not expose publicly before the security stage.

## Manual testing

Run `dotnet run --project src/LedgerFlow.Api --launch-profile http`, then open
http://localhost:5121/swagger. Visual Studio's HTTP and HTTPS profiles open Swagger
automatically. Swagger is available only in Development; running with
`--no-launch-profile` does not automatically enable it.

Import `postman collection/LedgerFlow.postman_collection.json` into Postman.
The collection variable `baseUrl` defaults to `http://localhost:5121`; edit it if
you use a different port, such as 5080 in the commands above. Send the liveness
request and inspect its status/body assertions in Test Results.

Swagger describes the HTTP contract; Swagger UI lets you execute it. The Postman
collection saves reusable requests and response assertions. Health and wallet requests
are included. Run the collection in order: Create stores walletId used by Get.
The health requests expect a healthy local stack. To test a database
outage manually, stop PostgreSQL with `docker compose stop postgres`: readiness
must return 503 while liveness stays 200. Restore it with `docker compose up -d --wait`.

## Verification results

Stage 03 verified locally: `dotnet build LedgerFlow.slnx` succeeded with zero
warnings/errors; `dotnet test LedgerFlow.slnx --no-build` passed all 54 cases
(36 domain unit cases, 18 API integration cases). PostgreSQL Testcontainers exercised
the migration, persistence, validation, constraints, rehydration and failure responses.
The local development database migration was also applied successfully.
Postman CLI passed 7 requests and 14 assertions; Swagger documented both wallet routes.
The temporary API process was stopped. PostgreSQL remains running.

Earlier foundation checks also verified readiness 503 and liveness 200 during a
deliberate PostgreSQL outage. Stage 03 implementation is complete; financial
operations remain Stage 04.
Stage 02 review is available in [the interview guide](docs/stage-01-02-interview-review.md).
See the [Stage 03 summary and 10 interview questions](docs/stage-03-interview-review.md)
for its architecture, commands, limitations and verification record.
