using MediatR;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Features.Aggregations.GetCustomerAggregation;

public sealed record GetCustomerAggregationQuery(
    string CustomerId,
    DateTimeOffset From,
    DateTimeOffset To,
    bool ForceRefresh = false) : IRequest<CustomerAggregationResult>;
