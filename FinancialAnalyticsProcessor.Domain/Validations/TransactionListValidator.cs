using FinancialAnalyticsProcessor.Domain.Entities;
using FluentValidation;

namespace FinancialAnalyticsProcessor.Domain.Validations
{
    public class TransactionListValidator : AbstractValidator<IEnumerable<Transaction>>
    {
        public TransactionListValidator()
        {
            // Rule to check if the list is not null or empty
            RuleFor(transactions => transactions)
                .NotNull()
                .WithMessage("The transaction list cannot be null.")
                .NotEmpty()
                .WithMessage("The transaction list cannot be empty.");

            // Rule to validate each transaction in the list
            RuleForEach(transactions => transactions)
                .SetValidator(new TransactionValidator())
                .WithMessage("One or more transactions are invalid.");
        }
    }
}
