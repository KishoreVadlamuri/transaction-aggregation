using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services;

public sealed class RuleBasedTransactionCategorizer : ITransactionCategorizer
{
    private static readonly (string Keyword, TransactionCategoryType Category)[] Rules =
    [
        ("salary", TransactionCategoryType.Income),
        ("payroll", TransactionCategoryType.Income),
        ("deposit", TransactionCategoryType.Income),
        ("uber", TransactionCategoryType.Transport),
        ("lyft", TransactionCategoryType.Transport),
        ("taxi", TransactionCategoryType.Transport),
        ("metro", TransactionCategoryType.Transport),
        ("fuel", TransactionCategoryType.Transport),
        ("gas station", TransactionCategoryType.Transport),
        ("whole foods", TransactionCategoryType.Groceries),
        ("trader joe", TransactionCategoryType.Groceries),
        ("grocery", TransactionCategoryType.Groceries),
        ("supermarket", TransactionCategoryType.Groceries),
        ("starbucks", TransactionCategoryType.Dining),
        ("restaurant", TransactionCategoryType.Dining),
        ("cafe", TransactionCategoryType.Dining),
        ("mcdonald", TransactionCategoryType.Dining),
        ("netflix", TransactionCategoryType.Subscription),
        ("spotify", TransactionCategoryType.Subscription),
        ("subscription", TransactionCategoryType.Subscription),
        ("amazon", TransactionCategoryType.Shopping),
        ("walmart", TransactionCategoryType.Shopping),
        ("target", TransactionCategoryType.Shopping),
        ("pharmacy", TransactionCategoryType.Healthcare),
        ("hospital", TransactionCategoryType.Healthcare),
        ("clinic", TransactionCategoryType.Healthcare),
        ("electric", TransactionCategoryType.Utilities),
        ("water bill", TransactionCategoryType.Utilities),
        ("internet", TransactionCategoryType.Utilities),
        ("utility", TransactionCategoryType.Utilities),
        ("cinema", TransactionCategoryType.Entertainment),
        ("movie", TransactionCategoryType.Entertainment),
        ("concert", TransactionCategoryType.Entertainment),
        ("airline", TransactionCategoryType.Travel),
        ("hotel", TransactionCategoryType.Travel),
        ("airbnb", TransactionCategoryType.Travel),
        ("transfer", TransactionCategoryType.Transfer),
        ("wire", TransactionCategoryType.Transfer),
        ("venmo", TransactionCategoryType.Transfer)
    ];

    private readonly ILogger<RuleBasedTransactionCategorizer> _logger;

    public RuleBasedTransactionCategorizer(ILogger<RuleBasedTransactionCategorizer>? logger = null)
    {
        _logger = logger ?? NullLogger<RuleBasedTransactionCategorizer>.Instance;
    }

    public TransactionCategoryType Categorize(FinancialTransaction transaction)
    {
        var haystack = $"{transaction.MerchantName} {transaction.Details}".ToLowerInvariant();

        if (transaction.TransactionAmount > 0 && ContainsAny(haystack, "salary", "payroll", "deposit", "refund"))
        {
            _logger.LogDebug(
                "Categorized transaction {TransactionId} as {Category} via income heuristic (Merchant={Merchant})",
                transaction.Id,
                TransactionCategoryType.Income,
                transaction.MerchantName);
            return TransactionCategoryType.Income;
        }

        foreach (var (keyword, category) in Rules)
        {
            if (haystack.Contains(keyword, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Categorized transaction {TransactionId} as {Category} via keyword '{Keyword}' (Merchant={Merchant})",
                    transaction.Id,
                    category,
                    keyword,
                    transaction.MerchantName);
                return category;
            }
        }

        var fallback = transaction.TransactionAmount >= 0
            ? TransactionCategoryType.Income
            : TransactionCategoryType.Other;

        _logger.LogDebug(
            "Categorized transaction {TransactionId} as {Category} via fallback (Merchant={Merchant})",
            transaction.Id,
            fallback,
            transaction.MerchantName);

        return fallback;
    }

    private static bool ContainsAny(string value, params string[] keywords) =>
        keywords.Any(k => value.Contains(k, StringComparison.Ordinal));
}

