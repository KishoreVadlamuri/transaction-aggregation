using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Domain.Entities;

public class FinancialTransaction
{
    public required Guid Id { get; init; }
    public required string CustomerId { get; init; }
    public required decimal TransactionAmount { get; init; }
    public required string Currency { get; init; }
    public required string MerchantName { get; init; }
    public required string Details { get; init; }
    public required DateTimeOffset TransactionDate { get; init; }
    public required TransactionSourceType Source { get; init; }
    public TransactionCategoryType Category { get; set; } = TransactionCategoryType.Uncategorized;
    public string? ExternalReference { get; init; }
}
