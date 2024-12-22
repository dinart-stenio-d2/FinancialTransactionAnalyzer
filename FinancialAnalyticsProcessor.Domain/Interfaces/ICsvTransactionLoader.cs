using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Domain.Interfaces
{
    public interface ICsvTransactionLoader
    {
       Task<IEnumerable<Transaction>> LoadTransactionsAsync(Stream csvStream);
       public Task RecreateTransactionAsync(string csvFilePath, Guid transactionId, string newDescription);
    }
}
