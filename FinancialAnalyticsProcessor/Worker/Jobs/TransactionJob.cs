using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using Hangfire;
using Polly;
using Polly.Retry;

namespace FinancialAnalyticsProcessor.Worker.Jobs
{
    /// <summary>
    /// Represents a scheduled job that processes financial transactions from a CSV file, performs analysis,
    /// saves a report, and purges transactions from the database.
    /// </summary>
    public class TransactionJob
    {
        /// <summary>
        /// The service responsible for loading transactions from a CSV file.
        /// </summary>
        private readonly ICsvTransactionLoader _loader;

        /// <summary>
        /// The service responsible for processing, analyzing, and managing transactions.
        /// </summary>
        private readonly ITransactionProcessor _processor;

        /// <summary>
        /// The retry policy used to handle failures and retry execution when necessary.
        /// </summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        /// <summary>
        /// The logger instance for recording informational messages, warnings, and errors.
        /// </summary>
        private readonly ILogger<TransactionJob> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionJob"/> class.
        /// </summary>
        /// <param name="loader">The transaction loader responsible for reading transactions from CSV.</param>
        /// <param name="processor">The transaction processor responsible for handling transactions.</param>
        /// <param name="retryPolicy">The retry policy for handling failures and retrying execution.</param>
        /// <param name="logger">The logger instance for logging job execution details.</param>
        public TransactionJob(ICsvTransactionLoader loader, ITransactionProcessor processor, AsyncRetryPolicy retryPolicy, ILogger<TransactionJob> logger)
        {
            _loader = loader;
            _processor = processor;
            _retryPolicy = retryPolicy;
            _logger = logger;
        }

        /// <summary>
        /// Executes the job that loads transactions from a CSV file, processes them, performs analysis,
        /// saves a report, and deletes processed transactions.
        /// </summary>
        /// <param name="csvFilePath">The path to the CSV file containing transactions.</param>
        /// <param name="outputPath">The path where the analysis report will be saved.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="Exception">
        /// Thrown if an error occurs during job execution.
        /// </exception>
        [DisableConcurrentExecution(timeoutInSeconds: 1800)] // Maximum blocking time: 30 minutes   
        public async Task ExecuteAsync(string csvFilePath, string outputPath)
        {
            _logger.LogInformation("Job started at {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            await _retryPolicy.ExecuteAsync(async context =>
            {
                try
                {
                    _logger.LogInformation("Opening CSV file: {CsvFilePath}", csvFilePath);
                    using var stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read);

                    _logger.LogInformation("Loading transactions from CSV file at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    var transactions = await _loader.LoadTransactionsAsync(stream);
                    _logger.LogInformation("Loaded {TransactionCount} transactions at {Time}", transactions.Count(), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Processing transactions at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.ProcessTransactionsAsync(transactions).ConfigureAwait(false);
                    _logger.LogInformation("Transactions processed successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Performing analysis on transactions at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    var analysis = await _processor.PerformAnalysisAsync();
                    _logger.LogInformation("Analysis completed successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Saving analysis report to {OutputPath} at {Time}", outputPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.SaveReportAsync(analysis, outputPath);
                    _logger.LogInformation("Report saved successfully to {OutputPath} at {Time}", outputPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Deleting all transactions at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.PurgeAllTransactionsAsync();
                    _logger.LogInformation("All transactions deleted successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during job execution at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    throw; // Re-throw for Polly to handle the exception
                }
            },
            new Context
            {
            { "csvFilePath", csvFilePath }
            });

            _logger.LogInformation("Job finished at {Time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
