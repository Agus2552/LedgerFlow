using LedgerFlow.Modules.Wallets.Domain;

namespace LedgerFlow.UnitTests;

public class WalletTests
{
    [Fact]
    public void Opening_creates_an_active_USD_wallet_for_the_owner()
    {
        var owner = Guid.NewGuid();
        var wallet = Wallet.Open(owner);

        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(owner, wallet.OwnerId);
        Assert.Equal("USD", wallet.Currency);
        Assert.Equal(WalletStatus.Active, wallet.Status);
    }

    [Fact]
    public void Wallets_for_the_same_owner_are_distinct_entities()
    {
        var owner = Guid.NewGuid();
        var first = Wallet.Open(owner);
        var second = Wallet.Open(owner);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Opening_without_an_owner_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Wallet.Open(Guid.Empty));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsupported_currency_is_rejected(string? currency)
    {
        Assert.Throws<ArgumentException>(() => Wallet.Open(Guid.NewGuid(), currency!));
    }

    [Fact]
    public void Closing_preserves_identity_ownership_and_currency()
    {
        var wallet = Wallet.Open(Guid.NewGuid());
        var id = wallet.Id;
        var owner = wallet.OwnerId;

        wallet.Close();

        Assert.Equal(WalletStatus.Closed, wallet.Status);
        Assert.Equal(id, wallet.Id);
        Assert.Equal(owner, wallet.OwnerId);
        Assert.Equal("USD", wallet.Currency);
    }

    [Fact]
    public void Closing_an_already_closed_wallet_is_a_no_op()
    {
        var wallet = Wallet.Open(Guid.NewGuid());
        wallet.Close();
        wallet.Close();

        Assert.Equal(WalletStatus.Closed, wallet.Status);
    }
}
