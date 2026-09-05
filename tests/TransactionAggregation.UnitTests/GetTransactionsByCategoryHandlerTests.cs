using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Features.Aggregations.GetTransactionsByCategory;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class GetTransactionsByCategoryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyMatchingCategoryTransactions()
    {
        var store = new FakeTransactionStore();
        await store.UpsertManyAsync(
        [
            Tx("cust-cat", -20m, "Whole Foods Market", "grocery", TransactionCategoryType.Groceries),
            Tx("cust-cat", -15.99m, "Netflix", "subscription", TransactionCategoryType.Subscription),
            Tx("cust-cat", -12m, "Trader Joe", "grocery run", TransactionCategoryType.Groceries),
            Tx("cust-other", -8m, "Whole Foods Market", "grocery", TransactionCategoryType.Groceries)
        ]);

        var sut = new GetTransactionsByCategoryHandler(
            store,
            new RuleBasedTransactionCategorizer(),
            NullLogger<GetTransactionsByCategoryHandler>.Instance);

        var from = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-08-31T00:00:00Z");

        var result = await sut.Handle(
            new GetTransactionsByCategoryQuery("cust-cat", TransactionCategoryType.Groceries, from, to),
            CancellationToken.None);

        result.CustomerId.Should().Be("cust-cat");
        result.Category.Should().Be(TransactionCategoryType.Groceries);
        result.TransactionCount.Should().Be(2);
        result.TotalAmount.Should().Be(-32m);
        result.Transactions.Should().OnlyContain(t => t.Category == TransactionCategoryType.Groceries);
        result.Transactions.Select(t => t.MerchantName).Should().BeEquivalentTo("Whole Foods Market", "Trader Joe");
    }

    [Fact]
    public async Task Handle_RecategorizesUncategorizedBeforeFiltering()
    {
        var store = new FakeTransactionStore();
        await store.UpsertManyAsync(
        [
            Tx("cust-cat", -15.99m, "Netflix", "Monthly subscription", TransactionCategoryType.Uncategorized)
        ]);

        var sut = new GetTransactionsByCategoryHandler(
            store,
            new RuleBasedTransactionCategorizer(),
            NullLogger<GetTransactionsByCategoryHandler>.Instance);

        var result = await sut.Handle(
            new GetTransactionsByCategoryQuery(
                "cust-cat",
                TransactionCategoryType.Subscription,
                DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-31T00:00:00Z")),
            CancellationToken.None);

        result.TransactionCount.Should().Be(1);
        result.Transactions[0].Category.Should().Be(TransactionCategoryType.Subscription);
    }

    [Fact]
    public async Task Handle_InvalidRange_Throws()
    {
        var sut = new GetTransactionsByCategoryHandler(
            new FakeTransactionStore(),
            new RuleBasedTransactionCategorizer(),
            NullLogger<GetTransactionsByCategoryHandler>.Instance);

        var act = () => sut.Handle(
            new GetTransactionsByCategoryQuery(
                "cust-cat",
                TransactionCategoryType.Dining,
                DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-01T00:00:00Z")),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static FinancialTransaction Tx(
        string customerId,
        decimal amount,
        string merchant,
        string details,
        TransactionCategoryType category) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TransactionAmount = amount,
            Currency = "ZAR",
            MerchantName = merchant,
            Details = details,
            TransactionDate = DateTimeOffset.Parse("2026-08-10T12:00:00Z"),
            Source = TransactionSourceType.Bank,
            Category = category
        };
}
