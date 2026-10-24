# ADR 0003: Immutable USD money in integer minor units

Status: Accepted

## Decision and context

The initial simulated wallet supports USD only, at cent precision. Represent Money
as an immutable reference-type record with a signed Int64 minor-unit amount and
an explicit currency. Private construction and get-only properties prevent invalid
currency values, including through record copy expressions. Null is not a Money value.

Use exact decimal conversion at boundaries; reject fractional cents without rounding.
Use checked arithmetic and checked conversion to reject overflow. Zero and negative
Money values are valid representations. Transfer positivity and account overdraft
restrictions belong to their respective domain operations, not this general value object.
Addition and subtraction require matching currencies; currently all valid instances
are USD, so that guard becomes observable only if currency support expands.

Money is the only type in a dependency-free SharedKernel, because both wallet and
ledger concepts need the same monetary semantics. Do not add generic repositories,
base entities or infrastructure abstractions to this project.

## Alternatives and consequences

Decimal is a valid alternative, especially for interest and fractional calculations,
but would still require precision and rounding policies. Integers fit fixed-cent
transfers and make the supported range explicit. Other currencies may have different
minor-unit scales; supporting them requires a deliberate redesign of conversion.
Double is unsuitable for exact decimal monetary values. This is not event sourcing
or an accounting model. No persistence mapping or API serialization contract is fixed yet.
