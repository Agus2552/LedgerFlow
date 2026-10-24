using LedgerFlow.Modules.Wallets.Domain;

namespace LedgerFlow.Modules.Wallets.Application;

public interface IWalletStore
{
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken);
    Task<Wallet?> FindAsync(Guid id, CancellationToken cancellationToken);
}
