using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.ExternalPublisher.Options;

namespace TransactionAggregation.ExternalPublisher.Generation;

/// <summary>
/// Loads FinancialTransaction records from a JSON data file and serves them in sequential chunks.
/// </summary>
public sealed class JsonFinancialTransactionChunkSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IReadOnlyList<FinancialTransaction> _transactions;
    private readonly ILogger<JsonFinancialTransactionChunkSource> _logger;
    private int _offset;

    public JsonFinancialTransactionChunkSource(
        IOptions<PublisherOptions> options,
        IHostEnvironment environment,
        ILogger<JsonFinancialTransactionChunkSource> logger)
        : this(ResolveAndLoad(options.Value.DataFilePath, environment.ContentRootPath), logger)
    {
    }

    public JsonFinancialTransactionChunkSource(
        IReadOnlyList<FinancialTransaction> transactions,
        ILogger<JsonFinancialTransactionChunkSource>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (transactions.Count == 0)
        {
            throw new InvalidOperationException("Financial transaction data file must contain at least one record.");
        }

        _transactions = transactions;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonFinancialTransactionChunkSource>.Instance;
        _logger.LogInformation(
            "Loaded {Count} FinancialTransaction records for chunk publishing",
            _transactions.Count);
    }

    public int TotalCount => _transactions.Count;

    public IReadOnlyList<FinancialTransaction> TakeChunk(int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        var records = new List<FinancialTransaction>(chunkSize);
        for (var i = 0; i < chunkSize; i++)
        {
            var index = (_offset + i) % _transactions.Count;
            records.Add(_transactions[index]);
        }

        _offset = (_offset + chunkSize) % _transactions.Count;

        _logger.LogDebug(
            "Served chunk of {Count} transactions from JSON data (next offset={Offset})",
            records.Count,
            _offset);

        return records;
    }

    public static IReadOnlyList<FinancialTransaction> LoadFromFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Financial transaction data file was not found: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        var transactions = JsonSerializer.Deserialize<List<FinancialTransaction>>(stream, JsonOptions);
        if (transactions is null || transactions.Count == 0)
        {
            throw new InvalidOperationException($"Financial transaction data file is empty or invalid: {filePath}");
        }

        return transactions;
    }

    private static IReadOnlyList<FinancialTransaction> ResolveAndLoad(string configuredPath, string contentRootPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(contentRootPath, "Data", "financial-transactions.json")
            : configuredPath;

        if (!Path.IsPathRooted(path))
        {
            path = Path.GetFullPath(Path.Combine(contentRootPath, path));
        }

        return LoadFromFile(path);
    }
}
