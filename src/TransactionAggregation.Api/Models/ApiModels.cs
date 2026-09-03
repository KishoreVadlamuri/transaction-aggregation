namespace TransactionAggregation.Api.Models;

public sealed class IngestionRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed class ApiError
{
    public required string Message { get; init; }
    public string? Detail { get; init; }
    public required string TraceId { get; init; }
}