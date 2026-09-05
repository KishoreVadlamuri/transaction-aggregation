using FluentAssertions;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class RuleBasedTransactionCategorizerTests
{
    private readonly RuleBasedTransactionCategorizer _sut = new();

    [Theory]
    [InlineData("Whole Foods Market", "Grocery purchase", TransactionCategoryType.Groceries)]
    [InlineData("Uber Trip", "Rideshare to office", TransactionCategoryType.Transport)]
    [InlineData("Netflix", "Monthly subscription", TransactionCategoryType.Subscription)]
    [InlineData("Downtown Bistro Restaurant", "Dinner", TransactionCategoryType.Dining)]
    [InlineData("City Electric Utility", "Monthly electric bill", TransactionCategoryType.Utilities)]
    [InlineData("ACME Corp Payroll", "Bi-weekly salary deposit", TransactionCategoryType.Income)]
    [InlineData("Venmo Transfer", "Transfer to roommate", TransactionCategoryType.Transfer)]
    [InlineData("Mystery Shop", "Unknown item", TransactionCategoryType.Other)]
    public void Categorize_MapsMerchantAndDescription_ToExpectedCategory(
        string merchant,
        string description,
        TransactionCategoryType expected)
    {
        var amount = expected == TransactionCategoryType.Income ? 100m : -25m;
        var tx = CreateTransaction(merchant, description, amount);

        var category = _sut.Categorize(tx);

        category.Should().Be(expected);
    }

    private static FinancialTransaction CreateTransaction(string merchant, string description, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = "cust-1",
            TransactionAmount = amount,
            Currency = "ZAR",
            MerchantName = merchant,
            Details = description,
            TransactionDate = DateTimeOffset.UtcNow,
            Source = TransactionSourceType.Bank
        };
}
