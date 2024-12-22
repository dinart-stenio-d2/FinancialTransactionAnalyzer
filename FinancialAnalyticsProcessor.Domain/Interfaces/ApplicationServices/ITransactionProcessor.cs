using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices
{
    public interface ITransactionProcessor
    {
        public Task ProcessTransactionsAsync(IEnumerable<Transaction> transactions);
        public Task<dynamic> PerformAnalysisAsync();
        public Task SaveReportAsync(dynamic report, string outputPath);
        public Task PurgeAllTransactionsAsync();

    }
}
