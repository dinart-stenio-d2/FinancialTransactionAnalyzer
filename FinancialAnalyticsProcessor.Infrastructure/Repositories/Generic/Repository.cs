using EFCore.BulkExtensions;
using FinancialAnalyticsProcessor.Domain.Interfaces.Repositories;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace FinancialAnalyticsProcessor.Infrastructure.Repositories.Generic
{
    /// <summary>
    /// Provides a generic repository implementation for database operations on entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type that the repository will manage.</typeparam>
    public class  Repository<T> : IRepository<T> where T : class
    {

        /// <summary>
        /// The database context used for accessing the database.
        /// </summary>
        private readonly TransactionDbContext _context;

        /// <summary>
        /// The DbSet representing the entity set for <typeparamref name="T"/>.
        /// </summary>
        private readonly DbSet<T> _dbSet;

        /// <summary>
        /// The logger used for recording informational messages, warnings, and errors.
        /// </summary>
        private readonly ILogger<Repository<T>> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{T}"/> class.
        /// </summary>
        /// <param name="context">The database context to use for entity operations.</param>
        /// <param name="logger">The logger instance for recording logs.</param>
        public Repository(TransactionDbContext context, ILogger<Repository<T>> logger)
        {
            _context = context;
            _dbSet = context.Set<T>();
            _logger = logger;
        }

        /// <summary>
        /// Saves all pending changes to the database asynchronously.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Adds a collection of entities to the database in a single operation.
        /// </summary>
        /// <param name="entities">The collection of entities to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _context.Set<T>().AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Performs a bulk insert of a collection of entities into the database.
        /// </summary>
        /// <param name="entities">The collection of entities to insert.</param>
        /// <param name="batchSize">The number of records per batch during insertion (default is 10,000).</param>
        /// <param name="configureOptions">Optional configuration for bulk insertion.</param>
        /// <returns>A task representing the asynchronous bulk insert operation.</returns>
        public async Task BulkInsertAsync(IEnumerable<T> entities, int batchSize = 10000, Action<BulkConfig> configureOptions = null)
        {
            _logger.LogInformation("Starting bulk insert for {EntityType}. Total records: {TotalRecords}.", typeof(T).Name, entities.Count());

            try
            {
                var bulkConfig = new BulkConfig
                {
                    BatchSize = batchSize,
                    SetOutputIdentity = true
                };

                configureOptions?.Invoke(bulkConfig);

                // Convert to a list for easier batching
                var entitiesList = entities.ToList();
                int totalRecords = entitiesList.Count;
                int processedRecords = 0;

                // Retrieve existing TransactionIds from the database to avoid duplicates
                var existingIds = await _context.Set<T>()
                    .Select(e => EF.Property<Guid>(e, "TransactionId")) // Replace "TransactionId" with the actual property name
                    .ToListAsync();

                // Filter out entities with duplicate TransactionIds
                entitiesList = entitiesList
                    .Where(e => !existingIds.Contains((Guid)typeof(T).GetProperty("TransactionId")!.GetValue(e)!))
                    .ToList();

                _logger.LogInformation("{FilteredRecords} records remain after filtering duplicates.", entitiesList.Count);

                for (int i = 0; i < entitiesList.Count; i += batchSize)
                {
                    var batch = entitiesList.Skip(i).Take(batchSize).ToList();

                    using var transaction = await _context.Database.BeginTransactionAsync();

                    foreach (var entity in batch)
                    {
                        // Detach entities to prevent tracking issues
                        _context.Entry(entity).State = EntityState.Detached;

                        // Add entity to the context
                        _dbSet.Add(entity);
                    }

                    // Save changes and commit the batch transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    processedRecords += batch.Count;
                    _logger.LogInformation("{ProcessedRecords}/{TotalRecords} records inserted successfully.", processedRecords, totalRecords);

                    // Clear the change tracker to avoid memory issues
                    _context.ChangeTracker.Clear();
                }

                _logger.LogInformation("Bulk insert completed successfully for {EntityType}. Total records: {TotalRecords}.", typeof(T).Name, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during bulk insert for {EntityType}.", typeof(T).Name);
                throw;
            }
        }

        /// <summary>
        /// Retrieves an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity.</param>
        /// <returns>A task representing the asynchronous operation, returning the entity if found, otherwise null.</returns>
        public async Task<T?> GetByIdAsync(Guid id)
        {
            _logger.LogInformation("Retrieving entity with ID {Id} from the database.", id);
            try
            {
                _logger.BeginScope("Method: GetByIdAsync");


                IQueryable<T> query = _context.Set<T>();


                var entityType = _context.Model.FindEntityType(typeof(T));
                if (entityType != null)
                {
                    foreach (var navigation in entityType.GetNavigations())
                    {
                        query = query.Include(navigation.Name);
                    }
                }

                var entity = await query.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

                if (entity == null)
                {
                    _logger.LogWarning("Entity with ID {Id} not found.", id);
                }
                else
                {
                    _logger.LogInformation("Entity with ID {Id} retrieved successfully.", id);
                }

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving entity with ID {Id}.", id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all entities of type <typeparamref name="T"/>, optionally including related entities.
        /// </summary>
        /// <param name="includes">Optional navigation properties to include in the query.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of all entities.</returns>
        public async Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
        {
            _logger.LogInformation("Retrieving all entities with related data from the database.");
            try
            {
                using (_logger.BeginScope("Method: GetAllAsync"))
                {
                    IQueryable<T> query = _context.Set<T>();


                    if (includes != null)
                    {
                        foreach (var include in includes)
                        {
                            query = query.Include(include);
                        }
                    }

                    var entities = await query.ToListAsync();
                    _logger.LogInformation("Successfully retrieved {Count} entities.", entities.Count);
                    return entities;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving entities.");
                throw;
            }
        }

        /// <summary>
        /// Adds a new entity to the database.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddAsync(T entity)
        {
            _logger.LogInformation("Adding a new entity to the database: {Entity}.", entity);
            try
            {
                using (_logger.BeginScope("Method: AddAsync"))
                {
                    using var dbContext = _context;
                    await dbContext.Set<T>().AddAsync(entity);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Entity added successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding the entity: {Entity}.", entity);
                throw;
            }
        }
        
        /// <summary>
        /// Updates an existing entity in the database.
        /// </summary>
        /// <param name="entity">The entity with updated values.</param>
        /// <returns>A task representing the asynchronous update operation.</returns>
        public async Task UpdateAsync(T entity)
        {
            _logger.LogInformation("Updating an entity in the database: {Entity}.", entity);

            try
            {
                using (_logger.BeginScope("Method: UpdateAsync"))
                {
                    using var dbContext = _context;

                    // Detach any existing tracked entity with the same key
                    var existingEntity = dbContext.Set<T>().Local.FirstOrDefault(e => ((dynamic)e).Id == ((dynamic)entity).Id);
                    if (existingEntity != null)
                    {
                        dbContext.Entry(existingEntity).State = EntityState.Detached;
                    }

                    // Attach the updated entity and mark it as modified
                    dbContext.Attach(entity);
                    dbContext.Entry(entity).State = EntityState.Modified;

                    // Save changes
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Entity updated successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the entity: {Entity}.", entity);
                throw;
            }
        }

        /// <summary>
        /// Deletes an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the entity to delete.</param>
        /// <returns>A task representing the asynchronous delete operation, returning true if the entity was deleted successfully, otherwise false.</returns>
        public async Task<bool> DeleteAsync(Guid id)
        {
            _logger.LogInformation("Deleting entity with ID {Id} from the database.", id);
            try
            {
                using (_logger.BeginScope("Method: DeleteAsync"))
                {
                    using var dbContext = _context;
                    var entity = await dbContext.Set<T>().FindAsync(id);
                    if (entity == null)
                    {
                        _logger.LogWarning("Entity with ID {Id} not found. Deletion skipped.", id);
                        return false; // Return false if the entity was not found
                    }

                    dbContext.Set<T>().Remove(entity);
                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation("Entity with ID {Id} deleted successfully.", id);
                    return true; // Return true if the deletion was successful
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting the entity with ID {Id}.", id);
                return false; // Return false if an exception occurred
            }
        }
        /// <summary>
        /// Deletes transactions based on a list of transaction IDs.
        /// </summary>
        /// <param name="transactionIds">A collection of transaction IDs to delete.</param>
        /// <returns>A task representing the asynchronous delete operation, returning the number of records deleted.</returns>
        public async Task<int> DeleteTransactionsByIdsAsync(IEnumerable<Guid> transactionIds)
        {
            if (transactionIds == null || !transactionIds.Any())
            {
                throw new ArgumentException("The list of transaction IDs cannot be null or empty.", nameof(transactionIds));
            }

            const int batchSize = 10000; 
            var totalDeleted = 0;

            try
            {
               
                var batches = transactionIds
                    .Select((id, index) => new { id, index })
                    .GroupBy(x => x.index / batchSize)
                    .Select(g => g.Select(x => x.id).ToList());

                foreach (var batch in batches)
                {
                    
                    var batchCount = await _context.Transactions
                        .Where(t => batch.Contains(t.TransactionId))
                        .CountAsync();

                    
                    await _context.BulkDeleteAsync(
                        _context.Transactions.Where(t => batch.Contains(t.TransactionId))
                    );

                    totalDeleted += batchCount;

                    _logger.LogInformation("Deleted {DeletedCount} transactions in the current batch.", batchCount);
                }

                _logger.LogInformation("Successfully deleted {TotalDeleted} transactions.", totalDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting transactions.");
                throw; // Re-throw the exception for further handling
            }

            return totalDeleted;
        }

        /// <summary>
        /// Retrieves all entity identifiers based on a specified key selector.
        /// </summary>
        /// <typeparam name="TKey">The type of the key to retrieve.</typeparam>
        /// <param name="keySelector">An expression defining the key to retrieve.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of entity identifiers.</returns>
        public async Task<IEnumerable<TKey>> GetAllEntityIdsAsync<TKey>(Expression<Func<T, TKey>> keySelector)
        {
            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector), "Key selector cannot be null.");
            }

            return await _dbSet
                .Select(keySelector)
                .ToListAsync();
        }
        /// <summary>
        /// Finds entities that match a specified predicate.
        /// </summary>
        /// <param name="predicate">The expression to filter the entities.</param>
        /// <returns>A task representing the asynchronous operation, returning a collection of matching entities.</returns>
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            _logger.LogInformation("Searching for entities with a given predicate.");
            try
            {
                using (_logger.BeginScope("Method: FindAsync"))
                {
                    using var dbContext = _context;
                    var results = await dbContext.Set<T>().Where(predicate).ToListAsync();
                    _logger.LogInformation("Successfully found {Count} matching entities.", results.Count);
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while searching for entities.");
                throw;
            }
        }

    }
}
