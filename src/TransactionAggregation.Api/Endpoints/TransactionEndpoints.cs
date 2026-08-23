using MediatR;
using TransactionAggregation.Application.Features.Transactions.GetTransactions;
using TransactionAggregation.Application.Features.Transactions.IngestTransactions;
using static TransactionAggregation.Api.Models.ApiModels;

namespace TransactionAggregation.Api.Endpoints;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers/{customerId}/transactions")
            .WithTags("Transactions");

        group.MapPost("/ingest", IngestAsync)
            .WithName("IngestTransactions")
            .WithSummary("Ingest and categorize transactions from mock sources")
            .WithDescription(
                "Pulls transactions from all mock data sources, categorizes them, stores them.")
            .Produces<IngestTransactionsResult>(StatusCodes.Status200OK);

        group.MapGet("/", GetStoredAsync)
            .WithName("GetStoredTransactions")
            .WithSummary("Read stored transactions for a customer")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> IngestAsync(
        string customerId,
        IngestionRequest? request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var to = request?.To ?? DateTimeOffset.UtcNow;
        var from = request?.From ?? to.AddDays(-30);

        var result = await sender.Send(
            new IngestTransactionsCommand(customerId, from, to),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetStoredAsync(
        string customerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        var transactions = await sender.Send(
            new GetTransactionsQuery(customerId, start, end),
            cancellationToken);

        return Results.Ok(transactions);
    }
}
