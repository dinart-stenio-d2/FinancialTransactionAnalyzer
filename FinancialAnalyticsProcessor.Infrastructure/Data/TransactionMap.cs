using CsvHelper.Configuration;
using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Infrastructure.Data
{
    public class TransactionMap : ClassMap<Transaction>
    {
        public TransactionMap()
        {
            Map(t => t.TransactionId);
            Map(t => t.UserId);
            Map(t => t.Date);
            Map(t => t.Amount);
            Map(t => t.Category);
            Map(t => t.Description);
            Map(t => t.Merchant);
        }
    }
}
