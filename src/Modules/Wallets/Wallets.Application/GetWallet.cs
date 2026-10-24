namespace LedgerFlow.Modules.Wallets.Application;

public sealed record GetWalletQuery(Guid Id);

public sealed class GetWalletHandler(IWalletStore store)
{
    public async Task<WalletResponse?> HandleAsync(GetWalletQuery query, CancellationToken cancellationToken)
    {
        var wallet = await store.FindAsync(query.Id, cancellationToken);
        return wallet is null ? null : WalletResponse.From(wallet);
    }
}
