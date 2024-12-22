using FluentValidation;
using FluentValidation.Results;


namespace FinancialAnalyticsProcessor.Core.Domain.DomainObjects
{
    public abstract class Entity
    {
        //public Guid Id { get; set; }
        public bool Valid { get; private set; }
        public bool Invalid => !Valid;

        public ValidationResult? ValidationResult { get; private set; }

        //protected Entity(string idParam = null)
        //{
        //    if (string.IsNullOrEmpty(idParam))
        //    {
        //        Id = Guid.NewGuid();
        //    }
        //    else
        //    {
        //        Id = new Guid(idParam);
        //    }

        //}

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
        /// <summary>
        /// Method used to compare instances of a class
        /// Each entity have own identity so for one entity
        /// to be equal to another it need to be of the same type 
        /// and also have the same Id
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>true or false</returns>
        //public override bool Equals(object? obj)
        //{
        //    var compareTo = obj as Entity;

        //    if (ReferenceEquals(this, compareTo)) return true;
        //    if (ReferenceEquals(null, compareTo)) return false;

        //    return Id.Equals(compareTo.Id);
        //}

        /// <summary>
        /// To comparer the has code of one class
        /// HashCode is a exclusive code of the class 
        /// </summary>
        /// <returns></returns>
        //public override int GetHashCode()
        //{
        //    return (GetType().GetHashCode() * 907) + Id.GetHashCode();
        //}
    }
}
