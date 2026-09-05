using FluentAssertions;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Messaging;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class KafkaConsumedTransactionPreparerTests
{
    [Fact]
    public void EnsureCategorized_MapsUncategorizedUsingRules()
    {
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = "cust-kafka",
            TransactionAmount = -15.99m,
            Currency = "ZAR",
            MerchantName = "Netflix",
            Details = "Monthly subscription",
            TransactionDate = DateTimeOffset.UtcNow,
            Source = TransactionSourceType.CreditCard,
            Category = TransactionCategoryType.Uncategorized
        };

        KafkaConsumedTransactionPreparer.EnsureCategorized(
            transaction,
            new RuleBasedTransactionCategorizer());

        transaction.Category.Should().Be(TransactionCategoryType.Subscription);
    }

    [Fact]
    public void EnsureCategorized_LeavesExistingCategoryUntouched()
    {
        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            CustomerId = "cust-kafka",
            TransactionAmount = -40m,
            Currency = "ZAR",
            MerchantName = "Uber Trip",
            Details = "ride",
            TransactionDate = DateTimeOffset.UtcNow,
            Source = TransactionSourceType.PaymentProvider,
            Category = TransactionCategoryType.Transport
        };

        KafkaConsumedTransactionPreparer.EnsureCategorized(
            transaction,
            new RuleBasedTransactionCategorizer());

        transaction.Category.Should().Be(TransactionCategoryType.Transport);
    }
}
