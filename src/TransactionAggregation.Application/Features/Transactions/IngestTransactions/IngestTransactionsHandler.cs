using MediatR;
using Microsoft.Extensions.Logging;

namespace TransactionAggregation.Application.Features.Transactions.IngestTransactions;

public sealed class IngestTransactionsHandler
: IRequestHandler<IngestTransactionsCommand, IngestTransactionsResult>
{
    private readonly ILogger<IngestTransactionsHandler> _logger;

    public IngestTransactionsHandler(
        ILogger<IngestTransactionsHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IngestTransactionsResult> Handle(
        IngestTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CustomerId);

        throw new Exception("Not implemented yet");
    }
}
