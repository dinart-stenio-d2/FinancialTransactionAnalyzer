using EFCore.BulkExtensions;
using System.Linq.Expressions;

namespace FinancialAnalyticsProcessor.Domain.Interfaces.Repositories
{
    /// <summary>
    /// Defines a generic repository interface for performing database operations on entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that the repository will manage.</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// Performs a bulk insert of a collection of entities into the database.
        /// </summary>
        /// <param name="entities">The collection of entities to insert.</param>
        /// <param name="batchSize">The number of records per batch during insertion (default is 1000).</param>
        /// <param name="configureOptions">Optional configuration for bulk insertion.</param>
        /// <returns>A task representing the asynchronous bulk insert operation.</returns>
        Task BulkInsertAsync(IEnumerable<T> entities, int batchSize = 1000, Action<BulkConfig> configureOptions = null);

        /// <summary>
        /// Retrieves an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity.</param>
        /// <returns>A task representing the asynchronous operation, returning the entity if found, otherwise null.</returns>
        Task<T?> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all entities of type <typeparamref name="T"/>, optionally including related entities.
        /// </summary>
        /// <param name="includes">Optional navigation properties to include in the query.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of all entities.</returns>
        Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);

        /// <summary>
        /// Adds a new entity to the database.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddAsync(T entity);

        /// <summary>
        /// Updates an existing entity in the database.
        /// </summary>
        /// <param name="entity">The entity with updated values.</param>
        /// <returns>A task representing the asynchronous update operation.</returns>
        Task UpdateAsync(T entity);

        /// <summary>
        /// Deletes transactions based on a list of transaction IDs.
        /// </summary>
        /// <param name="transactionIds">A collection of transaction IDs to delete.</param>
        /// <returns>A task representing the asynchronous delete operation, returning the number of records deleted.</returns>
        Task<int> DeleteTransactionsByIdsAsync(IEnumerable<Guid> transactionIds);

        /// <summary>
        /// Retrieves all entity identifiers based on a specified key selector.
        /// </summary>
        /// <typeparam name="TKey">The type of the key to retrieve.</typeparam>
        /// <param name="keySelector">An expression defining the key to retrieve.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of entity identifiers.</returns>
        Task<IEnumerable<TKey>> GetAllEntityIdsAsync<TKey>(Expression<Func<T, TKey>> keySelector);

        /// <summary>
        /// Deletes an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to delete.</param>
        /// <returns>A task representing the asynchronous delete operation, returning true if the entity was deleted successfully, otherwise false.</returns>
        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// Finds entities that match a specified predicate.
        /// </summary>
        /// <param name="predicate">The expression to filter the entities.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of matching entities.</returns>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Adds a collection of entities to the database in a single operation.
        /// </summary>
        /// <param name="entities">The collection of entities to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddRangeAsync(IEnumerable<T> entities);

        /// <summary>
        /// Saves all pending changes to the database.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveChangesAsync();
    }

}
