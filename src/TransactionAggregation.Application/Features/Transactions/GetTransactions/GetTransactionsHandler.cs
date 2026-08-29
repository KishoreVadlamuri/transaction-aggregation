using MediatR;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.GetTransactions;

public sealed class GetTransactionsHandler(ITransactionStore store,
        ILogger<GetTransactionsHandler> logger)
    : IRequestHandler<GetTransactionsQuery, IReadOnlyList<FinancialTransaction>>
{
    public async Task<IReadOnlyList<FinancialTransaction>> Handle(
        GetTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);

        logger.LogInformation(
            "Reading stored transactions for {CustomerId} from {From} to {To}",
            request.CustomerId,
            request.From,
            request.To);

        var transactions = await store.GetByCustomerAsync(
            request.CustomerId,
            request.From,
            request.To,
            cancellationToken);

        logger.LogInformation(
            "Returned {Count} stored transactions for {CustomerId}",
            transactions.Count,
            request.CustomerId);

        return transactions;
    }
}