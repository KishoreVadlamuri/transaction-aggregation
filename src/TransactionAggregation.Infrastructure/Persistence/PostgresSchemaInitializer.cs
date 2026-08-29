using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TransactionAggregation.Infrastructure.Persistence;

public sealed class PostgresSchemaInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PostgresSchemaInitializer> _logger;

    public PostgresSchemaInitializer(
        IServiceProvider serviceProvider,
        ILogger<PostgresSchemaInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();

        _logger.LogInformation("Applying pending EF Core migrations");
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("PostgreSQL schema is up to date (auth credentials are not stored in the database)");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PostgreSQL schema initializer stopped");
        return Task.CompletedTask;
    }
}