using Microsoft.Extensions.Diagnostics.HealthChecks;
using LedgerFlow.Api.Health;
using LedgerFlow.Api;
using LedgerFlow.Api.Wallets;
using LedgerFlow.Modules.Wallets.Application;
using LedgerFlow.Modules.Wallets.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddDbContext<WalletsDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("LedgerFlow");
    options.UseNpgsql(connectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "wallets"));
});
builder.Services.AddScoped<IWalletStore, EfWalletStore>();
builder.Services.AddScoped<CreateWalletHandler>();
builder.Services.AddScoped<GetWalletHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapWalletEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
{
    var report = await healthChecks.CheckHealthAsync(_ => false, cancellationToken);
    return Results.Text(report.Status.ToString(), "text/plain",
        statusCode: report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK);
})
.WithName("GetLiveness")
.WithTags("Health")
.WithSummary("Check API liveness")
.WithDescription("Checks API liveness only. Does not check PostgreSQL connectivity.")
.Produces<string>(StatusCodes.Status200OK, "text/plain")
.Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

app.MapGet("/health/ready", async (HealthCheckService healthChecks, CancellationToken cancellationToken) =>
{
    var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);
    return Results.Text(report.Status.ToString(), "text/plain",
        statusCode: report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK);
})
.WithName("GetReadiness")
.WithTags("Health")
.WithSummary("Check database readiness")
.WithDescription("Executes SELECT 1 against PostgreSQL. Returns 503 when configuration is missing or the database is unavailable.")
.Produces<string>(StatusCodes.Status200OK, "text/plain")
.Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");

app.Run();

public partial class Program;
