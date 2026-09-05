using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Infrastructure.DataSources;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class MockTransactionSourceTests
{
    [Fact]
    public async Task MockSources_ReturnDeterministicData_ForSameCustomer()
    {
        var bank = new BankTransactionSource(NullLogger<BankTransactionSource>.Instance);
        var card = new CreditCardTransactionSource(NullLogger<CreditCardTransactionSource>.Instance);
        var wallet = new PaymentProviderTransactionSource(NullLogger<PaymentProviderTransactionSource>.Instance);

        var from = DateTimeOffset.Parse("2026-07-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-07-31T23:59:59Z");

        var first = await bank.GetTransactionsAsync("cust-42", from, to);
        var second = await bank.GetTransactionsAsync("cust-42", from, to);
        var cardTx = await card.GetTransactionsAsync("cust-42", from, to);
        var walletTx = await wallet.GetTransactionsAsync("cust-42", from, to);

        first.Should().NotBeEmpty();
        first.Select(t => t.Id).Should().Equal(second.Select(t => t.Id));
        cardTx.Should().NotBeEmpty();
        walletTx.Should().NotBeEmpty();
        first.Should().OnlyContain(t => t.Source == Domain.Enums.TransactionSourceType.Bank);
        cardTx.Should().OnlyContain(t => t.Source == Domain.Enums.TransactionSourceType.CreditCard);
        walletTx.Should().OnlyContain(t => t.Source == Domain.Enums.TransactionSourceType.PaymentProvider);
    }
}
