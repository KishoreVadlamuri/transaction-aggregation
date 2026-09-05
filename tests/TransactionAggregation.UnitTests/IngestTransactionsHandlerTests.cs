using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TransactionAggregation.Application.Features.Transactions.IngestTransactions;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class IngestTransactionsHandlerTests
{
    [Fact]
    public async Task Handle_CategorizesStoresAndPublishes()
    {
        var source = new Mock<ITransactionSource>();
        source.SetupGet(s => s.Name).Returns("MockCreditCard");
        source.Setup(s => s.GetTransactionsAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinancialTransaction>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CustomerId = "cust-9",
                    TransactionAmount = -15.99m,
                    Currency = "ZAR",
                    MerchantName = "Netflix",
                    Details = "Monthly subscription",
                    TransactionDate = DateTimeOffset.UtcNow,
                    Source = TransactionSourceType.CreditCard,
                    Category = TransactionCategoryType.Uncategorized
                }
            });

        var store = new FakeTransactionStore();

        var sut = new IngestTransactionsHandler(
            [source.Object],
            store,
            new RuleBasedTransactionCategorizer(),
            NullLogger<IngestTransactionsHandler>.Instance);

        var result = await sut.Handle(
            new IngestTransactionsCommand(
                "cust-9",
                DateTimeOffset.UtcNow.AddDays(-7),
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        result.IngestedCount.Should().Be(1);
        result.Sources.Should().ContainSingle().Which.Should().Be("MockCreditCard");

        var stored = await store.GetByCustomerAsync("cust-9", DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        stored.Should().ContainSingle();
        stored[0].Category.Should().Be(TransactionCategoryType.Subscription);
    }
}
