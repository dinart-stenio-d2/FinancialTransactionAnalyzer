using FinancialAnalyticsProcessor.Domain.Entities;

namespace FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices
{

    /// <summary>
    /// Defines methods for processing, analyzing, saving, and purging financial transactions.
    /// </summary>
    public interface ITransactionProcessor
    {
        /// <summary>
        /// Processes a list of transactions, validating them, identifying duplicates, 
        /// and inserting unique transactions into the database.
        /// </summary>
        /// <param name="transactions">The collection of transactions to be processed.</param>
        /// <returns>A task representing the asynchronous processing operation.</returns>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails for the provided transactions.
        /// </exception>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs during the transaction processing workflow.
        /// </exception>
        Task ProcessTransactionsAsync(IEnumerable<Transaction> transactions);

        /// <summary>
        /// Performs analysis on financial transactions, computing summaries, 
        /// top spending categories, and the highest spender.
        /// </summary>
        /// <returns>
        /// A task representing the asynchronous operation, returning a dynamic object 
        /// containing the analysis results.
        /// </returns>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs while analyzing the transactions.
        /// </exception>
        Task<dynamic> PerformAnalysisAsync();

        /// <summary>
        /// Saves a report to a specified file path in JSON format.
        /// </summary>
        /// <param name="report">The report object to be serialized and saved.</param>
        /// <param name="outputPath">The file path where the report should be saved.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="ReportSerializationException">
        /// Thrown if an error occurs while serializing the report.
        /// </exception>
        /// <exception cref="FileProcessingException">
        /// Thrown if an error occurs while writing the report to the file system.
        /// </exception>
        Task SaveReportAsync(dynamic report, string outputPath);

        /// <summary>
        /// Deletes all transactions from the database.
        /// </summary>
        /// <returns>A task representing the asynchronous purge operation.</returns>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs while purging the transactions.
        /// </exception>
        Task PurgeAllTransactionsAsync();
    }

}
