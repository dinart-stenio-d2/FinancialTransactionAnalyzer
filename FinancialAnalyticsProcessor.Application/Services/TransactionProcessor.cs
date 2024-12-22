using AutoMapper;
using FinancialAnalyticsProcessor.Domain.Entities;
using FinancialAnalyticsProcessor.Domain.Interfaces.ApplicationServices;
using FinancialAnalyticsProcessor.Domain.Interfaces.Repositories;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace FinancialAnalyticsProcessor.Application.Services
{
    public class TransactionProcessor : ITransactionProcessor
    {
        private readonly IRepository<Infrastructure.DbEntities.Transaction> _repository;
        private readonly ILogger<TransactionProcessor> _logger;
        private readonly IMapper _mapper;
        private readonly IValidator<Transaction> _transactionValidator;
        private readonly IValidator<IEnumerable<Transaction>> _transactionListValidator;
        private readonly IServiceScopeFactory _scopeFactory;


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

        public async Task ProcessTransactionsAsync(IEnumerable<Transaction> transactions)
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

            //Once the business rules have been validated the list is mapped to a list represented by the database entity: Infrastructure.DbEntities.Transaction
            var dbTransactions = transactions
                .AsParallel() // To process in parallel if necessary
                .Select(transaction => _mapper.Map<Infrastructure.DbEntities.Transaction>(transaction))
                .ToList();

      
            // Ensure unique transactions based on TransactionId
            var groupedTransactions = await Task.Run(() =>
                                                 dbTransactions.GroupBy(t => t.TransactionId).ToList())
                                                .ConfigureAwait(false);

            // Select single transactions
            var uniqueTransactions = await Task.Run(() =>
                                                groupedTransactions.Select(g => g.First())
                                               .ToList())
                                               .ConfigureAwait(false);

            // Identify duplicate transactions
            var duplicateTransactions = await Task.Run(() =>
                                                     groupedTransactions
                                                        .Where(g => g.Count() > 1)
                                                        .SelectMany(g => g.Skip(1)) // All duplicates except the first one
                                                        .ToList())
                                                        .ConfigureAwait(false);

            // Write duplicate transactions to a text file
            if (duplicateTransactions.Any())
            {
                // Directory to save the file
                var directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ErrorsInTheProcessing");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // File name with timestamp
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var duplicateFilePath = Path.Combine(directoryPath, $"duplicate_transactions_{timestamp}.txt");

                // Create a log of duplicates with additional information
                var duplicateLog = await Task.Run(() =>
                                              duplicateTransactions
                                                 .Select(t =>
                                                 {
                                                     var threadId = Thread.CurrentThread.ManagedThreadId;
                                                     var taskId = Task.CurrentId.HasValue ? Task.CurrentId.Value : -1;
                                                     return $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}, " +
                                                            $"ThreadId: {threadId}, TaskId: {taskId}, " +
                                                            $"TransactionId: {t.TransactionId}, Details: {JsonConvert.SerializeObject(t)}";
                                                 }).ToList())
                                                .ConfigureAwait(false);

                // Write to file
                await File.WriteAllLinesAsync(duplicateFilePath, duplicateLog)
                          .ConfigureAwait(false);
                
                _logger.LogWarning("Duplicate transactions logged to {FilePath}.", duplicateFilePath);
            }

            _logger.LogInformation("Processing {Count} unique transactions.", uniqueTransactions.Count);

            // Lot size for insertion
            const int batchSize = 10000;

            //Split single transactions into batches
            var transactionBatches = uniqueTransactions
                .Select((transaction, index) => new { transaction, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.transaction).ToList())
                .ToList();

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


            //Wait for all tasks to complete
            await Task.WhenAll(batchInsertTasks).ConfigureAwait(false);
            _logger.LogInformation("Transaction processing completed. {Count} transactions inserted.", uniqueTransactions.Count);
        }

        public async Task<dynamic> PerformAnalysisAsync()
        {
            _logger.LogInformation("Starting transaction analysis...");

            var transaction = new Transaction();
            try
            {
                
                var dbTransactions = await _repository.GetAllAsync();

            
                var transactions = await Task.Run(() => _mapper.Map<IEnumerable<Domain.Entities.Transaction>>(dbTransactions)).ConfigureAwait(false);

                var usersSummaryTask = Task.Run(() => transaction.CalculateUserSummaries(transactions));
                var topCategoriesTask = Task.Run(() => transaction.CalculateTopCategories(transactions));
                var highestSpenderTask = Task.Run(() => transaction.FindHighestSpender(transactions));

                await Task.WhenAll(usersSummaryTask, topCategoriesTask, highestSpenderTask);


                var usersSummary = await usersSummaryTask.ConfigureAwait(false);
                var topCategories = await topCategoriesTask.ConfigureAwait(false);
                var highestSpender = await highestSpenderTask.ConfigureAwait(false);

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
                _logger.LogError(ex, "An error occurred during transaction analysis.");
                throw;
            }
        }

        public async Task SaveReportAsync(dynamic report, string outputPath)
        {
            _logger.LogInformation("Saving report to {OutputPath}...", outputPath);

            try
            {
               
                var serializeTask = Task.Run(() =>
                {
                    _logger.LogInformation("Serializing the report...");
                    return JsonConvert.SerializeObject(report, Formatting.Indented);
                });

             
                var fileCheckTask = Task.Run(() =>
                {
                    if (File.Exists(outputPath))
                    {
                        _logger.LogWarning("File already exists at {OutputPath}. Overwriting the file.", outputPath);
                        File.Delete(outputPath);
                    }
                });

                await Task.WhenAll(serializeTask, fileCheckTask).ConfigureAwait(false);

                var json = await serializeTask.ConfigureAwait(false);
                await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);

                _logger.LogInformation("Report saved successfully to {OutputPath}.", outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving the report.");
                throw;
            }
        }

        public async Task PurgeAllTransactionsAsync()
        {
            _logger.LogInformation("Initiating the deletion of all transactions...");

            try
            {
                _logger.BeginScope("Method: DeleteAllTransactionsAsync");

                var transactionIds = await _repository.GetAllEntityIdsAsync(t => t.TransactionId);
                var totalDeleted = await _repository.DeleteTransactionsByIdsAsync(transactionIds);

                _logger.LogInformation("Total transactions deleted: {TotalDeleted}", totalDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting all transactions.");
                throw; // Re-throw the exception to propagate it up the call stack
            }
        }

    }
}
