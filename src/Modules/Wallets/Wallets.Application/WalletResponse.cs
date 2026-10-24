using LedgerFlow.Modules.Wallets.Domain;

namespace LedgerFlow.Modules.Wallets.Application;

public sealed record WalletResponse(Guid Id, Guid OwnerId, string Currency, string Status)
{
    public static WalletResponse From(Wallet wallet)
        => new(wallet.Id, wallet.OwnerId, wallet.Currency, wallet.Status.ToString());
}
