using Microsoft.Extensions.DependencyInjection;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Services;

namespace TransactionAggregation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddSingleton<ITransactionCategorizer, RuleBasedTransactionCategorizer>();
        return services;
    }
}
