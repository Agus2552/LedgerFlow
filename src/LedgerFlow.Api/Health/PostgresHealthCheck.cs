using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace LedgerFlow.Api.Health;

public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("LedgerFlow");
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("Database configuration is missing.");

        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = 2,
                CommandTimeout = 2
            };
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is 1
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database probe failed.");
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or ArgumentException or OperationCanceledException)
        {
            // Do not include credentials or provider diagnostics in the public response.
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}
