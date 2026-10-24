# LedgerFlow — Stages 01 and 02: Summary and Interview Review

At the end of each stage, review a summary of what we built and why, followed by
10 technical interview questions with answers. Answers are collapsed so you can
think first and then check yourself.

This review covers Stages 01 and 02. Stage 03 comes next: creating a wallet through
an endpoint, application logic, EF Core, and PostgreSQL.

## What we built and why

LedgerFlow is a simulated wallet backend designed as a **modular monolith**: one
deployable application with explicit business boundaries. This gives us local
database transactions and simpler operations while still practicing modular design.

Our foundation includes:

- **ASP.NET Core API**, with Swagger and a Postman collection for manual testing.
- **PostgreSQL in Docker**, for a reproducible database environment.
- **Liveness and readiness endpoints**, separating a responsive API from an available database.
- **Configuration through user secrets**, keeping local database credentials outside committed files.
- **ADRs**, recording decisions, alternatives, and consequences.

Our domain model includes:

| Concept | Responsibility |
| --- | --- |
| `Money` | Immutable USD values, exact cent precision, checked arithmetic |
| `Wallet` | Wallet identity, ownership, currency, and lifecycle |
| `LedgerAccount` | Financial balance and local overdraft protection |
| `Posting` | One positive debit or credit against an account |
| `JournalEntry` | Immutable, balanced collection of postings |
| `Transfer` | Payment intent and its pending, confirmed, or rejected outcome |

The important architectural choice is **separating local invariants from
operation-wide consistency**. Our domain objects enforce their own rules. Later,
application coordination and PostgreSQL transactions will make the entire transfer atomic.

At the last verification, the solution built without warnings and **40 tests
passed**. We have not yet implemented business endpoints, domain persistence,
transaction coordination, optimistic database versions, or idempotency.

## 1. Why did you choose a modular monolith instead of microservices?

<details>
<summary>Show answer</summary>

“We need clear business boundaries, but we don’t currently need independent
deployment or scaling. A modular monolith lets us use local transactions and avoid
network failures and distributed consistency mechanisms.

The trade-off is that modules share a deployment and database infrastructure. We
enforce boundaries through data ownership and explicit contracts rather than
physical separation.”

Microservices would become worth considering if operational requirements justified
their additional complexity.

</details>

## 2. How does your design apply Clean Architecture and dependency inversion?

<details>
<summary>Show answer</summary>

“Our domain projects contain business concepts and rules without dependencies on
HTTP, EF Core, or PostgreSQL. Ledger and Transfers depend only on the small
SharedKernel for Money.

As we introduce application and infrastructure layers, application-owned contracts
will describe required capabilities, and infrastructure will implement them. The
API’s composition root will connect implementations through dependency injection.”

Dependency inversion concerns the direction of source-code dependencies. It doesn’t
mean every class needs an interface.

</details>

## 3. What is the difference between an entity and a value object?

<details>
<summary>Show answer</summary>

“An entity has an identity that persists as its state changes. A Wallet remains
the same Wallet when it becomes closed.

A value object is defined by its values. Two Money instances containing 1,025 USD
cents are equivalent, even if they are separate objects.”

Money is immutable and uses value equality. Wallet has controlled state changes
and a unique ID.

</details>

## 4. Why did you represent money using integer cents? Could decimal work?

<details>
<summary>Show answer</summary>

“Our MVP supports USD at cent precision, so integer minor units make that precision
explicit. We reject fractional cents and use checked arithmetic to detect overflow.

Decimal would also be a valid choice. It can represent decimal monetary values
accurately, but we would still need precision and rounding policies.”

For interest, conversion, or fractional calculations, we would revisit the
representation. We avoid `double` because binary floating-point cannot exactly
represent many decimal amounts.

</details>

## 5. Why does Money allow negative values while Transfer rejects them?

<details>
<summary>Show answer</summary>

“Money represents a monetary value; Transfer represents an operation. Their
invariants differ.

Zero is a valid balance, and signed values can represent accounting positions. A
transfer amount must be strictly positive, because direction is already expressed
by its source and destination.”

The general principle is to enforce a rule at the boundary where it actually
applies. Requesting money would be a separate use case, not a negative transfer.

</details>

## 6. What is an aggregate, and where are your aggregate boundaries?

<details>
<summary>Show answer</summary>

“An aggregate groups domain state whose local invariants are protected through an
aggregate root. In LedgerFlow, Wallet, LedgerAccount, JournalEntry, and Transfer
are separate roots.

JournalEntry owns its postings and ensures they balance. LedgerAccount protects
its own balance. Transfer protects its lifecycle.”

An aggregate isn’t a module, a database table, or necessarily the entire database
transaction. Our transfer use case will coordinate several aggregates within one
local transaction.

</details>

## 7. Why separate Wallet, LedgerAccount, Transfer, and JournalEntry?

<details>
<summary>Show answer</summary>

“They represent different responsibilities:

- Wallet describes the customer’s product and lifecycle.
- LedgerAccount owns financial balance.
- Transfer describes payment intent and outcome.
- JournalEntry records accounting effects.

Keeping them separate avoids making one object responsible for ownership,
lifecycle, accounting, and payment processing.”

A funding operation or correction can generate a journal entry without being an
ordinary wallet-to-wallet transfer.

</details>

## 8. Does double-entry accounting guarantee that a transfer is valid?

<details>
<summary>Show answer</summary>

“No. It guarantees balanced accounting effects, not every business condition.

Debiting Ana $80 and crediting Bruno $80 is balanced, but it is invalid if Ana only
has $70. We must also check funds, wallet availability, authorization, currency
compatibility, and duplicate execution.”

Corrections create new compensating entries rather than modifying history. Our
current immutable model supports this approach; database-level append-only
enforcement is still future work.

</details>

## 9. How do transactions, optimistic concurrency, and idempotency differ?

<details>
<summary>Show answer</summary>

“They solve three different problems:

- A transaction makes related changes commit or roll back together.
- Optimistic concurrency detects updates based on stale state.
- Idempotency prevents a repeated request from executing twice.”

For a transfer, we plan to commit balances, journal entry, transfer result, and
successful idempotency record together. A version conflict will require rollback
and a bounded retry with fresh data and repeated validation.

These mechanisms are designed but not implemented yet.

</details>

## 10. What do your health checks and tests actually prove?

<details>
<summary>Show answer</summary>

“Liveness shows that the API can respond. Readiness executes a minimal PostgreSQL
query, verifying connectivity and query execution. Neither proves that financial
operations work correctly.

Domain tests verify local rules such as monetary precision, balanced entries,
overdraft rejection, and valid state transitions. API integration tests exercise
the HTTP pipeline.”

We also manually verified database-outage behavior: readiness returned 503 while
liveness remained 200. Database concurrency, transaction rollback across financial
writes, and idempotency still require dedicated integration tests when implemented.

</details>
