using MediatR;
using TransactionAggregtion.Domain.Entities;

namespace TransactionAggregation.Application.Features.Transactions.GetTransactions;

public sealed class GetTransactionsHandler
: IRequestHandler<GetTransactionsQuery, IReadOnlyList<FinancialTransaction>>
{
    public GetTransactionsHandler()
    {
    }

    public Task<IReadOnlyList<FinancialTransaction>> Handle(
        GetTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);

        throw new Exception("Not implemented yet");
    }
}
