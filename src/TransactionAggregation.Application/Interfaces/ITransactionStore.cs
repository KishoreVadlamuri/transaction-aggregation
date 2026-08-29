using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Interfaces;

public interface ITransactionStore
{
    Task UpsertManyAsync(IEnumerable<FinancialTransaction> transactions, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAndCategoryAsync(
        string customerId,
        TransactionCategoryType category,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
