# ADR 0006: Wallet creation vertical slice

Status: Accepted

## Problem and behavior

Expose POST /wallets to create wallet metadata and GET /wallets/{id} to retrieve it.
Successful creation persists one Active USD wallet and returns 201 with a Location
header. Missing/empty owner and unsupported/missing currency return 400 validation
problems. Malformed JSON or GUID fields return 400; unknown IDs return 404.
Transient database failures return a generic 503, other unexpected failures 500.

## Architecture

Wallets.Application owns command/query handlers, the response DTO, and IWalletStore.
It references Domain, not EF Core or ASP.NET. Infrastructure implements the store
using scoped WalletsDbContext. API maps transport requests to commands and results
to HTTP. The composition root registers implementations and handlers as scoped.

Use separate command/query handlers without MediatR. They make mutation versus
retrieval explicit without a mediator dependency or separate databases. Use a
specific store contract rather than a generic repository, IQueryable exposure or
an extra unit-of-work wrapper. AddAsync includes SaveChangesAsync for this single
aggregate slice; this pattern must be revisited before multi-module transactions.
Use EF Core for the simple query too, with AsNoTracking; Dapper has no demonstrated
benefit here. Explicit DTOs prevent exposing the domain as the HTTP contract.

## Persistence

Wallets owns wallets.wallets and its migration history in the wallets schema.
Map private constructor properties explicitly and include status in constructor
binding, so loading a Closed wallet does not reset it to Active. IDs are generated
by Domain, not PostgreSQL. Store status as a constrained string. Database constraints
reject empty IDs, unsupported currency and invalid status. Owner ID has a nonunique
index; multiple wallets per owner/currency are currently allowed. No cross-module
foreign key is introduced for identity before identity ownership is implemented.

Generate and apply explicit EF migrations with a repository-local tool. Do not run
migrations on API startup or use EnsureCreated as a substitute. One SaveChanges
provides the atomic persistence required for this wallet insert. No explicit transfer
transaction or concurrency token is introduced prematurely.

## Alternatives, scope and limitations

Putting EF directly in an endpoint is shorter but couples transport, use case and
storage. MediatR and generic repository layers add machinery without a need here.
In-memory/SQLite tests would not validate PostgreSQL-specific mapping and constraints;
use an isolated PostgreSQL Testcontainer, run migrations, then test through HTTP.

This slice creates metadata only: no ledger account, funds or balance is created.
No close endpoint is exposed. It has no authentication or owner-existence check yet;
ownerId is client-supplied. Run locally; do not expose publicly before the security
stage. POST is not idempotent and retries can create another wallet. A 503 response
does not establish that a disconnected client-side write was never committed.
HTTP constraint violations are not all classified as outages: only recognized
transient provider failures produce 503; unexpected persistence errors remain 500.

Stage 04 must provision linked ledger accounts and redesign shared transaction
coordination before financial operations are enabled. No module may write another
module's tables directly from application code.
