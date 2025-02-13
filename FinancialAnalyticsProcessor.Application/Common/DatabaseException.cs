using Microsoft.Data.SqlClient;

namespace FinancialAnalyticsProcessor.Application.Common
{
    public class DatabaseException : Exception
    {
        public int? ErrorCode { get; }

        public DatabaseException() { }

        public DatabaseException(string message) : base(message) { }

        public DatabaseException(string message, Exception innerException)
            : base(message, innerException)
        {
            if (innerException is SqlException sqlEx)
            {
                ErrorCode = sqlEx.Number; // Código do erro SQL Server
            }
        }

        public DatabaseException(string message, int errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
