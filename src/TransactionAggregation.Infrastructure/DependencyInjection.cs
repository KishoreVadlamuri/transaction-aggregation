using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure.DataSources;
using TransactionAggregation.Infrastructure.Persistence;

namespace TransactionAggregation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        services.AddDbContext<TransactionDbContext>(options =>
                options.UseNpgsql(storageOptions.PostgresConnectionString));
        services.AddScoped<ITransactionStore, PostgresTransactionStore>();
        services.AddHostedService<PostgresSchemaInitializer>();

        services.AddSingleton<ITransactionSource, BankTransactionSource>();
        services.AddSingleton<ITransactionSource, CreditCardTransactionSource>();
        services.AddSingleton<ITransactionSource, PaymentProviderTransactionSource>();

        return services;
    }
}
