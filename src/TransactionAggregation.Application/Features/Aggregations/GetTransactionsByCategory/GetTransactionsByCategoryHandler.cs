using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Aggregations.GetTransactionsByCategory;

public sealed class GetTransactionsByCategoryHandler (ITransactionStore store,
        ITransactionCategorizer categorizer,
        ILogger<GetTransactionsByCategoryHandler> logger)
    : IRequestHandler<GetTransactionsByCategoryQuery, CategoryTransactionsResult>
{
    public async Task<CategoryTransactionsResult> Handle(
        GetTransactionsByCategoryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);
        if (!Enum.IsDefined(request.Category))
        {
            throw new ArgumentException($"Unknown category '{request.Category}'.", nameof(request.Category));
        }

        if (request.To < request.From)
        {
            logger.LogWarning(
                "Invalid date range for category transactions: CustomerId={CustomerId}, From={From}, To={To}",
                request.CustomerId,
                request.From,
                request.To);
            throw new ArgumentException("'to' must be greater than or equal to 'from'.", nameof(request.To));
        }

        logger.LogInformation(
            "Fetching transactions for {CustomerId} in category {Category} from {From} to {To}",
            request.CustomerId,
            request.Category,
            request.From,
            request.To);

        // Ensure any uncategorized stored rows are categorized before filtering.
        var stored = await store.GetByCustomerAsync(
            request.CustomerId,
            request.From,
            request.To,
            cancellationToken);

        var recategorized = new List<Domain.Entities.FinancialTransaction>();
        foreach (var tx in stored)
        {
            if (tx.Category == TransactionCategoryType.Uncategorized)
            {
                tx.Category = categorizer.Categorize(tx);
                recategorized.Add(tx);
            }
        }

        if (recategorized.Count > 0)
        {
            await store.UpsertManyAsync(recategorized, cancellationToken);
            logger.LogInformation(
                "Recategorized and persisted {Count} previously uncategorized transactions for {CustomerId}",
                recategorized.Count,
                request.CustomerId);
        }

        var transactions = await store.GetByCustomerAndCategoryAsync(
            request.CustomerId,
            request.Category,
            request.From,
            request.To,
            cancellationToken);

        var totalAmount = decimal.Round(transactions.Sum(t => t.TransactionAmount), 2);
        var currency = transactions.Select(t => t.Currency).FirstOrDefault() ?? "ZAR";

        logger.LogInformation(
            "Returning {Count} transactions for {CustomerId} in category {Category} (TotalAmount={TotalAmount})",
            transactions.Count,
            request.CustomerId,
            request.Category,
            totalAmount);

        return new CategoryTransactionsResult
        {
            CustomerId = request.CustomerId,
            Category = request.Category,
            From = request.From,
            To = request.To,
            TransactionCount = transactions.Count,
            TotalAmount = totalAmount,
            Currency = currency,
            Transactions = transactions
        };
    }
}

