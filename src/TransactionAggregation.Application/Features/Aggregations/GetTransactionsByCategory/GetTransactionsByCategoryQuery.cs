using MediatR;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Aggregations.GetTransactionsByCategory;

public sealed record GetTransactionsByCategoryQuery(
    string CustomerId,
    TransactionCategoryType Category,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<CategoryTransactionsResult>;

public sealed class CategoryTransactionsResult
{
    public required string CustomerId { get; init; }
    public required TransactionCategoryType Category { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required int TransactionCount { get; init; }
    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }
    public required IReadOnlyList<FinancialTransaction> Transactions { get; init; }
}