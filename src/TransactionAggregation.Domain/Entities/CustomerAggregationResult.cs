using System;
using System.Collections.Generic;
using System.Text;

namespace TransactionAggregation.Domain.Entities
{
    public class CustomerAggregationResult
    {
        public required string CustomerId { get; init; }
        public required DateTimeOffset From { get; init; }
        public required DateTimeOffset To { get; init; }
        public required int TotalTransactions { get; init; }
        public required decimal TotalSpend { get; init; }
        public required decimal TotalIncome { get; init; }
        public required decimal NetAmount { get; init; }
        public required string Currency { get; init; }
        public required IReadOnlyList<CategoryAggregate> Categories { get; init; }
        public required DateTimeOffset GeneratedAt { get; init; }
        public required TimeSpan ComputationDuration { get; init; }
        public required bool ServedFromCache { get; init; }
        public required IReadOnlyList<string> SourcesIncluded { get; init; }
    }
}
