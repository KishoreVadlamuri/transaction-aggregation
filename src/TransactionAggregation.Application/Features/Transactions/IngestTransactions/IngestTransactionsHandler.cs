using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.IngestTransactions;

public sealed class IngestTransactionsHandler (IEnumerable<ITransactionSource> sources,
        ITransactionStore store,
        ITransactionCategorizer categorizer,
        ILogger<IngestTransactionsHandler> logger)
: IRequestHandler<IngestTransactionsCommand, IngestTransactionsResult>
{
    public async Task<IngestTransactionsResult> Handle(
        IngestTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);

        logger.LogInformation(
            "Starting ingestion for {CustomerId} from {From} to {To}",
            request.CustomerId,
            request.From,
            request.To);

        var sourceNames = new List<string>();
        var collected = new List<TransactionAggregation.Domain.Entities.FinancialTransaction>();
        var categorizedCount = 0;

        foreach (var source in sources)
        {
            logger.LogDebug(
                "Fetching transactions from source {Source} for {CustomerId}",
                source.Name,
                request.CustomerId);

            var batch = await source.GetTransactionsAsync(
                request.CustomerId,
                request.From,
                request.To,
                cancellationToken);

            foreach (var tx in batch)
            {
                if (tx.Category == TransactionCategoryType.Uncategorized)
                {
                    tx.Category = categorizer.Categorize(tx);
                    categorizedCount++;
                }
            }

            sourceNames.Add(source.Name);
            collected.AddRange(batch);

            logger.LogInformation(
                "Source {Source} returned {Count} transactions for {CustomerId}",
                source.Name,
                batch.Count,
                request.CustomerId);
        }

        logger.LogDebug(
            "Categorized {CategorizedCount} of {TotalCount} transactions for {CustomerId}",
            categorizedCount,
            collected.Count,
            request.CustomerId);

        await store.UpsertManyAsync(collected, cancellationToken);
        logger.LogInformation(
            "Persisted {Count} transactions for {CustomerId}",
            collected.Count,
            request.CustomerId);

        logger.LogInformation(
            "Ingested {Count} transactions for {CustomerId} from {SourceCount} sources ({Sources})",
            collected.Count,
            request.CustomerId,
            sourceNames.Count,
            string.Join(", ", sourceNames));

        return new IngestTransactionsResult
        {
            CustomerId = request.CustomerId,
            IngestedCount = collected.Count,
            Sources = sourceNames
        };
    }
}
