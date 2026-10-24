# ADR 0004: Wallet identity and lifecycle

Status: Accepted for the initial domain model

## Decision

Wallet is an entity and aggregate root for wallet identity, ownership, immutable
currency and lifecycle. Open assigns a new non-empty identity, requires an owner,
accepts USD only and produces Active status. Close transitions to Closed permanently;
repeating Close is a no-op. No reopening or ownership transfer is supported.

Wallet contains no balance and does not depend on Ledger implementation or persistence.
OwnerId is an opaque identity reference; this aggregate cannot establish that the
owner exists or that a caller is authorized. Application/security must enforce those rules.
Duplicate owner/currency policy remains open; local construction alone cannot enforce
uniqueness across stored wallets.

Close currently expresses a local state transition, not a complete executable close
use case. The future coordinator must atomically verify zero balance and absence of
pending financial activity in Ledger, and serialize closure against transfers. No close
endpoint may be published until these cross-module conditions are implemented and tested.

## Alternatives and consequences

A mutable DTO would let callers bypass lifecycle rules. A record would communicate
value equality where identity matters. A shared Wallet/Ledger aggregate would couple
wallet metadata to accounting. Keeping ownership separate requires explicit orchestration
for opening the corresponding ledger account and closing safely.
Frozen/suspended states can be added when a concrete use case requires them; a state
machine library and generic base entity are unnecessary for this two-state lifecycle.

The first persistence slice must decide whether wallet opening includes ledger-account
creation. Until it does, opening wallet metadata alone must not imply it is financially
usable. ADR 0005 defines the initial Ledger/Transfers model and the first slice policy.
