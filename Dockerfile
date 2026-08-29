# Multi-stage production Dockerfile for Transaction Aggregation API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TransactionAggregation.slnx ./
COPY src/TransactionAggregation.Domain/TransactionAggregation.Domain.csproj src/TransactionAggregation.Domain/
COPY src/TransactionAggregation.Application/TransactionAggregation.Application.csproj src/TransactionAggregation.Application/
COPY src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj src/TransactionAggregation.Infrastructure/
COPY src/TransactionAggregation.Api/TransactionAggregation.Api.csproj src/TransactionAggregation.Api/
COPY src/TransactionAggregation.UnitTests/TransactionAggregation.UnitTests.csproj src/TransactionAggregation.UnitTests/

RUN dotnet restore TransactionAggregation.slnx

COPY src/ src/

RUN dotnet publish src/TransactionAggregation.Api/TransactionAggregation.Api.csproj \
    -c Release \
    -o /app/publish/api \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app

# aspnet:10.0 images do not include adduser; use the built-in non-root APP_UID.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/snapshots \
    && chown -R $APP_UID:$APP_UID /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/api .
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TransactionAggregation.Api.dll"]
