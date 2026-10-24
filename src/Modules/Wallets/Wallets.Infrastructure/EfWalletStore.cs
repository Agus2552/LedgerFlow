using LedgerFlow.Modules.Wallets.Application;
using LedgerFlow.Modules.Wallets.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerFlow.Modules.Wallets.Infrastructure;

public sealed class EfWalletStore(WalletsDbContext db) : IWalletStore
{
    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Wallet?> FindAsync(Guid id, CancellationToken cancellationToken)
        => db.Wallets.AsNoTracking().SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
}
