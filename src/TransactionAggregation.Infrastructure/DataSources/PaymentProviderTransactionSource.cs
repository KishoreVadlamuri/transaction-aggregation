using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.DataSources;

public sealed class PaymentProviderTransactionSource (ILogger<PaymentProviderTransactionSource> logger) : ITransactionSource
{
    public string Name => "PaymentProvider";

    public Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var seed = HashCode.Combine(customerId.ToLowerInvariant(), "wallet");
        var random = new Random(seed);
        var transactions = new List<FinancialTransaction>();
        var cursor = from.Date;

        while (cursor <= to.Date)
        {
            if (cursor.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Wednesday)
            {
                transactions.Add(Create(
                    customerId,
                    -Round(random.Next(8, 28) + random.NextDouble()),
                    "Uber Trip",
                    "Rideshare to office",
                    cursor.AddHours(8),
                    random));
            }

            if (cursor.DayOfWeek == DayOfWeek.Monday)
            {
                transactions.Add(Create(
                    customerId,
                    -5.75m,
                    "Starbucks Coffee",
                    "Cafe latte",
                    cursor.AddHours(7),
                    random));
            }

            if (cursor.Day == 10)
            {
                transactions.Add(Create(
                    customerId,
                    -40.00m,
                    "Venmo Transfer",
                    "Transfer to roommate",
                    cursor.AddHours(12),
                    random));
            }

            if (cursor.Day == 20)
            {
                transactions.Add(Create(
                    customerId,
                    -9.99m,
                    "Spotify",
                    "Music subscription",
                    cursor.AddHours(5),
                    random));
            }

            cursor = cursor.AddDays(1);
        }

        logger.LogDebug(
            "PaymentProvider generated {Count} transactions for {CustomerId} between {From} and {To}",
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
        string description,
        DateTimeOffset occurredAt,
        Random random) =>
        new()
        {
            Id = CreateDeterministicId(customerId, merchant, occurredAt, amount),
            CustomerId = customerId,
            TransactionAmount = amount,
            Currency = "USD",
            MerchantName = merchant,
            Details = description,
            TransactionDate = occurredAt,
            Source = TransactionSourceType.PaymentProvider,
            ExternalReference = $"WALLET-{random.Next(100000, 999999)}"
        };

    private static Guid CreateDeterministicId(string customerId, string merchant, DateTimeOffset occurredAt, decimal amount)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{customerId}|wallet|{merchant}|{occurredAt:O}|{amount}"));
        return new Guid(bytes);
    }

    private static decimal Round(double value) => decimal.Round((decimal)value, 2);
}
