using System.Collections.Concurrent;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.UnitTests;

/// <summary>
/// Lightweight in-process store for unit tests.
/// </summary>
internal sealed class FakeTransactionStore : ITransactionStore
{
    private readonly ConcurrentDictionary<Guid, FinancialTransaction> _transactions = new();

    public Task UpsertManyAsync(
        IEnumerable<FinancialTransaction> transactions,
        CancellationToken cancellationToken = default)
    {
        foreach (var tx in transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _transactions[tx.Id] = tx;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FinancialTransaction> results = _transactions.Values
            .Where(t =>
                string.Equals(t.CustomerId, customerId, StringComparison.OrdinalIgnoreCase) &&
                t.TransactionDate >= from &&
                t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAndCategoryAsync(
        string customerId,
        TransactionCategoryType category,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FinancialTransaction> results = _transactions.Values
            .Where(t =>
                string.Equals(t.CustomerId, customerId, StringComparison.OrdinalIgnoreCase) &&
                t.Category == category &&
                t.TransactionDate >= from &&
                t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_transactions.Count);
}
