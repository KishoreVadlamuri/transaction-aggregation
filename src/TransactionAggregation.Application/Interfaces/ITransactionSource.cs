using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Interfaces;

public interface ITransactionSource
{
    string Name { get; }
    Task<IReadOnlyList<FinancialTransaction>> GetTransactionsAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
