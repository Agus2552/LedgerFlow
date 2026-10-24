# LedgerFlow — Stage 03: Summary and Interview Review

## What we built and why

Stage 03 turns the Wallet model into a vertical slice: a real request crosses HTTP,
application logic, domain validation, EF Core, and PostgreSQL. Stage 02 rules now
have a persistence and transport path rather than existing only in unit tests.

### Behavior

| Request | Expected result |
| --- | --- |
| POST /wallets with a non-empty ownerId and currency USD | 201 Created, wallet JSON, Location header |
| GET /wallets/{created-id} | 200 with the persisted wallet |
| POST with empty ownerId or unsupported/missing currency | 400 validation problem |
| POST with malformed JSON or invalid GUID fields | 400 problem |
| GET /wallets/{unknown-guid} | 404 problem |
| Database temporarily unavailable | Generic 503 problem |

Example request:

```json
{
  "ownerId": "11111111-1111-4111-8111-111111111111",
  "currency": "USD"
}
```

The response contains id, ownerId, currency and status. No balance is returned,
because this stage creates metadata only. It does not create a Ledger account.

### Request flow and responsibilities

```text
POST /wallets
  -> CreateWalletRequest (HTTP DTO)
  -> CreateWalletCommand
  -> CreateWalletHandler (validation and orchestration)
  -> Wallet.Open (domain invariants)
  -> IWalletStore.AddAsync
  -> EfWalletStore / WalletsDbContext
  -> PostgreSQL wallets.wallets
  -> WalletResponse
  -> 201 Created + Location
```

- **Domain** owns valid Wallet construction and lifecycle; no EF or HTTP references.
- **Application** owns command/query handlers, DTO mapping and its store contract.
- **Infrastructure** implements that contract using EF Core and PostgreSQL.
- **API** owns routing, HTTP status codes and dependency registration.

Runtime calls travel toward storage; source dependencies point toward application
contracts and domain concepts. Infrastructure depends on Application to implement
IWalletStore. Application does not depend on Infrastructure.

### Decisions worth defending

- Command and query handlers are separate, but use one database and no MediatR.
- A small wallet-specific store has a clear purpose; there is no generic repository.
- Read operations use AsNoTracking because this response does not modify the entity.
- Explicit DTOs keep the HTTP contract separate from domain internals.
- Wallet's private constructor now restores status rather than always assigning Active.
- A generated migration creates the table, checks, owner index and migration history.
- Migrations run explicitly, not as a side effect of every API startup.
- PostgreSQL constraints reinforce domain rules against direct invalid writes.
- Problem Details standardizes failures without publishing provider exceptions or passwords.
- Scoped handlers/store/DbContext share one request lifetime. DbContext is not a singleton
  and must not be used concurrently by multiple threads.
- Testcontainers supplies a temporary real PostgreSQL database, separate from the local
  development database. Tests apply the actual migrations before exercising HTTP.

### How to run and explore

From the repository root, with Docker Desktop running:

```powershell
docker compose up -d --wait
./scripts/Set-LocalDatabase.ps1
./scripts/Update-Database.ps1
dotnet test LedgerFlow.slnx
dotnet run --project src/LedgerFlow.Api --launch-profile http
```

Open http://localhost:5121/swagger. Import the updated
`postman collection/LedgerFlow.postman_collection.json` and run requests in order.
Create saves walletId for the subsequent Get request. You can change ownerId/baseUrl.
Every successful POST creates another wallet, including repeated identical requests.
Stop the API with Ctrl+C before rebuilding if Windows reports the executable is locked.

### Current limitations

There is no authentication, source-of-truth owner validation, wallet-create idempotency,
balance endpoint, financial transaction, or close endpoint. ownerId is client-supplied
and must later be derived from or checked against authenticated identity. Run this
learning API locally. Multiple wallets for the same owner and currency are allowed.
No client should assume that retrying POST after a lost response is safe from duplicates.

## Interview practice

Think through each question before expanding its answer.

### 1. What makes this a vertical slice instead of another horizontal layer?

<details>
<summary>Show answer</summary>

“It delivers one usable business capability across all necessary layers: HTTP input,
application orchestration, domain construction, persistence and an HTTP response.
We can create a wallet and fetch it in a separate request. We did not build every
module's infrastructure before delivering behavior.”

The slice still respects architectural boundaries; vertical delivery and layered
dependencies describe different aspects of the design.

</details>

### 2. Why do you have both an HTTP request DTO and an application command?

<details>
<summary>Show answer</summary>

“The request DTO represents our transport contract; the command represents use-case
intent. Their fields currently match, but they can evolve independently. The API
maps between them without making Application depend on ASP.NET.”

The response DTO also prevents serialization from turning domain properties into
an accidental public contract. Small explicit mappings do not require AutoMapper.

</details>

### 3. Is this CQRS even though you do not use MediatR or separate databases?

<details>
<summary>Show answer</summary>

“Yes, in a pragmatic form: CreateWalletCommand changes state and GetWalletQuery
retrieves it, with separate handlers. CQRS is a separation of responsibilities,
not a requirement to use a mediator, events or two databases.”

