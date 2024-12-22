using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using Hangfire;
using Polly;
using Polly.Retry;
using System;

namespace FinancialAnalyticsProcessor.Worker.Jobs
{
    public class TransactionJob
    {
        private readonly ICsvTransactionLoader _loader;
        private readonly ITransactionProcessor _processor;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly ILogger<TransactionJob> _logger;

        public TransactionJob(ICsvTransactionLoader loader, ITransactionProcessor processor, AsyncRetryPolicy retryPolicy, ILogger<TransactionJob> logger)
        {
            _loader = loader;
            _processor = processor;
            _retryPolicy = retryPolicy;
            _logger = logger;
        }

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

                    _logger.LogInformation("Loading transactions from CSV file at {Time}." , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    var transactions = await _loader.LoadTransactionsAsync(stream);
                    _logger.LogInformation("Loaded {TransactionCount} transactions at {Time}", transactions.Count() , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Processing transactionsat {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.ProcessTransactionsAsync(transactions).ConfigureAwait(false);
                    _logger.LogInformation("Transactions processed successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Performing analysis on transactions at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    var analysis = await _processor.PerformAnalysisAsync();
                    _logger.LogInformation("Analysis completed successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Saving analysis report to {OutputPath} at {Time}", outputPath , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.SaveReportAsync(analysis, outputPath);
                    _logger.LogInformation("Report saved successfully to {OutputPath} at {Time}", outputPath , DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    _logger.LogInformation("Deleting all transactions at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    await _processor.PurgeAllTransactionsAsync();
                    _logger.LogInformation("All transactions deleted successfully at {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during job executionat {Time}.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
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
