namespace FinancialAnalyticsProcessor.Infrastructure.Exceptions
{
    public class TransactionNotFoundException : Exception
    {
        public Guid TransactionId { get; }
        public string FilePath { get; }

        public TransactionNotFoundException(Guid transactionId, string filePath)
            : base($"Transaction with ID {transactionId} not found in the file: {filePath}.")
        {
            TransactionId = transactionId;
            FilePath = filePath;
        }
    }
}
