namespace FinancialAnalyticsProcessor.Application.Common
{
    public class FileProcessingException : Exception
    {
        public string FilePath { get; }

        public FileProcessingException() { }

        public FileProcessingException(string message) : base(message) { }

        public FileProcessingException(string message, Exception innerException)
            : base(message, innerException) { }

        public FileProcessingException(string message, string filePath, Exception innerException)
            : base(message, innerException)
        {
            FilePath = filePath;
        }
    }
}
