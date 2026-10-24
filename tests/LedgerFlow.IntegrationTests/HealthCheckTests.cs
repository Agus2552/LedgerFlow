using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace LedgerFlow.IntegrationTests;

public class HealthCheckTests
{
    [Fact]
    public async Task Wallet_database_failure_returns_generic_503_without_provider_details()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development")
                .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:LedgerFlow"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=private-test-value;Timeout=1;Pooling=false"
                    })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/wallets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-test-value", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("stackTrace", body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=private-test-value;Pooling=false")]
    public async Task Database_failure_makes_readiness_unhealthy_but_keeps_liveness_healthy(string connectionString)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LedgerFlow"] = connectionString
                })));
        using var client = factory.CreateClient();

        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());

        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_in_development_serves_ui_and_documents_liveness()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Contains("Swagger UI", await uiResponse.Content.ReadAsStringAsync());

        using var document = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var operation = document.RootElement.GetProperty("paths")
            .GetProperty("/health/live").GetProperty("get");
        Assert.True(operation.GetProperty("responses").TryGetProperty("200", out _));
        var readiness = document.RootElement.GetProperty("paths")
            .GetProperty("/health/ready").GetProperty("get");
        Assert.True(readiness.GetProperty("responses").TryGetProperty("503", out _));
    }

    [Fact]
    public async Task Liveness_returns_healthy()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
