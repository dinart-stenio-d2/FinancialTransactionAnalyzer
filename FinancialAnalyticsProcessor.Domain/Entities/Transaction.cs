using FinancialAnalyticsProcessor.Core.Domain.DomainObjects;

namespace FinancialAnalyticsProcessor.Domain.Entities
{
    /// <summary>
    /// Transaction entity
    /// </summary>
    public class Transaction : Entity, IAggregateRoot
    {
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public DateTimeOffset Date { get; set; }
        public decimal? Amount { get; set; } 
        public string Category { get; set; }
        public string Description { get; set; }
        public string Merchant { get; set; }

        /// <summary>
        /// Method to calculate user summaries
        /// </summary>
        /// <param name="transactions"></param>
        /// <returns></returns>
        public IEnumerable<dynamic> CalculateUserSummaries(IEnumerable<Transaction> transactions)
        {
            try
            {
                return transactions
                    .GroupBy(t => t.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        TotalIncome = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                        TotalExpense = g.Where(t => t.Amount < 0).Sum(t => t.Amount)
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while calculating user summaries: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Method to calculate top categories
        /// </summary>
        /// <param name="transactions"></param>
        /// <param name="topCount"></param>
        /// <returns></returns>
        public IEnumerable<dynamic> CalculateTopCategories(IEnumerable<Transaction> transactions, int topCount = 3)
        {
            try
            {
                return transactions
                    .GroupBy(t => t.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(topCount)
                    .Select(g => new { Category = g.Key, TransactionsCount = g.Count() });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while calculating top categories: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Method to find the highest spender
        /// </summary>
        /// <param name="transactions">The list of transactions</param>
        /// <returns>The highest spender information or default values if no transactions exist</returns>
        public dynamic FindHighestSpender(IEnumerable<Transaction> transactions)
        {
            try
            {
                if (transactions == null || !transactions.Any())
                {
                    // Return a default value or handle the empty list case
                    return new { UserId = Guid.Empty, TotalSpent = 0m }; // Default value
                }

                var highestSpender = transactions
                    .GroupBy(t => t.UserId)
                    .OrderByDescending(g => g.Sum(t => t.Amount ?? 0)) // Handle null amounts with ??
                    .Select(g => new
                    {
                        UserId = g.Key,
                        TotalSpent = g.Sum(t => t.Amount ?? 0) // Sum with null amounts treated as 0
                    })
                    .FirstOrDefault(); // Safely return null if no results

                // Ensure a non-null return value
                return highestSpender ?? new { UserId = Guid.Empty, TotalSpent = 0m };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while finding the highest spender: {ex.Message}");
                throw;
            }
        }
    }



}
