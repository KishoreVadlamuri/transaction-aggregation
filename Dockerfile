# Multi-stage production Dockerfile for Transaction Aggregation API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TransactionAggregation.slnx ./
COPY src/TransactionAggregation.Domain/TransactionAggregation.Domain.csproj src/TransactionAggregation.Domain/
COPY src/TransactionAggregation.Application/TransactionAggregation.Application.csproj src/TransactionAggregation.Application/
COPY src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj src/TransactionAggregation.Infrastructure/
COPY src/TransactionAggregation.Messaging/TransactionAggregation.Messaging.csproj src/TransactionAggregation.Messaging/
COPY src/TransactionAggregation.Api/TransactionAggregation.Api.csproj src/TransactionAggregation.Api/
COPY src/TransactionAggregation.ExternalPublisher/TransactionAggregation.ExternalPublisher.csproj src/TransactionAggregation.ExternalPublisher/
COPY src/TransactionAggregation.UnitTests/TransactionAggregation.UnitTests.csproj src/TransactionAggregation.UnitTests/

RUN dotnet restore TransactionAggregation.slnx

COPY src/ src/

RUN dotnet publish src/TransactionAggregation.Api/TransactionAggregation.Api.csproj \
    -c Release \
    -o /app/publish/api \
    /p:UseAppHost=false \
 && dotnet publish src/TransactionAggregation.ExternalPublisher/TransactionAggregation.ExternalPublisher.csproj \
    -c Release \
    -o /app/publish/publisher \
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
ENV Aggregation__ExpensiveComputationDelayMs=1500
ENV Kafka__BootstrapServers=kafka:9092
ENV ServiceAccount__Username=appuser
ENV ServiceAccount__PasswordHash=AQAAAAIAAYagAAAAEM4nTp2F4Jr8ev8AqBhqc7z2ltXlgwIUautkG0PjCoBWGepWTLj7eILTYQLfw0dzKw==

EXPOSE 8080

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/api .
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "TransactionAggregation.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS publisher
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production
ENV Kafka__BootstrapServers=kafka:9092
ENV Kafka__Topic=customer-transactions
ENV Kafka__ConsumerGroupId=transaction-aggregation
ENV Kafka__ClientId=transaction-transaction-aggregation-api
ENV Publisher__Enabled=true
ENV Publisher__IntervalSeconds=10
ENV Publisher__ChunkSize=50
ENV Publisher__DataFilePath=Data/financial-transactions.json

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish/publisher .
USER $APP_UID

ENTRYPOINT ["dotnet", "TransactionAggregation.ExternalPublisher.dll"]
