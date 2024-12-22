using FinancialAnalyticsProcessor.Domain.Entities;
using FluentValidation;

namespace FinancialAnalyticsProcessor.Domain.Validations
{
    public class TransactionValidator : AbstractValidator<Transaction>
    {
        private static readonly object _fileLock = new object(); // Lock para controle de acesso ao arquivo
        public TransactionValidator()
        {
            RuleFor(t => t.TransactionId)
                .NotEmpty().WithMessage("TransactionId is required.");

            RuleFor(t => t.UserId)
                .NotEmpty().WithMessage("UserId is required.");

            RuleFor(t => t.Date)
                .NotEmpty().WithMessage("Date is required.")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Date cannot be in the future.");

      

            RuleFor(t => t.Category)
                .NotEmpty().WithMessage("Category is required.")
                .MaximumLength(100).WithMessage("Category must not exceed 100 characters.");

            RuleFor(t => t.Description)
              .NotEmpty()
              .WithMessage(t => $"Description cannot be null or empty. Transaction ID: |{t.TransactionId}|")
              .MaximumLength(255)
              .WithMessage(t => $"Description must not exceed 255 characters. Transaction ID: |{t.TransactionId}|");


            RuleFor(t => t.Merchant)
                .NotEmpty().WithMessage("Merchant is required.")
                .MaximumLength(100).WithMessage("Merchant must not exceed 100 characters.");
            
            // Rule for capturing and reporting any validation failure with transaction details
            RuleFor(t => t)
                .Custom((transaction, context) =>
                {
                    // Validation for each property explicitly
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

                    // If there are errors, add a failure
                    if (errors.Any())
                    {
                        // Include all transaction field values in the error message
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
