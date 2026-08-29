dotnet ef migrations add InitialCreate
  --project src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj
  --startup-project src/TransactionAggregation.Api/TransactionAggregation.Api.csproj
  --output-dir Persistence/Migrations
  --context TransactionDbContext

dotnet ef database update
  --project src/TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj
  --startup-project src/TransactionAggregation.Api/TransactionAggregation.Api.csproj
  --context TransactionDbContext