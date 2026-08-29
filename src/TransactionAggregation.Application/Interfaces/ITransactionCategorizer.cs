using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Interfaces;

public interface ITransactionCategorizer
{
    TransactionCategoryType Categorize(FinancialTransaction transaction);
}
