namespace FinancialAnalyticsProcessor.Infrastructure.Exceptions
{
    public class CsvProcessingException : Exception
    {
        public CsvProcessingException(string message) : base(message) { }

        public CsvProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
