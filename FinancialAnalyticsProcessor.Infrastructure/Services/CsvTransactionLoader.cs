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
                    csv.Context.RegisterClassMap<TransactionMap>();

                    var transactionToRecreate = default(Transaction);
                    var transactions = new List<Transaction>();

                    await foreach (var transaction in csv.GetRecordsAsync<Transaction>())
                    {
                        if (transaction.TransactionId == transactionId)
                        {
                            transactionToRecreate = transaction;
                        }
                        transactions.Add(transaction);
                    }

                    if (transactionToRecreate == null)
                    {
                        _logger.LogWarning("Transaction with ID {TransactionId} not found in file {CsvFilePath}.", transactionId, csvFilePath);
                        throw new ArgumentException($"Transaction with ID {transactionId} not found in the file.");
                    }

                    _logger.LogInformation("Recreating transaction with ID {TransactionId}.", transactionId);

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

                    csvWriter.Context.RegisterClassMap<TransactionMap>();

                    await csvWriter.WriteRecordsAsync(transactions.Select(t =>
                        t.TransactionId == transactionId ? recreatedTransaction : t));
                }

                // Replace the original file with the temporary one asynchronously
                await Task.Run(() => File.Delete(csvFilePath));
                await Task.Run(() => File.Move(tempFilePath, csvFilePath));

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