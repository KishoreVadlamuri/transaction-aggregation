using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Api.Models;
using TransactionAggregation.Application.Features.Aggregations.GetCustomerAggregation;
using TransactionAggregation.Application.Features.Aggregations.GetTransactionsByCategory;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Api.Endpoints;

public static class AggregationEndpoints
{
    public static void MapAggregationEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1/customers/{customerId}/aggregations")
            .WithTags("Aggregations")
            .RequireAuthorization();

        v1.MapGet("/", GetAggregationAsync)
            .WithName("GetCustomerAggregation")
            .WithSummary("Aggregate Customer Transactions")
            .WithDescription(
                "Fetches multi-source data, categorizes transactions, computes category rollups.")
            .Produces<CustomerAggregationResult>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest);

        v1.MapGet("/categories/{category}", GetTransactionsByCategoryAsync)
            .WithName("GetTransactionsByCategory")
            .WithSummary("Transaction details for a customer by category")
            .WithDescription(
                "Returns all stored transaction details for the customer filtered by category. " +
                "Uncategorized rows are categorized first. Optional from/to default to the last 30 days.")
            .Produces<CategoryTransactionsResult>(StatusCodes.Status200OK)
            .Produces<ApiError>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetAggregationAsync(
    string customerId,
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    ISender sender,
    HttpContext httpContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken,
    bool forceRefresh = false)
    {
        var logger = loggerFactory.CreateLogger("TransactionAggregation.Api.Aggregations");
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        logger.LogInformation(
            "HTTP aggregation requested for {CustomerId} from {From} to {To} (ForceRefresh={ForceRefresh})",
            customerId,
            start,
            end,
            forceRefresh);

        var result = await sender.Send(
            new GetCustomerAggregationQuery(customerId, start, end, forceRefresh),
            cancellationToken);

        httpContext.Response.Headers.Append(
            "X-Computation-Ms",
            ((int)result.ComputationDuration.TotalMilliseconds).ToString());

        logger.LogInformation(
            "HTTP aggregation completed for {CustomerId}: Transactions={Count}, DurationMs={DurationMs}",
            customerId,
            result.TotalTransactions,
            (int)result.ComputationDuration.TotalMilliseconds);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTransactionsByCategoryAsync(
        string customerId,
        TransactionCategoryType category,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        ISender sender,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("TransactionAggregation.Api.Aggregations");
        var end = to ?? DateTimeOffset.UtcNow;
        var start = from ?? end.AddDays(-30);

        logger.LogInformation(
            "HTTP category transactions requested for {CustomerId} category {Category} from {From} to {To}",
            customerId,
            category,
            start,
            end);

        var result = await sender.Send(
            new GetTransactionsByCategoryQuery(customerId, category, start, end),
            cancellationToken);

        logger.LogInformation(
            "HTTP category transactions completed for {CustomerId}/{Category}: {Count} transactions",
            customerId,
            category,
            result.TransactionCount);

        return Results.Ok(result);
    }
}
