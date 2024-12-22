using EFCore.BulkExtensions;
using System.Linq.Expressions;

namespace FinancialAnalyticsProcessor.Domain.Interfaces.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task BulkInsertAsync(IEnumerable<T> entities, int batchSize = 1000, Action<BulkConfig> configureOptions = null);
        Task<T?> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task<int> DeleteTransactionsByIdsAsync(IEnumerable<Guid> transactionIds);
        Task<IEnumerable<TKey>> GetAllEntityIdsAsync<TKey>(Expression<Func<T, TKey>> keySelector);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddRangeAsync(IEnumerable<T> entities);
        Task SaveChangesAsync();

    }
}
