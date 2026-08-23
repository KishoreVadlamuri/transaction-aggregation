using MediatR;
using TransactionAggregtion.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.GetTransactions;

public sealed record GetTransactionsQuery(
    string CustomerId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<IReadOnlyList<FinancialTransaction>>;