A mediator might be useful later for justified pipeline behavior, but would add
indirection here without changing the fundamental design.

</details>

### 4. Why define IWalletStore rather than let the handler use DbContext directly?

<details>
<summary>Show answer</summary>

“The application owns a small contract describing the persistence capabilities it
needs. EF Infrastructure implements it, so Application remains independent of EF
and PostgreSQL. This is a concrete use of dependency inversion.”

Using DbContext directly would be simpler in a different architecture, but would
couple this application layer to EF. We did not add a generic repository or expose
IQueryable. AddAsync saves the one aggregate; it must be revisited when shared
multi-module transactions become necessary.

</details>

### 5. Why validate at the application boundary, in Domain, and in the database?

<details>
<summary>Show answer</summary>

“They protect different boundaries. Application collects actionable input errors;
Domain refuses invalid Wallet construction regardless of caller; database checks
protect stored data even when writes bypass the application.”

GUID parsing and malformed JSON belong to transport binding. An empty owner and
unsupported currency are application/domain validation. Owner existence and
authorization cannot be established by checking a GUID's format.

</details>

### 6. What do EF migrations do, and why not use EnsureCreated or migrate on startup?

<details>
<summary>Show answer</summary>

“A migration records an explicit schema change, and EF tracks which migrations
have been applied. The model snapshot supports generating subsequent changes.
We apply migrations explicitly so schema changes can be reviewed and controlled.”

EnsureCreated is not a versioned migration workflow. Startup migration can be
convenient locally, but introduces deployment coordination and permission concerns.
Our tests apply real migrations so the schema workflow itself is exercised.

</details>

### 7. How do you persist an encapsulated entity without adding public setters?

<details>
<summary>Show answer</summary>

“EF configuration explicitly maps properties and uses a private constructor with
matching parameter names/types. Wallet's public factory still protects new creation,
while persistence rehydrates saved identity, ownership, currency and status.”

Including status in that constructor matters: loading a Closed wallet must not
silently reset it to Active. We test a saved Closed wallet through a fresh request.

</details>

### 8. Why are DbContext and handlers scoped, and why use AsNoTracking for GET?

<details>
<summary>Show answer</summary>

“A scoped lifetime gives the request its own unit of work and tracked state.
DbContext is not thread-safe and should not be a singleton shared across requests.
Handlers and the store share the request's scoped context.”

AsNoTracking avoids tracking entities we only read and map to response DTOs. It is
a suitable default here, not a claim that all no-tracking queries are always faster.

</details>

### 9. Which HTTP statuses do you return, and how do you avoid leaking internal errors?

<details>
<summary>Show answer</summary>

“Creation returns 201 with a Location header; retrieval returns 200; invalid input
returns 400; a missing wallet returns 404. Recognized transient database failures
return 503 with generic Problem Details, and unexpected errors return generic 500.”

The exception handler recognizes wrapped provider failures too. It does not send
connection strings, SQL diagnostics or stack traces to the client. A 503 is not
proof that a write was never committed, so clients must not assume blind POST
retries are safe before idempotency is implemented.

</details>

### 10. Why use PostgreSQL Testcontainers, and what do these tests still not prove?

<details>
<summary>Show answer</summary>

“They run real PostgreSQL in an isolated disposable container. We apply migrations,
send HTTP requests, verify persistence across scopes, check malformed/invalid inputs,
and test database constraints and state rehydration. This catches provider-specific
issues that an EF in-memory provider or mock would miss.”

The tests do not prove financial transfer atomicity, optimistic concurrency under
contention, idempotency, authorization or production performance. Those are separate
requirements for later stages. Docker must be available; failure to start the
container is a real test failure, not silently replaced by an in-memory database.

</details>

## Verification record

Verified locally on October 24, 2026:

- `dotnet build LedgerFlow.slnx`: succeeded, zero warnings and errors.
- `dotnet test LedgerFlow.slnx --no-build`: 54 passed, zero failed or skipped
  (36 domain unit cases and 18 API integration cases).
- The HTTP wallet tests created an isolated PostgreSQL container, applied the actual
  migration and disposed the container afterward.
- `./scripts/Update-Database.ps1`: applied `20261024192346_InitialWallets` to the
  local Compose database. On a fresh database, EF first probes for migration history;
  the initial missing-history diagnostic was followed by successful creation/application.
- The live Swagger document contained POST /wallets and GET /wallets/{id}.
- Postman CLI: 7 requests and 14 assertions passed against the local migrated database.
  One sample wallet was created by this manual collection run.
- The temporary API process was stopped after verification; PostgreSQL remains available.

During setup, an EF relational package version mismatch was corrected. Docker Desktop
also failed because an old analytics socket was inaccessible. Its temporary socket
directory was preserved as `run.stale-stage03` and Docker recreated fresh sockets;
containers and database volumes were preserved. These environment issues were fixed
before the successful test run rather than counted as passing checks.

The implementation completes Stage 03's wallet-creation scope. Financial operations
remain Stage 04, and your review of these decisions is the learning checkpoint.
