namespace FinancialAnalyticsProcessor.Application.Common
{
    public class TransactionProcessingException : Exception
    {
        public TransactionProcessingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
