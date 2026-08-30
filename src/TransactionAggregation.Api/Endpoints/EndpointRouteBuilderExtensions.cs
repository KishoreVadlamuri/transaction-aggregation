namespace TransactionAggregation.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapTransactionEndpoints();
        app.MapAggregationEndpoints();
        return app;
    }
}
