using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Infrastructure.Persistence;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class PostgresTransactionStoreTests
{
    [Fact]
    public async Task UpsertAndGetByCustomer_PersistsAndReadsTransactions()
    {
        await using var db = CreateDbContext();
        var store = new PostgresTransactionStore(db, NullLogger<PostgresTransactionStore>.Instance);

        var tx = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = "cust-pg",
            TransactionAmount = -25.50m,
            Currency = "ZAR",
            MerchantName = "Uber Trip",
            Details = "ride",
            TransactionDate = DateTimeOffset.Parse("2026-08-10T12:00:00Z"),
            Source = TransactionSourceType.PaymentProvider,
            Category = TransactionCategoryType.Transport
        };

        await store.UpsertManyAsync([tx]);

        var loaded = await store.GetByCustomerAsync(
            "cust-pg",
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-31T00:00:00Z"));

        loaded.Should().ContainSingle();
        loaded[0].Id.Should().Be(tx.Id);
        loaded[0].Category.Should().Be(TransactionCategoryType.Transport);
        (await store.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertManyAsync_UpdatesExistingRow()
    {
        await using var db = CreateDbContext();
        var store = new PostgresTransactionStore(db, NullLogger<PostgresTransactionStore>.Instance);
        var id = Guid.NewGuid();

        await store.UpsertManyAsync(
        [
            new FinancialTransaction
            {
                Id = id,
                CustomerId = "cust-pg",
                TransactionAmount = -10m,
                Currency = "ZAR",
                MerchantName = "Netflix",
                Details = "subscription",
                TransactionDate = DateTimeOffset.Parse("2026-08-10T12:00:00Z"),
                Source = TransactionSourceType.CreditCard,
                Category = TransactionCategoryType.Uncategorized
            }
        ]);

        await store.UpsertManyAsync(
        [
            new FinancialTransaction
            {
                Id = id,
                CustomerId = "cust-pg",
                TransactionAmount = -15.99m,
                Currency = "ZAR",
                MerchantName = "Netflix",
                Details = "subscription",
                TransactionDate = DateTimeOffset.Parse("2026-08-10T12:00:00Z"),
                Source = TransactionSourceType.CreditCard,
                Category = TransactionCategoryType.Subscription
            }
        ]);

        var loaded = await store.GetByCustomerAsync(
            "cust-pg",
            DateTimeOffset.MinValue,
            DateTimeOffset.MaxValue);

        loaded.Should().ContainSingle();
        loaded[0].TransactionAmount.Should().Be(-15.99m);
        loaded[0].Category.Should().Be(TransactionCategoryType.Subscription);
        (await store.CountAsync()).Should().Be(1);
    }

    private static TransactionDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TransactionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TransactionDbContext(options);
    }
}
