using Microsoft.Extensions.Options;
using System.Diagnostics;
using TransactionAggregation.ExternalPublisher.Generation;
using TransactionAggregation.ExternalPublisher.Messaging;
using TransactionAggregation.ExternalPublisher.Options;

namespace TransactionAggregation.ExternalPublisher;

/// <summary>
/// External feed simulator: once running, publishes FinancialTransaction chunks from a JSON
/// data file to Kafka at a fixed interval for the Transaction Aggregation API consumer.
/// </summary>
public sealed class FinancialTransactionChunkPublisherWorker : BackgroundService
{
    private readonly IFinancialTransactionChunkPublisher _publisher;
    private readonly JsonFinancialTransactionChunkSource _chunkSource;
    private readonly PublisherOptions _options;
    private readonly ILogger<FinancialTransactionChunkPublisherWorker> _logger;
    private int _chunkSequence;

    public FinancialTransactionChunkPublisherWorker(
        IFinancialTransactionChunkPublisher publisher,
        JsonFinancialTransactionChunkSource chunkSource,
        IOptions<PublisherOptions> options,
        ILogger<FinancialTransactionChunkPublisherWorker> logger)
    {
        _publisher = publisher;
        _chunkSource = chunkSource;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("External FinancialTransaction chunk publisher is disabled");
            return;
        }

        var intervalSeconds = Math.Max(1, _options.IntervalSeconds);
        var chunkSize = Math.Max(1, _options.ChunkSize);

        _logger.LogInformation(
            "External publisher started. Interval={IntervalSeconds}s, ChunkSize={ChunkSize}, DataRecords={DataRecords}, DataFile={DataFile}",
            intervalSeconds,
            chunkSize,
            _chunkSource.TotalCount,
            _options.DataFilePath);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            await PublishNextChunkAsync(chunkSize, stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishNextChunkAsync(chunkSize, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("External publisher cancellation requested; shutting down");
        }

        _logger.LogInformation(
            "External publisher stopped after {ChunkCount} chunks",
            _chunkSequence);
    }

    private async Task PublishNextChunkAsync(int chunkSize, CancellationToken stoppingToken)
    {
        var chunkSequence = Interlocked.Increment(ref _chunkSequence);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug(
            "Reading FinancialTransaction chunk {ChunkSequence} (size={ChunkSize}) from JSON data",
            chunkSequence,
            chunkSize);

        var chunk = _chunkSource.TakeChunk(chunkSize);
        var customerSummary = string.Join(
            ", ",
            chunk.GroupBy(t => t.CustomerId).Select(g => $"{g.Key}:{g.Count()}"));

        _logger.LogInformation(
            "Prepared chunk {ChunkSequence} with {Count} transactions ({CustomerSummary})",
            chunkSequence,
            chunk.Count,
            customerSummary);

        try
        {
            await _publisher.PublishChunkAsync(chunk, stoppingToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Published chunk {ChunkSequence} successfully in {ElapsedMs}ms",
                chunkSequence,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Publish of chunk {ChunkSequence} cancelled after {ElapsedMs}ms",
                chunkSequence,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Unexpected failure publishing FinancialTransaction chunk {ChunkSequence} after {ElapsedMs}ms",
                chunkSequence,
                stopwatch.ElapsedMilliseconds);
        }
    }
}

