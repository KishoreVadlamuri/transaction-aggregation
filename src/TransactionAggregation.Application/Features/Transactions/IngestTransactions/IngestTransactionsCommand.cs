using MediatR;

namespace TransactionAggregation.Application.Features.Transactions.IngestTransactions;

public sealed record IngestTransactionsCommand(
string CustomerId,
DateTimeOffset From,
DateTimeOffset To) : IRequest<IngestTransactionsResult>;

public sealed class IngestTransactionsResult
{
    public required string CustomerId { get; init; }
    public required int IngestedCount { get; init; }
    public required IReadOnlyList<string> Sources { get; init; }
}
