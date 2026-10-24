# ADR 0005: Ledger and transfer aggregate boundaries

Status: Accepted for Stage 02 domain modeling

## Context and decision

Keep LedgerAccount, JournalEntry and Transfer as separate aggregate roots. Ledger
owns accounts and entries. Transfers owns transfer intent and terminal outcome, and
references the journal by ID. Neither domain project references the other's internals.

LedgerAccount owns a materialized balance, initially zero. Wallet accounts cannot
be overdrawn. A controlled simulated clearing account can carry a negative balance
to offset simulated funding. All accounts currently use a credit-normal convention:
credits increase balances, debits decrease them. This is a simplified wallet liability
subledger, not a general chart of accounts or a real bank asset ledger. Debit does
not universally mean decrease; the chosen account convention makes it so here.

JournalEntry owns an immutable copied collection of positive debit/credit Postings.
At least two distinct accounts are required; the currency is uniform and total
debits equal total credits. Accumulate Int64 cents in decimal to avoid intermediate
Int64 overflow. Posting is an immutable value object scoped to its journal entry.
Corrections create new opposite postings referencing the original entry.

Transfer requires a requester, distinct source/destination wallets and positive
Money. It starts Pending and can become Confirmed with a journal reference or
Rejected with an explicit reason. Both outcomes are terminal. Concurrency conflicts
are technical retry conditions, not insufficient-funds business rejections.
Use standard argument exceptions for invalid construction, InvalidOperationException
for invalid state or insufficient funds, and OverflowException for numeric bounds.
Do not expose raw exceptions as HTTP contracts; translate errors in the future slice.

## Consistency boundary and limitations

Local aggregate validation is not transaction orchestration. LedgerAccount.Apply
does not know whether the posting is persisted, balanced with other postings or
already applied. JournalEntry creation does not prove referenced accounts exist.
Transfer.Confirm does not load or validate its journal reference. The application
coordinator must establish those relationships and commit affected account balances,
one journal entry, transfer outcome and successful idempotency record atomically.
On failure, roll back and discard mutated in-memory aggregates before retrying.
Optimistic versions and unique constraints must prevent stale writes, duplicate
operations, duplicate reversals and multiple ledger accounts for the same wallet.
Pending has no public asynchronous workflow at this stage.

Wallet availability and source ownership require application contracts. Closing
requires a zero balance and no pending financial activity, coordinated against transfers.
Reversing an entry can fail if it would overdraw a recipient; reversal creation alone
does not guarantee it can be applied. Domain immutability alone does not enforce
append-only database permissions, audit retention or exactly-once execution.

## Alternatives and consequences

One aggregate spanning all accounts and transfers would create a growing consistency
and loading boundary. Independent transactions would require compensation and eventual
consistency. Instead keep small aggregates with one explicit local database transaction
for transfer execution. Do not introduce events, event sourcing, repositories or EF
mapping before the corresponding application/persistence slice.

## Next slice

Stage 03 creates wallet metadata only, which is not yet financially usable. Stage 04
will provision the linked ledger account before funding/transfers are available,
including a policy for preexisting wallet metadata. This keeps the first slice small
without claiming wallet metadata alone is a ready financial account.
