using TransactionAggregtion.Domain.Enums;

namespace TransactionAggregtion.Domain.Entities;

public class CategoryAggregate
{
    public required TransactionCategoryType Category { get; init; }
    public required int TransactionCount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal AverageAmount { get; init; }
}
