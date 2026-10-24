using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LedgerFlow.Modules.Wallets.Application;
using LedgerFlow.Modules.Wallets.Domain;
using LedgerFlow.Modules.Wallets.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LedgerFlow.IntegrationTests;

public sealed class WalletApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17").Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:LedgerFlow"] = database.GetConnectionString() })));
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WalletsDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await database.DisposeAsync();
    }
}

public sealed class WalletApiTests(WalletApiFixture fixture) : IClassFixture<WalletApiFixture>
{
    [Fact]
    public async Task Creating_wallet_returns_location_and_persists_across_requests()
    {
        using var client = fixture.Factory.CreateClient();
        var owner = Guid.NewGuid();
        using var created = await client.PostAsJsonAsync("/wallets", new { ownerId = owner, currency = "USD" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var wallet = (await created.Content.ReadFromJsonAsync<WalletResponse>())!;
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(owner, wallet.OwnerId);
        Assert.Equal("USD", wallet.Currency);
        Assert.Equal("Active", wallet.Status);
        Assert.Equal($"/wallets/{wallet.Id}", created.Headers.Location!.OriginalString);

        using var secondClient = fixture.Factory.CreateClient();
        var fetched = await secondClient.GetFromJsonAsync<WalletResponse>(created.Headers.Location);
        Assert.Equal(wallet, fetched);
        using var scope = fixture.Factory.Services.CreateScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<WalletsDbContext>()
            .Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id);
        Assert.Equal(owner, persisted.OwnerId);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Invalid_currency_returns_validation_problem_without_inserting(string? currency)
    {
        var owner = Guid.NewGuid();
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/wallets", new { ownerId = owner, currency });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("currency", out _));
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<WalletsDbContext>().Wallets.AnyAsync(w => w.OwnerId == owner));
    }

    [Fact]
    public async Task Empty_owner_and_currency_report_both_errors()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/wallets", new { ownerId = Guid.Empty, currency = "EUR" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("ownerId", out _));
        Assert.True(errors.TryGetProperty("currency", out _));
    }

    [Theory]
    [InlineData("{\"ownerId\":\"not-a-guid\",\"currency\":\"USD\"}")]
    [InlineData("{ invalid json")]
    [InlineData("null")]
    public async Task Malformed_requests_return_400(string json)
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsync("/wallets", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Unknown_wallet_returns_404_problem()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync($"/wallets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Closed_wallet_state_is_preserved_when_rehydrated()
    {
        var wallet = Wallet.Open(Guid.NewGuid());
        wallet.Close();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
            db.Wallets.Add(wallet);
            await db.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateClient();
        var fetched = await client.GetFromJsonAsync<WalletResponse>($"/wallets/{wallet.Id}");
        Assert.Equal("Closed", fetched!.Status);
    }

    [Fact]
    public async Task Database_constraints_reject_invalid_direct_writes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
        var id = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO wallets.wallets (id, owner_id, currency, status) VALUES ({id}, {owner}, 'EUR', 'Active')"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO wallets.wallets (id, owner_id, currency, status) VALUES ({id}, {Guid.Empty}, 'USD', 'Active')"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO wallets.wallets (id, owner_id, currency, status) VALUES ({id}, {owner}, 'USD', 'Unknown')"));
    }

    [Fact]
    public async Task Schema_matches_the_current_model_and_readiness_is_healthy()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
