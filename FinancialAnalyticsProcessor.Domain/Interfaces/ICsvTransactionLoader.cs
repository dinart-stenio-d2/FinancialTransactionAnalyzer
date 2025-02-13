using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Domain.Interfaces
{
    /// <summary>
    /// Defines methods for loading and modifying transactions from a CSV file.
    /// </summary>
    public interface ICsvTransactionLoader
    {
        /// <summary>
        /// Loads transactions asynchronously from a CSV stream.
        /// </summary>
        /// <param name="csvStream">The stream containing CSV data.</param>
        /// <returns>
        /// A task representing the asynchronous operation, returning an <see cref="IEnumerable{Transaction}"/> 
        /// containing the parsed transactions.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the provided <paramref name="csvStream"/> is null or not readable.
        /// </exception>
        /// <exception cref="CsvHelperException">
        /// Thrown if an error occurs while parsing the CSV data.
        /// </exception>
        /// <exception cref="IOException">
        /// Thrown if an I/O error occurs while reading from the CSV stream.
        /// </exception>
        Task<IEnumerable<Transaction>> LoadTransactionsAsync(Stream csvStream);

        /// <summary>
        /// Recreates a transaction in a CSV file by modifying its description.
        /// </summary>
        /// <param name="csvFilePath">The file path of the CSV file.</param>
        /// <param name="transactionId">The ID of the transaction to be recreated.</param>
        /// <param name="newDescription">The new description to apply to the transaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="TransactionNotFoundException">
        /// Thrown if the specified transaction ID is not found in the file.
        /// </exception>
        /// <exception cref="CsvProcessingException">
        /// Thrown if an error occurs while reading or writing the CSV file.
        /// </exception>
        Task RecreateTransactionAsync(string csvFilePath, Guid transactionId, string newDescription);
    }

}
