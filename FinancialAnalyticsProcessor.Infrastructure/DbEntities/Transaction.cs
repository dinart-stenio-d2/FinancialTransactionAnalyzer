namespace FinancialAnalyticsProcessor.Infrastructure.DbEntities
{
    /// <summary>
    /// Represents a financial transaction.
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// Gets or sets the unique identifier for the transaction.
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the user associated with the transaction.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the date and time of the transaction in UTC format.
        /// </summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// Gets or sets the transaction amount.
        /// A null value indicates that the amount is not specified.
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// Gets or sets the category of the transaction (e.g., Food, Transport, Utilities).
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets the description of the transaction.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the name of the merchant or vendor associated with the transaction.
        /// </summary>
        public string Merchant { get; set; }
    }

}
