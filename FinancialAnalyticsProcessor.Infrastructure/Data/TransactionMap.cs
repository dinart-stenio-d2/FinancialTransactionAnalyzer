using CsvHelper.Configuration;
using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Infrastructure.Data
{
    /// <summary>
    /// Defines the CSV mapping configuration for the <see cref="Transaction"/> entity.
    /// This mapping is used by CsvHelper to correctly read and write transaction data from and to CSV files.
    /// </summary>
    public class TransactionMap : ClassMap<Transaction>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionMap"/> class.
        /// Configures the CSV column mappings for the <see cref="Transaction"/> entity.
        /// </summary>
        public TransactionMap()
        {
            Map(t => t.TransactionId).Name("TransactionId");
            Map(t => t.UserId).Name("UserId");
            Map(t => t.Date).Name("Date");
            Map(t => t.Amount).Name("Amount");
            Map(t => t.Category).Name("Category");
            Map(t => t.Description).Name("Description");
            Map(t => t.Merchant).Name("Merchant");
        }
    }

}
