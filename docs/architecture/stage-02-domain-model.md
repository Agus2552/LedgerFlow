# Stage 02 domain model

Implementation and automated checks are complete. The developer has authorized
Stage 03 while continuing self-study using the Stage 01–02 interview review file.

| Type | Kind | Owns |
| --- | --- | --- |
| Money | Immutable value object | Signed USD cents and exact arithmetic |
| Wallet | Entity / aggregate root | Owner, currency and lifecycle |
| LedgerAccount | Entity / aggregate root | Account identity and materialized balance |
| Posting | Immutable value object within an entry | Account reference, positive amount, debit/credit side |
| JournalEntry | Immutable entity / aggregate root | Balanced postings and reversal reference |
| Transfer | Entity / aggregate root | Payment intent, pending/confirmed/rejected state |

Dependencies: Wallets.Domain is standalone; Ledger.Domain and Transfers.Domain each
reference SharedKernel. SharedKernel has no external dependencies. Domain modules do
not reference each other, ASP.NET, EF Core or Npgsql. API has no new business endpoints.

## Example: simulated funding and transfer

Amounts below use major USD units for readability; the implementation stores cents.

1. Funding: debit clearing 100 and credit Ana 100. Balances: clearing -100, Ana 100.
2. Transfer: debit Ana 30 and credit Bruno 30. Balances: clearing -100, Ana 70, Bruno 30.
3. Total balance across the subledger remains zero; each journal balances independently.
4. A later attempted debit of 80 from Ana fails because only 70 is available.

Creating valid objects does not commit those operations. Application transaction
orchestration is future work. See ADR 0005 for the complete consistency boundary.

## Verification

`dotnet build LedgerFlow.slnx`: zero warnings and errors.
`dotnet test LedgerFlow.slnx --no-build`: 36 domain unit cases and 4 API integration
cases passed. Coverage includes Money precision/overflow, Wallet construction/lifecycle,
posting validation, journal balance, snapshots/reversals, simulated conservation of value,
account overdraft protection, transfer input rules and terminal state transitions.
These tests do not prove PostgreSQL concurrency, transaction rollback or idempotency.

## Review prompts with model answers

- Why is Wallet an entity but Money a value object? A wallet has continuing identity;
  two equal monetary values are interchangeable.
- Why is a transfer not the same as a journal entry? A transfer captures business
  intent/outcome; a journal entry captures its accounting effects. Funding and corrections
  also generate entries without being ordinary wallet-to-wallet transfers.
- Does a balanced entry prevent overdrafts? No. Balance conservation and sufficient
  funds are distinct invariants.
- Do separate aggregates imply separate database transactions? No. This monolith
  explicitly coordinates them in one local transaction for a transfer.
- Can an in-memory validation prevent concurrent overspending? No. Database versions,
  isolation and atomic persistence are still required.
