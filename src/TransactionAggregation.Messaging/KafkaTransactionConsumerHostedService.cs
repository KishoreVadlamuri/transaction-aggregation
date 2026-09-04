using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Messaging;

public sealed class KafkaTransactionConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITransactionCategorizer _categorizer;
    private readonly ILogger<KafkaTransactionConsumerHostedService> _logger;

    public KafkaTransactionConsumerHostedService(
        IOptions<KafkaOptions> options,
        IServiceProvider serviceProvider,
        ITransactionCategorizer categorizer,
        ILogger<KafkaTransactionConsumerHostedService> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _categorizer = categorizer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Kafka consumer disabled");
            return;
        }

        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            ClientId = $"{_options.ClientId}-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            SessionTimeoutMs = 10000,
            SocketTimeoutMs = 5000
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        try
        {
            consumer.Subscribe(_options.Topic);
            _logger.LogInformation("Kafka consumer subscribed to {Topic}", _options.Topic);
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Unable to subscribe to Kafka topic {Topic}; consumer will idle", _options.Topic);
            await IdleUntilCancelled(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null)
                {
                    continue;
                }

                var transaction = JsonSerializer.Deserialize<FinancialTransaction>(result.Message.Value, JsonOptions);
                if (transaction is null)
                {
                    _logger.LogWarning(
                        "Skipped Kafka message at {TopicPartitionOffset}; payload could not be deserialized",
                        result.TopicPartitionOffset);
                    continue;
                }

                var previousCategory = transaction.Category;
                KafkaConsumedTransactionPreparer.EnsureCategorized(transaction, _categorizer);
                if (previousCategory == TransactionCategoryType.Uncategorized)
                {
                    _logger.LogDebug(
                        "Categorized Kafka transaction {TransactionId} as {Category}",
                        transaction.Id,
                        transaction.Category);
                }

                using var scope = _serviceProvider.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ITransactionStore>();
                await store.UpsertManyAsync([transaction], stoppingToken);

                _logger.LogInformation(
                    "Consumed Kafka transaction {TransactionId} for {CustomerId} as {Category} from {Topic} @ {Offset}",
                    transaction.Id,
                    transaction.CustomerId,
                    transaction.Category,
                    result.Topic,
                    result.Offset);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Kafka consume error on topic {Topic}", _options.Topic);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (KafkaException ex)
            {
                _logger.LogWarning(ex, "Kafka broker unavailable; backing off");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Kafka consumer stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected Kafka consumer failure");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        try
        {
            consumer.Close();
            _logger.LogInformation("Kafka consumer closed for topic {Topic}", _options.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while closing Kafka consumer");
        }
    }

    private static async Task IdleUntilCancelled(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }
}

