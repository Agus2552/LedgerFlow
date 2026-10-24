# ADR 0001: Modular monolith foundation

Status: Accepted for the initial implementation; module contracts remain provisional.

## Context

LedgerFlow models simulated money. Internal transfers must commit their financial
effects and transfer result atomically. The learning scope does not require independent deployments.

## Decision

Use a modular monolith with one PostgreSQL database and explicit data ownership.
Identity owns identity; Wallets owns wallet ownership, currency and lifecycle;
Transfers owns transfer lifecycle and idempotency; Ledger owns accounts, journal
entries and financial balances. Modules access each other through explicit contracts.
No module writes another module's tables directly.

The future transfer use case will coordinate one local database transaction through
application contracts and infrastructure adapters. Separate DbContexts must explicitly
enlist in the same connection and transaction. Domain code remains persistence independent.
Balance versions will support optimistic concurrency. Conflicts roll back the whole
transaction; bounded retries re-read and revalidate funds. Idempotency is a separate,
database-enforced mechanism. Optimistic concurrency does not eliminate deadlocks.
Wallet lifecycle changes must also be coordinated with transfer validation; the exact
mechanism remains open until the transfer slice.

Use EF Core for writes when persistence is introduced. Add Dapper only for a concrete
query that benefits from explicit SQL. PostgreSQL provides transactional persistence;
schemas will express module ownership. The foundation contains no empty business modules.

## Alternatives and consequences

Microservices would add network failures and distributed consistency before there is
a deployment need. Independent module transactions would require eventual consistency
and compensation. A local transaction instead couples participants to one database.
Pessimistic balance locking remains an alternative if measured contention makes
optimistic retries expensive. Frequent conflicts require revisiting this decision.
EF Core alone may suffice for reads; adopting Dapper adds a second mapping/query path.
Separate databases improve isolation but prevent the chosen local atomic transaction.

## Open decisions

Currency and money representation are resolved by ADR 0003. Exact concurrency token, idempotency scope and retention,
and customer profile ownership will be decided before the relevant business slice.
