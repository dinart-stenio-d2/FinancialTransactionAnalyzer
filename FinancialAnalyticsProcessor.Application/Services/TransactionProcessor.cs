using AutoMapper;
using FinancialAnalyticsProcessor.Application.Common;
using FinancialAnalyticsProcessor.Domain.Entities;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using FinancialAnalyticsProcessor.Domain.Interfaces.Repositories;
using FluentValidation;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace FinancialAnalyticsProcessor.Application.Services
{
    /// <summary>
    /// Service for processing financial transactions, including validation, duplicate detection, and database insertion.   
    /// </summary>
    public class TransactionProcessor : ITransactionProcessor
    {
        /// <summary>
        /// Repository for handling database transactions.
        /// </summary>
        private readonly IRepository<Infrastructure.DbEntities.Transaction> _repository;

        /// <summary>
        /// Logger for logging informational messages and errors during transaction processing.
        /// </summary>
        private readonly ILogger<TransactionProcessor> _logger;

        /// <summary>
        /// AutoMapper instance used for mapping between domain and database entities.
        /// </summary>
        private readonly IMapper _mapper;

        /// <summary>
        /// Validator for individual transactions, ensuring they meet business rules.
        /// </summary>
        private readonly IValidator<Transaction> _transactionValidator;

        /// <summary>
        /// Validator for lists of transactions, ensuring bulk processing meets validation rules.
        /// </summary>
        private readonly IValidator<IEnumerable<Transaction>> _transactionListValidator;

        /// <summary>
        /// Factory for creating scoped service providers, used for managing dependencies within transaction processing.
        /// </summary>
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionProcessor"/> class.
        /// </summary>
        /// <param name="repository">The repository for handling database transactions.</param>
        /// <param name="logger">The logger used for logging information and errors.</param>
        /// <param name="mapper">The AutoMapper instance for mapping between domain and database entities.</param>
        /// <param name="transactionValidator">The validator for individual transactions.</param>
        /// <param name="transactionListValidator">The validator for lists of transactions.</param>
        /// <param name="scopeFactory">The service scope factory for managing dependency injection scopes.</param>
        public TransactionProcessor(IRepository<Infrastructure.DbEntities.Transaction> repository, 
            ILogger<TransactionProcessor> logger , 
            IMapper mapper,
            IValidator<Transaction> transactionValidator,
            IValidator<IEnumerable<Transaction>> transactionListValidator,
            IServiceScopeFactory scopeFactory)
        {
            _repository = repository;
            _logger = logger;
            _mapper = mapper;
            _transactionValidator = transactionValidator;   
            _transactionListValidator = transactionListValidator;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Processes a list of transactions by validating business rules, identifying duplicates, 
        /// and inserting unique transactions into the database.
        /// </summary>
        /// <param name="transactions">The list of transactions to be processed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ValidationException">
        /// Thrown if the validation of business rules fails for the provided transactions.
        /// </exception>
        /// <exception cref="FileProcessingException">
        /// Thrown if an error occurs while writing duplicate transactions to a file.
        /// </exception>
        /// <exception cref="DatabaseException">
        /// Thrown if a database error occurs while inserting transactions.
        /// </exception>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an unexpected error occurs during transaction processing.
        /// </exception>
        public async Task ProcessTransactionsAsync(IEnumerable<Transaction> transactions)
        {
            const int batchSize = 10000;

            try
            {
                _logger.LogInformation("Starting transaction processing.");

                // Initial validation of the list of transactions, here validating the domain's business rules
                var validationResult = await _transactionListValidator.ValidateAsync(transactions);
                if (!validationResult.IsValid)
                {
                    var errorSummary = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                    _logger.LogError("Validation failed for transactions: {Errors}", errorSummary);
                    throw new ValidationException(validationResult.Errors);
                }

                // Map transactions to database entity
                var dbTransactions = transactions
                    .AsParallel()
                    .Select(transaction => _mapper.Map<Infrastructure.DbEntities.Transaction>(transaction))
                    .ToList();

                // Get unique transactions
                var uniqueTransactions = dbTransactions
                    .GroupBy(t => t.TransactionId)
                    .Select(g => g.OrderBy(t => t.TransactionId).FirstOrDefault())
                    .ToList();

                // Search for duplicate transactions and save them into a file
                var duplicateTransactions = await GetDuplicateTransactionsAsync(dbTransactions);
                await SaveDuplicateTransactionsToFileAsync(duplicateTransactions);

                _logger.LogInformation("Processing {Count} unique transactions.", uniqueTransactions.Count);

                // Split transactions into batches
                var transactionBatches = SplitTransactionsIntoBatches(uniqueTransactions, batchSize);

                // Insert transaction batches asynchronously
                await InsertTransactionBatchesAsync(transactionBatches, batchSize);

                _logger.LogInformation("Transaction processing completed. {Count} transactions inserted.", uniqueTransactions.Count);
            }
            catch (Exception ex) when (ex is AutoMapperMappingException or AutoMapperConfigurationException)
            {
                throw MappingException.FromAutoMapperException(ex);
            }
            catch (IOException ex)
            {
                throw new FileProcessingException("An error occurred while writing to the file.", "duplicate_transactions.txt", ex);
            }
            catch (SqlException ex)
            {
                throw new DatabaseException("An error occurred while executing a SQL Server operation.", ex);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "An unexpected error occurred during transaction processing.");
                throw new TransactionProcessingException("An unexpected error occurred while processing transactions.", ex);
            }
        }

        /// <summary>
        /// Performs an analysis on financial transactions, retrieving data from the database, 
        /// mapping entities, and calculating user summaries, top spending categories, and 
        /// the highest spender using parallel processing.
        /// </summary>
        /// <returns>
        /// A dynamic object containing the calculated user summaries, top categories, 
        /// and the highest spender.
        /// </returns>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an unexpected error occurs during transaction processing.
        /// </exception>
        /// <exception cref="MappingException">
        /// Thrown if an error occurs while mapping database entities to domain entities.
        /// </exception>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs while retrieving transactions from the database.
        /// </exception>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs during transaction calculations.
        /// </exception>
        public async Task<dynamic> PerformAnalysisAsync()
        {
            _logger.LogInformation("Starting transaction analysis...");

            var transaction = new Transaction();

            try
            {
                // Get transactions from the database
                IEnumerable<Infrastructure.DbEntities.Transaction> dbTransactions;
                try
                {
                    dbTransactions = await _repository.GetAllAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving transactions from the database.");
                    throw new TransactionProcessingException("Failed to retrieve transactions from the database.", ex);
                }

                // Maps transactions
                IEnumerable<Domain.Entities.Transaction> transactions;
                try
                {
                    transactions = _mapper.Map<IEnumerable<Domain.Entities.Transaction>>(dbTransactions);
                }
                catch (Exception ex) when (ex is AutoMapperMappingException or AutoMapperConfigurationException)
                {
                    throw MappingException.FromAutoMapperException(ex);
                }

                // Uses PLINQ to process calculations in parallel (CPU-bound)
                Task<IEnumerable<dynamic>> usersSummaryTask;
                Task<IEnumerable<dynamic>> topCategoriesTask;
                Task<dynamic> highestSpenderTask;

                try
                {
                    usersSummaryTask = Task.Run(() => transaction.CalculateUserSummaries(transactions.AsParallel()));
                    topCategoriesTask = Task.Run(() => transaction.CalculateTopCategories(transactions.AsParallel()));
                    highestSpenderTask = Task.Run(() => transaction.FindHighestSpender(transactions.AsParallel()));

                    await Task.WhenAll(usersSummaryTask, topCategoriesTask, highestSpenderTask);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while performing transaction calculations.");
                    throw new TransactionProcessingException("An error occurred while calculating transaction analytics.", ex);
                }

                // Get the results
                var usersSummary = await usersSummaryTask;
                var topCategories = await topCategoriesTask;
                var highestSpender = await highestSpenderTask;

                _logger.LogInformation("Transaction analysis completed successfully.");

                return new
                {
                    UsersSummary = usersSummary,
                    TopCategories = topCategories,
                    HighestSpender = highestSpender
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during transaction analysis.");
                throw new TransactionProcessingException("An unexpected error occurred during transaction analysis.", ex);
            }
        }

        /// <summary>
        /// Saves a report to a specified file in JSON format. Handles serialization, file existence checks, 
        /// and asynchronous writing while ensuring proper error handling.
        /// </summary>
        /// <param name="report">The dynamic report object to be serialized and saved.</param>
        /// <param name="outputPath">The file path where the report should be saved.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="ReportSerializationException">
        /// Thrown if an error occurs during the serialization of the report.
        /// </exception>
        /// <exception cref="FileProcessingException">
        /// Thrown if an error occurs while checking, deleting, or writing to the output file.
        /// </exception>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an unexpected error occurs during the report-saving process.
        /// </exception>
        public async Task SaveReportAsync(dynamic report, string outputPath)
        {
            _logger.LogInformation("Saving report to {OutputPath}...", outputPath);

            try
            {
                // Serialize the report
                string json;
                try
                {
                    _logger.LogInformation("Serializing the report...");
                    json = JsonConvert.SerializeObject(report, Formatting.Indented);
                }
                catch (JsonException ex) // Captura qualquer erro de serialização JSON
                {
                    _logger.LogError(ex, "Error occurred while serializing the report.");
                    throw ReportSerializationException.FromJsonException(ex);
                }
                // Check and delete the file if necessary
                try
                {
                    if (File.Exists(outputPath))
                    {
                        _logger.LogWarning("File already exists at {OutputPath}. Overwriting the file.", outputPath);
                        File.Delete(outputPath);
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Error occurred while checking/deleting the file at {OutputPath}.", outputPath);
                    throw new FileProcessingException("Failed to check or delete the existing file.", outputPath, ex);
                }

                // Write the file asynchronously
                try
                {
                    await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "I/O error occurred while writing the report file.");
                    throw new FileProcessingException("An I/O error occurred while writing the report file.", outputPath, ex);
                }

                _logger.LogInformation("Report saved successfully to {OutputPath}.", outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while saving the report.");
                throw new TransactionProcessingException("An unexpected error occurred while saving the report.", ex);
            }
        }

        /// <summary>
        /// Deletes all transactions from the database. Retrieves transaction IDs and removes them in bulk.
        /// </summary>
        /// <returns>A task representing the asynchronous purge operation.</returns>
        /// <exception cref="TransactionProcessingException">
        /// Thrown if an error occurs while retrieving transaction IDs or deleting transactions.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown if an unexpected error occurs during transaction deletion.
        /// </exception>
        public async Task PurgeAllTransactionsAsync()
        {
            _logger.LogInformation("Initiating the deletion of all transactions...");

            try
            {
                _logger.BeginScope("Method: PurgeAllTransactionsAsync");

                IEnumerable<Guid> transactionIds;
                try
                {
                    transactionIds = await _repository.GetAllEntityIdsAsync(t => t.TransactionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving transaction IDs from the database.");
                    throw new TransactionProcessingException("Failed to retrieve transaction IDs.", ex);
                }

                int totalDeleted;
                try
                {
                    totalDeleted = await _repository.DeleteTransactionsByIdsAsync(transactionIds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while deleting transactions.");
                    throw new TransactionProcessingException("An error occurred while deleting transactions from the database.", ex);
                }

                _logger.LogInformation("Total transactions deleted: {TotalDeleted}", totalDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting all transactions.");
                throw new Exception("An unexpected error occurred while purging all transactions.", ex);
            }
        }


        #region Private Methods 

        /// <summary>
        /// Identifies and retrieves duplicate transactions from the provided list of transactions.
        /// </summary>
        /// <param name="dbTransactions">The collection of database transactions to check for duplicates.</param>
        /// <returns>A task that returns a list of duplicate transactions.</returns>
        private async Task<List<Infrastructure.DbEntities.Transaction>> GetDuplicateTransactionsAsync(IEnumerable<Infrastructure.DbEntities.Transaction> dbTransactions)
        {
            return await Task.Run(() => dbTransactions
                .GroupBy(t => t.TransactionId)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.OrderBy(t => t.TransactionId).Skip(1))
                .ToList());
        }

        /// <summary>
        /// Saves duplicate transactions to a log file for tracking and analysis.
        /// </summary>
        /// <param name="duplicateTransactions">The collection of duplicate transactions to be saved.</param>
        /// <returns>A task representing the asynchronous file write operation.</returns>
        /// <exception cref="FileProcessingException">
        /// Thrown if an error occurs while creating the directory, opening the file, or writing to the file.
        /// </exception>
        private async Task SaveDuplicateTransactionsToFileAsync(IEnumerable<Infrastructure.DbEntities.Transaction> duplicateTransactions)
        {
            if (!duplicateTransactions.Any()) return;

            var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ErrorsInTheProcessing");

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating directory: {DirectoryPath}", directoryPath);
                throw new FileProcessingException("Failed to create the directory for saving duplicate transactions.", directoryPath, ex);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var duplicateFilePath = Path.Combine(directoryPath, $"duplicate_transactions_{timestamp}.txt");

            try
            {
                using (var writer = new StreamWriter(duplicateFilePath, false))
                {
                    foreach (var t in duplicateTransactions)
                    {
                        var logEntry = $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}, " +
                                       $"TransactionId: {t.TransactionId}, Details: {JsonConvert.SerializeObject(t)}";

                        await writer.WriteLineAsync(logEntry);
                    }
                }

                _logger.LogWarning("Duplicate transactions logged to {FilePath}.", duplicateFilePath);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error occurred while writing duplicate transactions to file: {FilePath}", duplicateFilePath);
                throw new FileProcessingException("An I/O error occurred while writing duplicate transactions to a file.", duplicateFilePath, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while writing duplicate transactions to file: {FilePath}", duplicateFilePath);
                throw new FileProcessingException("An unexpected error occurred while writing duplicate transactions to a file.", duplicateFilePath, ex);
            }
        }
        /// <summary>
        /// Splits a list of unique transactions into batches of a specified size.
        /// </summary>
        /// <param name="uniqueTransactions">The collection of transactions to be batched.</param>
        /// <param name="batchSize">The maximum number of transactions per batch.</param>
        /// <returns>A list containing batches of transactions.</returns>
        private List<List<Infrastructure.DbEntities.Transaction>> SplitTransactionsIntoBatches(IEnumerable<Infrastructure.DbEntities.Transaction> uniqueTransactions, int batchSize)
        {
            return uniqueTransactions
                .Select((transaction, index) => new { transaction, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.transaction).ToList())
                .ToList();
        }

        /// <summary>
        /// Inserts batches of transactions into the database asynchronously.
        /// </summary>
        /// <param name="transactionBatches">The collection of transaction batches to insert.</param>
        /// <param name="batchSize">The batch size used for insertion.</param>
        /// <returns>A task representing the asynchronous database insert operation.</returns>
        private async Task InsertTransactionBatchesAsync(IEnumerable<List<Infrastructure.DbEntities.Transaction>> transactionBatches, int batchSize)
        {
            var batchInsertTasks = transactionBatches.Select(async batch =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IRepository<Infrastructure.DbEntities.Transaction>>();

                    await repository.BulkInsertAsync(batch, batchSize, options =>
                    {
                        options.SetOutputIdentity = true;
                        options.BatchSize = batchSize;
                    });
                }
            });

            await Task.WhenAll(batchInsertTasks);
        }

        #endregion Private Methods

    }
}
