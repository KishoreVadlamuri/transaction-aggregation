using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionAggregation.Application.Interfaces;

namespace TransactionAggregation.Infrastructure.Caching;

/// <summary>
/// Cache backed solely by <see cref="IDistributedCache"/> (Valkey via StackExchange.Redis,
/// or in-process distributed memory when Valkey is not configured).
/// </summary>
public class DistributedCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(
        IDistributedCache distributedCache,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
        _logger.LogInformation("Distributed cache service initialized");
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var payload = await _distributedCache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogDebug("Cache miss for key {Key}", key);
            return default;
        }

        _logger.LogDebug("Cache hit for key {Key}", key);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _distributedCache.SetStringAsync(
                key,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
            _logger.LogDebug("Set cache key {Key} with TTL {Ttl}", key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _distributedCache.RemoveAsync(key, cancellationToken);
        _logger.LogDebug("Removed cache key {Key}", key);
    }
}
