using FinancialAnalyticsProcessor.Core.Domain.DomainObjects;
using Microsoft.EntityFrameworkCore;

namespace FinancialAnalyticsProcessor.Infrastructure.Data
{
    public class TransactionDbContext : DbContext
    {
        public TransactionDbContext(DbContextOptions<TransactionDbContext> options) : base(options) { }

        public DbSet<Infrastructure.DbEntities.Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure precision for the Amount property
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .Property(t => t.Amount)
                .HasPrecision(18, 2);

            // Configure indexes

            // Clustered index on TransactionId (primary key)
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .ToTable("Transactions")
                .HasKey(t => t.TransactionId);
            

            // Non-clustered index on UserId and Date (queries by user and date)
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .HasIndex(t => new { t.UserId, t.Date })
                .HasDatabaseName("IX_Transaction_UserId_Date");

            // Non-clustered index on Category
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .HasIndex(t => t.Category)
                .HasDatabaseName("IX_Transaction_Category");

            // Non-clustered index on Description
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .HasIndex(t => t.Description)
                .HasDatabaseName("IX_Transaction_Description");

            // Non-clustered index on Amount
            modelBuilder.Entity<Infrastructure.DbEntities.Transaction>()
                .HasIndex(t => t.Amount)
                .HasDatabaseName("IX_Transaction_Amount");

            // Covered index (manually configured in SQL Server, optional)
            // Add a non-clustered index on UserId, Date with INCLUDE for Category
            // This must be handled as a manual SQL migration or raw SQL script.
        }
    }
}
