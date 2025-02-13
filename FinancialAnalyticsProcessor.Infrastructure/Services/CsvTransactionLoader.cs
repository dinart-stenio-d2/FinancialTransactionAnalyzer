using CsvHelper;
using CsvHelper.Configuration;
using FinancialAnalyticsProcessor.Domain.Entities;
using FinancialAnalyticsProcessor.Domain.Interfaces;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using FinancialAnalyticsProcessor.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinancialAnalyticsProcessor.Infrastructure.Services
{
    /// <summary>
    /// Loads transactions from a CSV stream and provides methods for recreating transactions in a CSV file.
    /// </summary>
    public class CsvTransactionLoader : ICsvTransactionLoader
    {
        /// <summary>
        /// Logger instance for recording informational messages, warnings, and errors.
        /// </summary>
        private readonly ILogger<CsvTransactionLoader> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CsvTransactionLoader"/> class.
        /// </summary>
        /// <param name="logger">The logger instance used for logging messages and errors.</param>
        public CsvTransactionLoader(ILogger<CsvTransactionLoader> logger)
        {
            _logger = logger;
        }

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
        /// <exception cref="Exception">
        /// Thrown if an unexpected error occurs during the transaction loading process.
        /// </exception>
        /// <remarks>
        /// This method uses <see cref="CsvHelper"/> to parse the CSV stream and extract transactions.
        /// </remarks>
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
        public async Task RecreateTransactionAsync(string csvFilePath, Guid transactionId, string newDescription)
        {
            _logger.LogInformation("Starting to recreate transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);

            var tempFilePath = $"{csvFilePath}.tmp";

            try
            {
                var transactions = await ReadTransactionsFromCsvAsync(csvFilePath);
                var transactionToRecreate = transactions.FirstOrDefault(t => t.TransactionId == transactionId);

                if (transactionToRecreate == null)
                {
                    _logger.LogWarning("Transaction with ID {TransactionId} not found in file {CsvFilePath}.", transactionId, csvFilePath);
                    throw new TransactionNotFoundException(transactionId, csvFilePath);
                }

                _logger.LogInformation("Recreating transaction with ID {TransactionId}.", transactionId);

                var recreatedTransaction = CreateUpdatedTransaction(transactionToRecreate, newDescription);

                await WriteTransactionsToCsvAsync(tempFilePath, transactions, transactionId, recreatedTransaction);
                await ReplaceOriginalFileAsync(csvFilePath, tempFilePath);

                _logger.LogInformation("Successfully recreated transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while recreating transaction with ID {TransactionId} in file {CsvFilePath}.", transactionId, csvFilePath);
                throw new CsvProcessingException($"An error occurred while processing the CSV file: {csvFilePath}.", ex);
            }
        }

        #region Private Methods 

        /// <summary>
        /// Reads transactions from a CSV file asynchronously.
        /// </summary>
        /// <param name="csvFilePath">The path of the CSV file.</param>
        /// <returns>A list of transactions read from the file.</returns>
        private async Task<List<Transaction>> ReadTransactionsFromCsvAsync(string csvFilePath)
        {
            var transactions = new List<Transaction>();

            try
            {
                using var reader = new StreamReader(csvFilePath);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                });

                csv.Context.RegisterClassMap<TransactionMap>();

                await foreach (var transaction in csv.GetRecordsAsync<Transaction>().ConfigureAwait(false))
                {
                    transactions.Add(transaction);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading transactions from CSV file: {CsvFilePath}.", csvFilePath);
                throw new CsvProcessingException("Failed to read transactions from the CSV file.", ex);
            }

            return transactions;
        }
        /// <summary>
        /// Writes transactions to a new CSV file, replacing a specific transaction with an updated version.
        /// </summary>
        /// <param name="filePath">The path of the new CSV file.</param>
        /// <param name="transactions">The list of transactions.</param>
        /// <param name="transactionId">The ID of the transaction to update.</param>
        /// <param name="updatedTransaction">The updated transaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WriteTransactionsToCsvAsync(string filePath, List<Transaction> transactions, Guid transactionId, Transaction updatedTransaction)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                using var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    Delimiter = ","
                });

                csvWriter.Context.RegisterClassMap<TransactionMap>();

                await csvWriter.WriteRecordsAsync(transactions.Select(t =>
                    t.TransactionId == transactionId ? updatedTransaction : t));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing transactions to new CSV file: {FilePath}.", filePath);
                throw new CsvProcessingException("Failed to write transactions to the new CSV file.", ex);
            }
        }

        /// <summary>
        /// Replaces the original CSV file with the newly updated file.
        /// </summary>
        /// <param name="originalFilePath">The path of the original file.</param>
        /// <param name="tempFilePath">The path of the temporary file.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ReplaceOriginalFileAsync(string originalFilePath, string tempFilePath)
        {
            try
            {
                await Task.Run(() =>
                {
                    File.Delete(originalFilePath);
                    File.Move(tempFilePath, originalFilePath);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replacing original CSV file with updated version.");
                throw new CsvProcessingException("Failed to replace the original CSV file with the updated version.", ex);
            }
        }

        /// <summary>
        /// Creates an updated transaction with a new description.
        /// </summary>
        /// <param name="originalTransaction">The original transaction.</param>
        /// <param name="newDescription">The new description.</param>
        /// <returns>A new transaction with the updated description.</returns>
        private Transaction CreateUpdatedTransaction(Transaction originalTransaction, string newDescription)
        {
            return new Transaction
            {
                TransactionId = originalTransaction.TransactionId,
                UserId = originalTransaction.UserId,
                Date = originalTransaction.Date,
                Amount = originalTransaction.Amount,
                Category = originalTransaction.Category,
                Description = newDescription,
                Merchant = originalTransaction.Merchant
            };
        }
        #endregion Private Methods
    }
}