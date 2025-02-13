using FinancialAnalyticsProcessor.Domain.Interfaces;
using FluentValidation;
using FluentValidation.Results;
using Polly;
using Polly.Retry;

namespace FinancialAnalyticsProcessor.FaultResiliencePolicies
{
    /// <summary>
    /// Provides a set of Polly policies for handling exceptions and retries in the application.
    /// </summary>
    public static class PollyPolicy
    {

        /// <summary>
        /// Creates a retry policy that handles validation exceptions and attempts to recreate transactions 
        /// when specific validation errors occur.
        /// </summary>
        /// <param name="csvTransactionLoader">The transaction loader responsible for recreating transactions.</param>
        /// <param name="logger">The logger instance used for logging retry attempts and errors.</param>
        /// <returns>
        /// An asynchronous retry policy that retries indefinitely when a <see cref="ValidationException"/> occurs.
        /// </returns>
        /// <remarks>
        /// This policy will only retry if a validation error message indicates that a transaction's description 
        /// is missing or exceeds the character limit. If a valid transaction ID is found in the error message, 
        /// the transaction is attempted to be recreated before continuing.
        /// </remarks>
        public static AsyncRetryPolicy CreateRetryPolicy(ICsvTransactionLoader csvTransactionLoader, ILogger logger)
        {
            return Policy
                .Handle<ValidationException>() // Handles only ValidationException
                .RetryForeverAsync(async (exception, context) =>
                {
                    logger.LogWarning($"Retry encountered a validation error: {exception.Message}. Retrying immediately...");

                    // Retrieve CSV file path from context
                    var csvFilePath = context.ContainsKey("csvFilePath") ? context["csvFilePath"] as string : null;
                    var newDescription = "New Description added after failure"; // Default new description

                    if (exception is ValidationException validationException)
                    {
                        bool isConditionMet = validationException.Errors
                            .Any(error => IsRetryConditionMet(error, logger));

                        if (!isConditionMet)
                        {
                            logger.LogWarning("No matching condition for retry was found. Exiting retry loop.");
                            return; // Exit retry loop if no condition is met
                        }

                        foreach (var error in validationException.Errors)
                        {
                            if (!IsRetryConditionMet(error, logger)) continue;

                            var transactionId = ExtractTransactionIdFromError(error, logger);
                            if (transactionId == null) continue;

                            logger.LogWarning($"Validation failed for transaction ID: {transactionId}. Attempting to recreate...");

                            await TryRecreateTransaction(csvTransactionLoader, csvFilePath, transactionId.Value, newDescription, logger);
                        }
                    }
                });
        }

        /// <summary>
        /// Checks if the validation error message matches a retry condition.
        /// </summary>
        /// <param name="error">Validation error to evaluate.</param>
        /// <param name="logger">Logger instance for logging warnings.</param>
        /// <returns>True if retry conditions are met, otherwise false.</returns>
        private static bool IsRetryConditionMet(ValidationFailure error, ILogger logger)
        {
            return error.ErrorMessage.Contains("Description cannot be null or empty") ||
                   error.ErrorMessage.Contains("Description must not exceed 255 characters");
        }
        /// <summary>
        /// Extracts the TransactionId from a validation error message.
        /// </summary>
        /// <param name="error">Validation error containing the TransactionId.</param>
        /// <param name="logger">Logger instance for logging warnings.</param>
        /// <returns>The extracted TransactionId if valid, otherwise null.</returns>
        private static Guid? ExtractTransactionIdFromError(ValidationFailure error, ILogger logger)
        {
            if (!error.ErrorMessage.Contains("Transaction ID: |")) return null;

            try
            {
                int startIndex = error.ErrorMessage.IndexOf("|") + 1;
                int endIndex = error.ErrorMessage.LastIndexOf("|");
                string transactionIdString = error.ErrorMessage.Substring(startIndex, endIndex - startIndex);

                if (Guid.TryParse(transactionIdString, out var transactionId))
                {
                    return transactionId;
                }

                logger.LogWarning("Invalid Transaction ID format in the validation error message.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while extracting Transaction ID from validation error message.");
            }

            return null;
        }

        /// <summary>
        /// Attempts to recreate a transaction in the CSV file.
        /// </summary>
        /// <param name="csvTransactionLoader">The transaction loader responsible for recreating transactions.</param>
        /// <param name="csvFilePath">Path to the CSV file where the transaction should be recreated.</param>
        /// <param name="transactionId">The ID of the transaction to be recreated.</param>
        /// <param name="newDescription">New description for the recreated transaction.</param>
        /// <param name="logger">Logger instance for logging actions and errors.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task TryRecreateTransaction(ICsvTransactionLoader csvTransactionLoader, string csvFilePath, Guid transactionId, string newDescription, ILogger logger)
        {
            try
            {
                await csvTransactionLoader.RecreateTransactionAsync(csvFilePath, transactionId, newDescription);
                logger.LogInformation($"Transaction with ID {transactionId} was recreated successfully in the CSV file.");
                logger.LogInformation($"Continuing processing for transaction ID: {transactionId} after successful recreation.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to recreate transaction during retry for ID {transactionId}.");
            }
        }

    }
}