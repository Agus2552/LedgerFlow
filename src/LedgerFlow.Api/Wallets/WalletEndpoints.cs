using LedgerFlow.Modules.Wallets.Application;

namespace LedgerFlow.Api.Wallets;

public sealed record CreateWalletRequest(Guid OwnerId, string? Currency);

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/wallets").WithTags("Wallets");

        group.MapPost("", async (CreateWalletRequest request, CreateWalletHandler handler, CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new(request.OwnerId, request.Currency), cancellationToken);
            return result.Wallet is null
                ? Results.ValidationProblem(result.Errors)
                : Results.Created($"/wallets/{result.Wallet.Id}", result.Wallet);
        })
        .WithName("CreateWallet")
        .WithSummary("Create USD wallet metadata")
        .WithDescription("Creates metadata only; no ledger account or balance is provisioned. Owner existence and authorization are deferred to the security stage.")
        .Produces<WalletResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:guid}", async (Guid id, GetWalletHandler handler, CancellationToken cancellationToken) =>
        {
            var wallet = await handler.HandleAsync(new(id), cancellationToken);
            return wallet is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Wallet not found.")
                : Results.Ok(wallet);
        })
        .WithName("GetWallet")
        .WithSummary("Get wallet metadata by ID")
        .Produces<WalletResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
