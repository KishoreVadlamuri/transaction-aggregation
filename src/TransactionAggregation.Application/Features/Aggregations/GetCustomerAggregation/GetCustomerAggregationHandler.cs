using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Aggregations.GetCustomerAggregation;

public sealed class GetCustomerAggregationHandler(IEnumerable<ITransactionSource> sources,
    ITransactionStore store,
    ITransactionCategorizer categorizer,
    ICacheService cache,
    IOptions<AggregationOptions> options,
    IOptions<CacheOptions> cacheOptions,
    ILogger<GetCustomerAggregationHandler> logger) : IRequestHandler<GetCustomerAggregationQuery, CustomerAggregationResult>
{
    public async Task<CustomerAggregationResult> Handle(
GetCustomerAggregationQuery request,
CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);
        if (request.To < request.From)
        {
            logger.LogWarning(
                "Invalid aggregation range for {CustomerId}: From={From}, To={To}",
                request.CustomerId,
                request.From,
                request.To);
            throw new ArgumentException("'to' must be greater than or equal to 'from'.", nameof(request.To));
        }

        var cacheKey = BuildCacheKey(request.CustomerId, request.From, request.To);

        logger.LogInformation(
            "Aggregation requested for {CustomerId} from {From} to {To} (ForceRefresh={ForceRefresh}, CacheKey={CacheKey})",
            request.CustomerId,
            request.From,
            request.To,
            request.ForceRefresh,
            cacheKey);

        if (!request.ForceRefresh)
        {
            var cached = await cache.GetAsync<CustomerAggregationResult>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                logger.LogInformation(
                    "Serving aggregation for {CustomerId} from cache ({TotalTransactions} transactions)",
                    request.CustomerId,
                    cached.TotalTransactions);
                return CloneWithCacheFlag(cached, servedFromCache: true);
            }

            logger.LogDebug("Cache miss for aggregation key {CacheKey}", cacheKey);
        }
        else
        {
            logger.LogInformation("Force refresh requested; bypassing cache for {CustomerId}", request.CustomerId);
        }

        var stopwatch = Stopwatch.StartNew();

        var storedTransactions = await store.GetByCustomerAsync(
            request.CustomerId,
            request.From,
            request.To,
            cancellationToken);

        var categorizedCount = 0;
        foreach (var tx in storedTransactions)
        {
            if (tx.Category == TransactionCategoryType.Uncategorized)
            {
                tx.Category = categorizer.Categorize(tx);
                categorizedCount++;
            }
        }

        logger.LogDebug(
            "Categorized {CategorizedCount} transactions during aggregation for {CustomerId}",
            categorizedCount,
            request.CustomerId);

        var categories = storedTransactions
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                var total = g.Sum(x => x.TransactionAmount);
                return new CategoryAggregate
                {
                    Category = g.Key,
                    TransactionCount = g.Count(),
                    TotalAmount = decimal.Round(total, 2),
                    AverageAmount = decimal.Round(total / g.Count(), 2)
                };
            })
            .OrderByDescending(c => Math.Abs(c.TotalAmount))
            .ToList();

        var totalSpend = storedTransactions.Where(t => t.TransactionAmount < 0).Sum(t => Math.Abs(t.TransactionAmount));
        var totalIncome = storedTransactions.Where(t => t.TransactionAmount > 0).Sum(t => t.TransactionAmount);
        var currency = storedTransactions.Select(t => t.Currency).FirstOrDefault() ?? options.Value.DefaultCurrency;

        stopwatch.Stop();

        var result = new CustomerAggregationResult
        {
            CustomerId = request.CustomerId,
            From = request.From,
            To = request.To,
            TotalTransactions = storedTransactions.Count,
            TotalSpend = decimal.Round(totalSpend, 2),
            TotalIncome = decimal.Round(totalIncome, 2),
            NetAmount = decimal.Round(totalIncome - totalSpend, 2),
            Currency = currency,
            Categories = categories,
            GeneratedAt = DateTimeOffset.UtcNow,
            ComputationDuration = stopwatch.Elapsed,
            ServedFromCache = false
        };

        await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(options.Value.CacheTtlSeconds), cancellationToken);
        
        logger.LogDebug(
            "Cached aggregation for {CustomerId} with TTL {TtlSeconds}s",
            request.CustomerId,
            options.Value.CacheTtlSeconds);

        logger.LogInformation(
            "Computed aggregation for {CustomerId}: {Count} transactions, Spend={TotalSpend}, Income={TotalIncome}, Net={NetAmount}, Categories={CategoryCount} in {Duration}ms",
            request.CustomerId,
            result.TotalTransactions,
            result.TotalSpend,
            result.TotalIncome,
            result.NetAmount,
            result.Categories.Count,
            stopwatch.ElapsedMilliseconds);

        return result;
    }

    private string BuildCacheKey(string customerId, DateTimeOffset from, DateTimeOffset to) =>
    $"{cacheOptions.Value.KeyPrefix}agg:{customerId}:{from:yyyyMMdd}:{to:yyyyMMdd}";

    private static CustomerAggregationResult CloneWithCacheFlag(
        CustomerAggregationResult source,
        bool servedFromCache) =>
        new()
        {
            CustomerId = source.CustomerId,
            From = source.From,
            To = source.To,
            TotalTransactions = source.TotalTransactions,
            TotalSpend = source.TotalSpend,
            TotalIncome = source.TotalIncome,
            NetAmount = source.NetAmount,
            Currency = source.Currency,
            Categories = source.Categories,
            GeneratedAt = source.GeneratedAt,
            ComputationDuration = source.ComputationDuration,
            ServedFromCache = servedFromCache
        };
}