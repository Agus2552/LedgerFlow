using LedgerFlow.SharedKernel;

namespace LedgerFlow.Modules.Ledger.Domain;

public enum PostingSide { Debit = 1, Credit = 2 }

public sealed record Posting
{
    public Guid AccountId { get; }
    public PostingSide Side { get; }
    public Money Amount { get; }

    public Posting(Guid accountId, PostingSide side, Money amount)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Account is required.", nameof(accountId));
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.MinorUnits <= 0) throw new ArgumentException("Posting amount must be positive.", nameof(amount));
        AccountId = accountId;
        Side = side;
        Amount = amount;
    }
}
