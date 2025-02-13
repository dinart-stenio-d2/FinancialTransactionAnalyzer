using FinancialAnalyticsProcessor.Domain.Entities;
using FluentValidation;

namespace FinancialAnalyticsProcessor.Domain.Validations
{
    /// <summary>
    /// Validates a <see cref="Transaction"/> object to ensure it meets defined business rules.
    /// </summary>
    public class TransactionValidator : AbstractValidator<Transaction>
    {
        /// <summary>
        /// Lock object for controlling concurrent access to the error log file.
        /// </summary>
        private static readonly object _fileLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionValidator"/> class.
        /// Defines validation rules for transaction properties.
        /// </summary>
        public TransactionValidator()
        {
            // Rule to validate TransactionId
            RuleFor(t => t.TransactionId)
                .NotEmpty().WithMessage("TransactionId is required.");

            // Rule to validate UserId
            RuleFor(t => t.UserId)
                .NotEmpty().WithMessage("UserId is required.");

            // Rule to validate Date
            RuleFor(t => t.Date)
                .NotEmpty().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Date cannot be in the future.");

            // Rule to validate Category
            RuleFor(t => t.Category)
                .NotEmpty().WithMessage("Category is required.")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");

            // Rule to validate Description
            RuleFor(t => t.Description)
                .NotEmpty()
                .WithMessage(t => $"Description cannot be null or empty. Transaction ID: |{t.TransactionId}|")
                .MaximumLength(255)
                .WithMessage(t => $"Description must not exceed 255 characters. Transaction ID: |{t.TransactionId}|");

            // Rule to validate Merchant
            RuleFor(t => t.Merchant)
                .NotEmpty().WithMessage("Merchant is required.")
                .MaximumLength(100).WithMessage("Merchant must not exceed 100 characters.");

            // Rule to validate the entire transaction object and log validation failures
            RuleFor(t => t)
                .Custom((transaction, context) =>
                {
                    var errors = new List<string>();

                    if (string.IsNullOrEmpty(transaction.TransactionId.ToString()))
                        errors.Add("TransactionId is required.");
                    if (string.IsNullOrEmpty(transaction.UserId.ToString()))
                        errors.Add("UserId is required.");
                    if (transaction.Date == default || transaction.Date > DateTime.UtcNow)
                        errors.Add("Date is required and cannot be in the future.");
                    if (string.IsNullOrEmpty(transaction.Category) || transaction.Category.Length > 100)
                        errors.Add("Category is required and must not exceed 100 characters.");
                    if (string.IsNullOrEmpty(transaction.Description) || transaction.Description.Length > 255)
                        errors.Add("Description is required and must not exceed 255 characters.");
                    if (string.IsNullOrEmpty(transaction.Merchant) || transaction.Merchant.Length > 100)
                        errors.Add("Merchant is required and must not exceed 100 characters.");

                    if (errors.Any())
                    {
                        var transactionDetails =
                            $"Transaction Details: TransactionId: {transaction.TransactionId}, " +
                            $"UserId: {transaction.UserId}, " +
                            $"Date: {transaction.Date}, " +
                            $"Amount: {transaction.Amount}, " +
                            $"Category: {transaction.Category}, " +
                            $"Description: {transaction.Description}, " +
                            $"Merchant: {transaction.Merchant}";

                        var errorMessage = string.Join("; ", errors);

                        context.AddFailure(new FluentValidation.Results.ValidationFailure(
                            nameof(Transaction),
                            $"Validation failed for transaction. Errors: {errorMessage}. {transactionDetails}"
                        ));

                        SaveValidationErrorsToFile(transaction, errors);
                    }
                });
        }

        /// <summary>
        /// Saves validation errors to a log file for auditing purposes.
        /// </summary>
        /// <param name="transaction">The transaction that failed validation.</param>
        /// <param name="errors">The list of validation errors.</param>
        private void SaveValidationErrorsToFile(Transaction transaction, List<string> errors)
        {
            var errorDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data", "ErrorsInTheProcessing");
            Directory.CreateDirectory(errorDirectory);

            var errorFilePath = Path.Combine(errorDirectory, "ErrorsInTheProcessing.txt");

            var transactionDetails =
                $"Transaction Details:\n" +
                $"TransactionId: {transaction.TransactionId}\n" +
                $"UserId: {transaction.UserId}\n" +
                $"Date: {transaction.Date}\n" +
                $"Amount: {transaction.Amount}\n" +
                $"Category: {transaction.Category}\n" +
                $"Description: {transaction.Description}\n" +
                $"Merchant: {transaction.Merchant}\n";

            var errorDetails = string.Join(Environment.NewLine, errors.Select(e => $"- {e}"));

            var fileContent =
                $"--------------------- Validation Error Logged at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} ---------------------\n" +
                $"{transactionDetails}\n" +
                $"Errors:\n{errorDetails}\n" +
                "------------------------------------------------------------------------------------------\n";

            try
            {
                lock (_fileLock)
                {
                    File.AppendAllText(errorFilePath, fileContent);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"An error occurred while writing to the file: {ex.Message}");
                throw;
            }
        }
    }

}
