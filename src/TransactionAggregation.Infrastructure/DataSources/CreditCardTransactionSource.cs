using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.DataSources;

public sealed class CreditCardTransactionSource (ILogger<CreditCardTransactionSource> logger) : ITransactionSource
{
    public string Name => "CreditCard";

    public Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var seed = HashCode.Combine(customerId.ToLowerInvariant(), "card");
        var random = new Random(seed);
        var transactions = new List<FinancialTransaction>();
        var cursor = from.Date;

        while (cursor <= to.Date)
        {
            if (cursor.DayOfWeek is DayOfWeek.Friday)
            {
                transactions.Add(Create(
                    customerId,
                    -Round(random.Next(40, 95) + random.NextDouble()),
                    "Downtown Bistro Restaurant",
                    "Dinner with friends",
                    cursor.AddHours(20),
                    random));
            }

            if (cursor.Day % 7 == 0)
            {
                transactions.Add(Create(
                    customerId,
                    -15.99m,
                    "Netflix",
                    "Monthly subscription",
                    cursor.AddHours(6),
                    random));
            }

            if (cursor.DayOfWeek == DayOfWeek.Saturday)
            {
                transactions.Add(Create(
                    customerId,
                    -Round(random.Next(25, 180) + random.NextDouble()),
                    "Amazon Marketplace",
                    "Online shopping order",
                    cursor.AddHours(14),
                    random));
            }

            if (cursor.Day == 3)
            {
                transactions.Add(Create(
                    customerId,
                    -62.40m,
                    "Shell Gas Station",
                    "Fuel fill-up",
                    cursor.AddHours(17),
                    random));
            }

            cursor = cursor.AddDays(1);
        }

        logger.LogDebug(
            "CreditCard generated {Count} transactions for {CustomerId} between {From} and {To}",
            transactions.Count,
            customerId,
            from,
            to);

        return Task.FromResult<IReadOnlyList<FinancialTransaction>>(transactions);
    }

    private static FinancialTransaction Create(
        string customerId,
        decimal amount,
        string merchant,
        string details,
        DateTimeOffset occurredAt,
        Random random) =>
        new()
        {
            Id = CreateDeterministicId(customerId, merchant, occurredAt, amount),
            CustomerId = customerId,
            TransactionAmount = amount,
            Currency = "USD",
            MerchantName = merchant,
            Details = details,
            TransactionDate = occurredAt,
            Source = TransactionSourceType.CreditCard,
            ExternalReference = $"CC-{random.Next(100000, 999999)}"
        };

    private static Guid CreateDeterministicId(string customerId, string merchant, DateTimeOffset occurredAt, decimal amount)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{customerId}|card|{merchant}|{occurredAt:O}|{amount}"));
        return new Guid(bytes);
    }

    private static decimal Round(double value) => decimal.Round((decimal)value, 2);
}
