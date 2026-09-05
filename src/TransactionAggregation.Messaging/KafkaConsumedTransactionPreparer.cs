using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Messaging;

/// <summary>
/// Applies the same Uncategorized → rule-based category step used by ingest
/// before Kafka-consumed transactions are persisted.
/// </summary>
public static class KafkaConsumedTransactionPreparer
{
    public static void EnsureCategorized(
        FinancialTransaction transaction,
        ITransactionCategorizer categorizer)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(categorizer);

        if (transaction.Category == TransactionCategoryType.Uncategorized)
        {
            transaction.Category = categorizer.Categorize(transaction);
        }
    }
}
