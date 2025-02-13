using FluentValidation;
using FluentValidation.Results;


namespace FinancialAnalyticsProcessor.Core.Domain.DomainObjects
{
    /// <summary>
    /// Base class for all entities 
    /// </summary>
    public abstract class Entity
    {
        public bool Valid { get; private set; }
        public bool Invalid => !Valid;

        public ValidationResult? ValidationResult { get; private set; }

        /// <summary>
        /// Notification Pattern Generic Method
        /// </summary>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="model"></param>
        /// <param name="validator"></param>
        /// <returns></returns>
        public bool Validate<TModel>(TModel model, AbstractValidator<TModel> validator)
        {
            ValidationResult = validator.Validate(model);
            return Valid = ValidationResult.IsValid;
        }

        public bool HasId<TModel>(dynamic model)
        {
            bool result = default(bool);

            if (!string.IsNullOrEmpty(model.Id))
            {
                result = true;
            }

            return result;
        }
    }
}
