using LedgerFlow.Modules.Wallets.Domain;

namespace LedgerFlow.Modules.Wallets.Application;

public sealed record CreateWalletCommand(Guid OwnerId, string? Currency);
public sealed record CreateWalletResult(WalletResponse? Wallet, Dictionary<string, string[]> Errors);

public sealed class CreateWalletHandler(IWalletStore store)
{
    public async Task<CreateWalletResult> HandleAsync(CreateWalletCommand command, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (command.OwnerId == Guid.Empty) errors["ownerId"] = ["A non-empty owner ID is required."];
        if (command.Currency != "USD") errors["currency"] = ["Only USD is supported."];
        if (errors.Count > 0) return new(null, errors);

        var wallet = Wallet.Open(command.OwnerId, command.Currency!);
        await store.AddAsync(wallet, cancellationToken);
        return new(WalletResponse.From(wallet), errors);
    }
}
