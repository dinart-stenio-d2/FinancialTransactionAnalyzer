using CsvHelper;
using CsvHelper.Configuration;
using FinancialAnalyticsProcessor.Domain.Entities;
using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinancialAnalyticsProcessor.Infrastructure.Services
{
    public class CsvTransactionLoader : ICsvTransactionLoader
    {
        private readonly ILogger<CsvTransactionLoader> _logger;

        public CsvTransactionLoader(ILogger<CsvTransactionLoader> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<Transaction>> LoadTransactionsAsync(Stream csvStream)
        {
            if (csvStream == null || !csvStream.CanRead)
            {
                _logger.LogError("The provided CSV stream is null or not readable.");
                throw new ArgumentException("Invalid CSV stream provided.", nameof(csvStream));
            }

            try
            {
                _logger.LogInformation("Starting to load transactions from the CSV stream.");

                using var reader = new StreamReader(csvStream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                });

                var records = new List<Transaction>();

                await foreach (var record in csv.GetRecordsAsync<Transaction>().ConfigureAwait(false))
                {
                    records.Add(record);
                }

                _logger.LogInformation($"Successfully loaded {records.Count} transactions from the CSV.");

                return records;
            }
            catch (CsvHelperException ex)
            {
                _logger.LogError(ex, "A CSV parsing error occurred while loading transactions.");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "An I/O error occurred while reading the CSV stream.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while loading transactions from the CSV stream.");
                throw;
            }
        }

        public async Task RecreateTransactionAsync(string csvFilePath, Guid transactionId, string newDescription)
        {
            _logger.LogInformation("Starting to recreate transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);
            var tempFilePath = $"{csvFilePath}.tmp";

            try
            {
                using (var reader = new StreamReader(csvFilePath))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                }))
                using (var writer = new StreamWriter(tempFilePath))
                using (var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                }))
                {
                    // Register the custom ClassMap
                    csv.Context.RegisterClassMap<TransactionMap>();

                    var transactions = csv.GetRecords<Transaction>().ToList();
                    var transactionToRecreate = transactions.FirstOrDefault(t => t.TransactionId == transactionId);

                    if (transactionToRecreate == null)
                    {
                        _logger.LogWarning("Transaction with ID {TransactionId} not found in file {CsvFilePath}.", transactionId, csvFilePath);
                        throw new ArgumentException($"Transaction with ID {transactionId} not found in the file.");
                    }

                    _logger.LogInformation("Recreating transaction with ID {TransactionId}.", transactionId);

                    // Recreate the transaction with the new description
                    var recreatedTransaction = new Transaction
                    {
                        TransactionId = transactionId,
                        UserId = transactionToRecreate.UserId,
                        Date = transactionToRecreate.Date,
                        Amount = transactionToRecreate.Amount,
                        Category = transactionToRecreate.Category,
                        Description = newDescription,
                        Merchant = transactionToRecreate.Merchant
                    };

                    // Register the ClassMap for the writer
                    csvWriter.Context.RegisterClassMap<TransactionMap>();

                    // Write all transactions to the new file
                    csvWriter.WriteHeader<Transaction>();
                    await csvWriter.NextRecordAsync();

                    var processedTransactions = await Task.WhenAll(transactions.Select(transaction =>
                    {
                        return Task.Run(() =>
                        {
                            if (transaction.TransactionId == transactionId)
                            {
                                return recreatedTransaction;
                            }
                            return transaction;
                        });
                    }));

                    foreach (var transaction in processedTransactions)
                    {
                        csvWriter.WriteRecord(transaction);
                        await csvWriter.NextRecordAsync();
                    }
                }

                // Replace the original file with the temporary file
                File.Delete(csvFilePath);
                File.Move(tempFilePath, csvFilePath);
                _logger.LogInformation("Successfully recreated transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while recreating transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);
                throw;
            }
        }
    }
}