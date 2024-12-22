using FinancialAnalyticsProcessor.Domain.Interfaces;
using FluentValidation;
using Polly;
using Polly.Retry;

namespace FinancialAnalyticsProcessor.FaultResiliencePolicies
{
    public static class PollyPolicy
    {
        public static AsyncRetryPolicy CreateRetryPolicy(ICsvTransactionLoader csvTransactionLoader, ILogger logger)
        {
            return Policy
                .Handle<ValidationException>() // Handles only ValidationException
                .RetryForeverAsync(async (exception, context) =>
                {
                    // Log the retry attempt and exception message
                    logger.LogWarning($"Retry encountered a validation error: {exception.Message}. Retrying immediately...");

                    // Retrieve parameters from the execution context
                    var csvFilePath = context.ContainsKey("csvFilePath") ? context["csvFilePath"] as string : null;
                    var newDescription = "New Description added after failure"; // Example description

                    if (exception is ValidationException validationException)
                    {
                        var isConditionMet = false; // Flag to track if retry condition is met

                        foreach (var error in validationException.Errors)
                        {
                            // Apply logic only if the error message matches the validation rule for Description
                            if (error.ErrorMessage.Contains("Description cannot be null or empty") ||
                                error.ErrorMessage.Contains("Description must not exceed 255 characters"))
                            {
                                isConditionMet = true;

                                // Check if the error message contains "Transaction ID: |"
                                if (error.ErrorMessage.Contains("Transaction ID: |"))
                                {
                                    // Extract the TransactionId from the error message
                                    var startIndex = error.ErrorMessage.IndexOf("|") + 1;
                                    var endIndex = error.ErrorMessage.LastIndexOf("|");
                                    var transactionIdString = error.ErrorMessage.Substring(startIndex, endIndex - startIndex);

                                    if (Guid.TryParse(transactionIdString, out var transactionId))
                                    {
                                        logger.LogWarning($"Validation failed for transaction ID: {transactionId}. Attempting to recreate...");

                                        // Call the method to recreate the transaction
                                        try
                                        {
                                            await csvTransactionLoader.RecreateTransactionAsync(csvFilePath, transactionId, newDescription);
                                            logger.LogInformation($"Transaction with ID {transactionId} was recreated successfully in the CSV file.");

                                            // Continue processing after successful recreation
                                            logger.LogInformation($"Continuing processing for transaction ID: {transactionId} after successful recreation.");
                                            return; // Exit the retry context and continue execution
                                        }
                                        catch (Exception ex)
                                        {
                                            logger.LogError(ex, $"Failed to recreate transaction during retry for ID {transactionId}.");
                                        }
                                    }
                                    else
                                    {
                                        logger.LogWarning("Invalid Transaction ID format in the validation error message.");
                                    }
                                }
                            }
                        }

                        // Exit retry loop if no condition is met
                        if (!isConditionMet)
                        {
                            logger.LogWarning("No matching condition for retry was found. Exiting retry loop.");
                            return; // Break out of the retry loop
                        }
                    }
                });
        }
    }
}
