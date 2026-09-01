using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Messaging;

public sealed class KafkaTransactionConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaTransactionConsumerHostedService> _logger;

    public KafkaTransactionConsumerHostedService(
        IOptions<KafkaOptions> options,
        IServiceProvider serviceProvider,
        ILogger<KafkaTransactionConsumerHostedService> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
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

        if (string.IsNullOrWhiteSpace(_options.BootstrapServers) || string.IsNullOrWhiteSpace(_options.Topic))
        {
            _logger.LogWarning("Kafka consumer configuration incomplete (BootstrapServers or Topic missing); consumer disabled");
            await IdleUntilCancelled(stoppingToken);
            return;
        }

        // Use a local copy of options so we can generate a default group id if needed
        var opts = _options;

        // Ensure we have a group id; generating a default if not provided prevents librdkafka errors
        if (string.IsNullOrWhiteSpace(opts.ConsumerGroupId))
        {
            opts = new KafkaOptions
            {
                Enabled = opts.Enabled,
                BootstrapServers = opts.BootstrapServers,
                Topic = opts.Topic,
                ClientId = opts.ClientId,
                ConsumerGroupId = string.IsNullOrWhiteSpace(opts.ClientId) ? "transaction-aggregation-consumer" : $"{opts.ClientId}-group"
            };
            _logger.LogInformation("No Kafka consumer group id provided; using generated group id {GroupId}", opts.ConsumerGroupId);
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = opts.BootstrapServers,
            GroupId = opts.ConsumerGroupId,
            ClientId = $"{opts.ClientId}-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            SessionTimeoutMs = 10000,
            SocketTimeoutMs = 5000
        };
        IConsumer<string, string>? consumer = null;

        try
        {
            consumer = new ConsumerBuilder<string, string>(config).Build();
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Unable to create Kafka consumer; will idle and retry when cancelled");
            await IdleUntilCancelled(stoppingToken);
            return;
        }

        try
        {
            consumer.Subscribe(opts.Topic);
            _logger.LogInformation("Kafka consumer subscribed to {Topic}", opts.Topic);
        }
        catch (KafkaException ex)
        {
            _logger.LogWarning(ex, "Unable to subscribe to Kafka topic {Topic}; consumer will idle", opts.Topic);
            consumer.Dispose();
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

                using var scope = _serviceProvider.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<ITransactionStore>();
                await store.UpsertManyAsync(new[] { transaction }, stoppingToken);

                _logger.LogInformation(
                    "Consumed Kafka transaction {TransactionId} for {CustomerId} from {Topic} @ {Offset}",
                    transaction.Id,
                    transaction.CustomerId,
                    result.Topic,
                    result.Offset);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Kafka consume error on topic {Topic}", opts.Topic);
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
            _logger.LogInformation("Kafka consumer closed for topic {Topic}", opts.Topic);
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

