using Newtonsoft.Json;

namespace FinancialAnalyticsProcessor.Application.Common
{
    public class ReportSerializationException : Exception
    {
        public ReportSerializationException(string message) : base(message) { }

        public ReportSerializationException(string message, Exception innerException)
            : base(message, innerException) { }

        /// <summary>
        /// Creates a <see cref="ReportSerializationException"/> from a Newtonsoft.Json exception.
        /// </summary>
        /// <param name="ex">The original Newtonsoft.Json exception.</param>
        /// <returns>A wrapped exception with a more specific message.</returns>
        public static ReportSerializationException FromJsonException(JsonException ex)
        {
            return ex switch
            {
                JsonSerializationException serializationEx => new ReportSerializationException("An error occurred during JSON serialization.", serializationEx),
                JsonReaderException readerEx => new ReportSerializationException("An error occurred while reading JSON.", readerEx),
                JsonWriterException writerEx => new ReportSerializationException("An error occurred while writing JSON.", writerEx),
                _ => new ReportSerializationException("An unknown JSON processing error occurred.", ex)
            };
        }
    }

}
