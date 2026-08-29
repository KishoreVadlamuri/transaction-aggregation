using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Persistence;

public sealed class PostgresTransactionStore(TransactionDbContext db, ILogger<PostgresTransactionStore> logger) : ITransactionStore
{
    public async Task UpsertManyAsync(IEnumerable<FinancialTransaction> transactions, CancellationToken cancellationToken = default)
    {
        var items = transactions.Select(FinancialTransactionRecord.FromDomain).ToList();
        if (items.Count == 0)
        {
            logger.LogDebug("UpsertManyAsync called with empty batch; skipping");
            return;
        }

        logger.LogDebug("Upserting {Count} transactions into PostgreSQL", items.Count);

        var ids = items.Select(x => x.Id).ToList();
        var existing = await db.Transactions
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var inserted = 0;
        var updated = 0;

        foreach (var item in items)
        {
            if (existing.TryGetValue(item.Id, out var current))
            {
                current.CustomerId = item.CustomerId;
                current.TransactionAmount = item.TransactionAmount;
                current.Currency = item.Currency;
                current.MerchantName = item.MerchantName;
                current.Details = item.Details;
                current.TransactionDate = item.TransactionDate;
                current.Source = item.Source;
                current.Category = item.Category;
                current.ExternalReference = item.ExternalReference;
                updated++;
            }
            else
            {
                db.Transactions.Add(item);
                inserted++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PostgreSQL upsert complete: {Inserted} inserted, {Updated} updated ({Total} total)",
            inserted,
            updated,
            items.Count);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAsync(
        string customerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Querying PostgreSQL transactions for {CustomerId} from {From} to {To}",
            customerId,
            from,
            to);

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.CustomerId.ToLower() == customerId.ToLower() &&
                t.TransactionDate >= from &&
                t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "PostgreSQL returned {Count} transactions for {CustomerId}",
            rows.Count,
            customerId);

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<FinancialTransaction>> GetByCustomerAndCategoryAsync(
        string customerId,
        TransactionCategoryType category,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "Querying PostgreSQL transactions for {CustomerId} category {Category} from {From} to {To}",
            customerId,
            category,
            from,
            to);

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t =>
                t.CustomerId.ToLower() == customerId.ToLower() &&
                t.Category == category &&
                t.TransactionDate >= from &&
                t.TransactionDate <= to)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "PostgreSQL returned {Count} transactions for {CustomerId} in category {Category}",
            rows.Count,
            customerId,
            category);

        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var count = await db.Transactions.CountAsync(cancellationToken);
        logger.LogDebug("PostgreSQL transaction count is {Count}", count);
        return count;
    }
}
