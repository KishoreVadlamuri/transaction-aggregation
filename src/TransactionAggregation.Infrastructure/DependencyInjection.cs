using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Infrastructure.Caching;
using TransactionAggregation.Infrastructure.DataSources;
using TransactionAggregation.Infrastructure.Persistence;

namespace TransactionAggregation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AggregationOptions>(configuration.GetSection(AggregationOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<ServiceAccountOptions>(configuration.GetSection(ServiceAccountOptions.SectionName));
        services.AddSingleton<IPasswordHasher<ServiceAccountIdentity>, PasswordHasher<ServiceAccountIdentity>>();
        services.AddSingleton<IAuthUserStore, ServiceAccountAuthStore>();

        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        services.AddDbContext<TransactionDbContext>(options =>
                options.UseNpgsql(storageOptions.PostgresConnectionString));
        services.AddScoped<ITransactionStore, PostgresTransactionStore>();
        services.AddHostedService<PostgresSchemaInitializer>();

        services.AddSingleton<ITransactionSource, BankTransactionSource>();
        services.AddSingleton<ITransactionSource, CreditCardTransactionSource>();
        services.AddSingleton<ITransactionSource, PaymentProviderTransactionSource>();

        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        // Valkey speaks the Redis protocol; StackExchange.Redis is the supported .NET client.
        if (!string.IsNullOrWhiteSpace(cacheOptions.ValkeyConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheOptions.ValkeyConnectionString;
                options.InstanceName = cacheOptions.KeyPrefix;
            });
        }
        else
        {
            // When Valkey is not configured.
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();

        return services;
    }
}
