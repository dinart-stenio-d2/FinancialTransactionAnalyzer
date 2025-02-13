using FinancialAnalyticsProcessor.Domain.Entities;
using FluentValidation;

namespace FinancialAnalyticsProcessor.Domain.Validations
{
    /// <summary>
    /// Validates a collection of <see cref="Transaction"/> objects to ensure they meet defined business rules.
    /// </summary>
    public class TransactionListValidator : AbstractValidator<IEnumerable<Transaction>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionListValidator"/> class.
        /// </summary>
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
