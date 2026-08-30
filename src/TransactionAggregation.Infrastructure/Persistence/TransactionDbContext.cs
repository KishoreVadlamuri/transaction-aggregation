using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Persistence;

public sealed class TransactionDbContext : DbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options)
        : base(options)
    {
    }

    public DbSet<FinancialTransactionRecord> Transactions => Set<FinancialTransactionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<FinancialTransactionRecord>();
        entity.ToTable("transactions");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.CustomerId).HasMaxLength(128).IsRequired();
        entity.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        entity.Property(x => x.MerchantName).HasMaxLength(256).IsRequired();
        entity.Property(x => x.Details).HasMaxLength(1024).IsRequired();
        entity.Property(x => x.ExternalReference).HasMaxLength(128);
        entity.Property(x => x.TransactionAmount).HasPrecision(18, 2);
        entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
        entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
        entity.HasIndex(x => new { x.CustomerId, x.TransactionDate });
    }
}

public sealed class FinancialTransactionRecord
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string MerchantName { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset TransactionDate { get; set; }
    public TransactionSourceType Source { get; set; }
    public TransactionCategoryType Category { get; set; }
    public string? ExternalReference { get; set; }

    public static FinancialTransactionRecord FromDomain(FinancialTransaction tx) =>
        new()
        {
            Id = tx.Id,
            CustomerId = tx.CustomerId,
            TransactionAmount = tx.TransactionAmount,
            Currency = tx.Currency,
            MerchantName = tx.MerchantName,
            Details = tx.Details,
            TransactionDate = tx.TransactionDate,
            Source = tx.Source,
            Category = tx.Category,
            ExternalReference = tx.ExternalReference
        };

    public FinancialTransaction ToDomain() =>
        new()
        {
            Id = Id,
            CustomerId = CustomerId,
            TransactionAmount = TransactionAmount,
            Currency = Currency,
            MerchantName = MerchantName,
            Details = Details,
            TransactionDate = TransactionDate,
            Source = Source,
            Category = Category,
            ExternalReference = ExternalReference
        };
}

