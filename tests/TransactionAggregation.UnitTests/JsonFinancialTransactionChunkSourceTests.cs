using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.ExternalPublisher.Generation;
using Xunit;

namespace TransactionAggregation.UnitTests;

public sealed class JsonFinancialTransactionChunkSourceTests
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    [Fact]
    public void TakeChunk_ReturnsSequentialSlicesFromLoadedData()
    {
        var source = new JsonFinancialTransactionChunkSource(CreateSampleTransactions(7));

        var first = source.TakeChunk(3);
        var second = source.TakeChunk(3);

        Assert.Equal(7, source.TotalCount);
        Assert.Equal(3, first.Count);
        Assert.Equal(3, second.Count);
        Assert.Equal("EXT-000000", first[0].ExternalReference);
        Assert.Equal("EXT-000003", second[0].ExternalReference);
    }

    [Fact]
    public void TakeChunk_WrapsAroundWhenDataIsExhausted()
    {
        var source = new JsonFinancialTransactionChunkSource(CreateSampleTransactions(4));

        var chunk = source.TakeChunk(6);

        Assert.Equal(6, chunk.Count);
        Assert.Equal("EXT-000000", chunk[0].ExternalReference);
        Assert.Equal("EXT-000003", chunk[3].ExternalReference);
        Assert.Equal("EXT-000000", chunk[4].ExternalReference);
        Assert.Equal("EXT-000001", chunk[5].ExternalReference);
    }

    [Fact]
    public void LoadFromFile_DeserializesFinancialTransactions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"financial-transactions-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(CreateSampleTransactions(3), WriteOptions));

            var transactions = JsonFinancialTransactionChunkSource.LoadFromFile(path);

            Assert.Equal(3, transactions.Count);
            Assert.All(transactions, tx =>
            {
                Assert.NotEqual(Guid.Empty, tx.Id);
                Assert.False(string.IsNullOrWhiteSpace(tx.CustomerId));
                Assert.False(string.IsNullOrWhiteSpace(tx.MerchantName));
                Assert.Equal("ZAR", tx.Currency);
                Assert.True(Enum.IsDefined(tx.Source));
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadFromFile_ReadsBundledFinancialDataFile()
    {
        var path = FindBundledDataFile();
        Assert.True(File.Exists(path), $"Expected data file at {path}");

        var transactions = JsonFinancialTransactionChunkSource.LoadFromFile(path);
        var source = new JsonFinancialTransactionChunkSource(transactions);

        Assert.True(transactions.Count >= 1000);
        var chunk = source.TakeChunk(5);
        Assert.Equal(5, chunk.Count);
        Assert.All(chunk, tx => Assert.False(string.IsNullOrWhiteSpace(tx.MerchantName)));
    }

    [Fact]
    public void Constructor_RequiresAtLeastOneTransaction()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new JsonFinancialTransactionChunkSource(Array.Empty<FinancialTransaction>()));
    }

    private static string FindBundledDataFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "TransactionAggregation.ExternalPublisher",
                "Data",
                "financial-transactions.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate financial-transactions.json from the test output directory.");
    }

    private static IReadOnlyList<FinancialTransaction> CreateSampleTransactions(int count)
    {
        var list = new List<FinancialTransaction>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new FinancialTransaction
            {
                Id = Guid.NewGuid(),
                CustomerId = $"cust-{1001 + (i % 3)}",
                TransactionAmount = -(10m + i),
                Currency = "ZAR",
                MerchantName = "Sample Merchant",
                Details = "Sample description",
                TransactionDate = DateTimeOffset.Parse("2026-08-01T12:00:00Z").AddMinutes(i),
                Source = TransactionSourceType.Bank,
                Category = TransactionCategoryType.Uncategorized,
                ExternalReference = $"EXT-{i:D6}"
            });
        }

        return list;
    }
}

