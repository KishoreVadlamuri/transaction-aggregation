using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.DataSources;

public sealed class BankTransactionSource (ILogger<BankTransactionSource> logger) : ITransactionSource
{
    public string Name => "Bank";

    public Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var seed = StableSeed(customerId, "bank");
        var random = new Random(seed);
        var transactions = new List<FinancialTransaction>();
        var cursor = from.Date;

        while (cursor <= to.Date)
        {
            if (cursor.Day == 1)
            {
                transactions.Add(Create(
                    customerId,
                    4250.00m,
                    "ACME Corp Payroll",
                    "Bi-weekly salary deposit",
                    cursor.AddHours(8),
                    random));
            }

            if (cursor.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday)
            {
                transactions.Add(Create(
                    customerId,
                    -Round(random.Next(35, 120) + random.NextDouble()),
                    "Whole Foods Market",
                    "Grocery purchase",
                    cursor.AddHours(18),
                    random));
            }

            if (cursor.Day == 15)
            {
                transactions.Add(Create(
                    customerId,
                    -148.55m,
                    "City Electric Utility",
                    "Monthly electric bill",
                    cursor.AddHours(9),
                    random));
            }

            cursor = cursor.AddDays(1);
        }

        logger.LogDebug(
            "Bank generated {Count} transactions for {CustomerId} between {From} and {To}",
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
            Currency = "ZAR",
            MerchantName = merchant,
            Details = details,
            TransactionDate = occurredAt,
            Source = TransactionSourceType.Bank,
            ExternalReference = $"BANK-{random.Next(100000, 999999)}"
        };

    private static Guid CreateDeterministicId(string customerId, string merchant, DateTimeOffset occurredAt, decimal amount)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{customerId}|bank|{merchant}|{occurredAt:O}|{amount}"));
        return new Guid(bytes);
    }

    private static int StableSeed(string customerId, string source) =>
        HashCode.Combine(customerId.ToLowerInvariant(), source);

    private static decimal Round(double value) => decimal.Round((decimal)value, 2);
}
