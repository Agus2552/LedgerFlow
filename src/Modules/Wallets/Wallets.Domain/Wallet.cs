namespace LedgerFlow.Modules.Wallets.Domain;

/// <summary>
/// Owns wallet identity and lifecycle. Financial balances belong to Ledger.
/// </summary>
public sealed class Wallet
{
    public Guid Id { get; }
    public Guid OwnerId { get; }
    public string Currency { get; }
    public WalletStatus Status { get; private set; }

    private Wallet(Guid id, Guid ownerId, string currency, WalletStatus status)
    {
        Id = id;
        OwnerId = ownerId;
        Currency = currency;
        Status = status;
    }

    public static Wallet Open(Guid ownerId, string currency = "USD")
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("A wallet must have an owner.", nameof(ownerId));
        if (currency != "USD")
            throw new ArgumentException("Only USD is supported.", nameof(currency));

        return new Wallet(Guid.NewGuid(), ownerId, currency, WalletStatus.Active);
    }

    /// <summary>
    /// Applies the lifecycle transition only. The coordinating use case must first
    /// establish Ledger's financial preconditions in the same consistency boundary.
    /// </summary>
    public void Close() => Status = WalletStatus.Closed;
}
