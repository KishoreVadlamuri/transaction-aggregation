using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TransactionAggregation.Application.Features.Aggregations.GetCustomerAggregation;
using TransactionAggregation.Application.Interfaces;
using TransactionAggregation.Application.Options;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Infrastructure.Caching;
using Xunit;

namespace TransactionAggregation.UnitTests;

public class GetCustomerAggregationHandlerTests
{
    [Fact]
    public async Task Handle_InvalidRange_Throws()
    {
        var sut = new GetCustomerAggregationHandler(
            Array.Empty<ITransactionSource>(),
            new FakeTransactionStore(),
            new RuleBasedTransactionCategorizer(),
            CreateCache(),
            TestConfiguration.CreateOptions<AggregationOptions>(AggregationOptions.SectionName),
            TestConfiguration.CreateOptions<CacheOptions>(CacheOptions.SectionName),
            NullLogger<GetCustomerAggregationHandler>.Instance);

        var act = () => sut.Handle(
            new GetCustomerAggregationQuery(
                "cust-1",
                DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
                DateTimeOffset.Parse("2026-08-01T00:00:00Z")),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static ICacheService CreateCache()
    {
        IDistributedCache distributed = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        return new DistributedCacheService(distributed, NullLogger<DistributedCacheService>.Instance);
    }

    private static FinancialTransaction Tx(
        string customerId,
        decimal amount,
        string merchant,
        string details,
        TransactionSourceType source) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            TransactionAmount = amount,
            Currency = TestConfiguration
                .GetOptions<AggregationOptions>(AggregationOptions.SectionName)
                .DefaultCurrency,
            MerchantName = merchant,
            Details = details,
            TransactionDate = DateTimeOffset.Parse("2026-08-10T12:00:00Z"),
            Source = source
        };
}
