using LedgerFlow.SharedKernel;

namespace LedgerFlow.Modules.Ledger.Domain;

public enum LedgerAccountKind { Wallet = 1, Clearing = 2 }

/// <summary>
/// Credit-normal account: credits increase balance, debits decrease it.
/// Application must persist balance changes with their journal entry atomically.
/// </summary>
public sealed class LedgerAccount
{
    public Guid Id { get; }
    public Guid? WalletId { get; }
    public LedgerAccountKind Kind { get; }
    public Money Balance { get; private set; }

    private LedgerAccount(Guid? walletId, LedgerAccountKind kind)
    {
        Id = Guid.NewGuid();
        WalletId = walletId;
        Kind = kind;
        Balance = Money.FromMinorUnits(0);
    }

    public static LedgerAccount ForWallet(Guid walletId)
    {
        if (walletId == Guid.Empty) throw new ArgumentException("Wallet is required.", nameof(walletId));
        return new LedgerAccount(walletId, LedgerAccountKind.Wallet);
    }

    public static LedgerAccount CreateClearing() => new(null, LedgerAccountKind.Clearing);

    public void Apply(Posting posting)
    {
        ArgumentNullException.ThrowIfNull(posting);
        if (posting.AccountId != Id) throw new ArgumentException("Posting belongs to another account.", nameof(posting));
        var next = posting.Side == PostingSide.Credit ? Balance + posting.Amount : Balance - posting.Amount;
        if (Kind == LedgerAccountKind.Wallet && next.MinorUnits < 0)
            throw new InvalidOperationException("Insufficient funds.");
        Balance = next;
    }
}
