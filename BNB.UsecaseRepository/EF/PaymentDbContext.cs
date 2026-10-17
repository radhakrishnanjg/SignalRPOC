using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BNB.UsecaseRepository.EF
{
    /// <summary>
    /// Entity Framework context that maps the existing Payment.TransactionDetails table.
    /// </summary>
    public class PaymentDbContext(IConfiguration config) : DbContext
    {
        public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();

        /// <summary>
        /// Connects to SQL Server using the PaymentConnection connection string.
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (options.IsConfigured) return;
            var connectionString = config.GetConnectionString("PaymentConnection")
                ?? throw new InvalidOperationException("Connection string 'PaymentConnection' not found.");
            options.UseSqlServer(connectionString);
        }

        /// <summary>
        /// Maps the entity to its table, key and column types.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionDetail>(e =>
            {
                e.ToTable("TransactionDetails", "Payment");
                e.HasKey(x => x.TransactionId);
                e.Property(x => x.TransactionId).ValueGeneratedOnAdd();
                e.Property(x => x.MessageId);
                // Unique when present: a second insert of the same message id is rejected by the database.
                e.HasIndex(x => x.MessageId).IsUnique().HasFilter("[MessageId] IS NOT NULL").HasDatabaseName("UX_TransactionDetails_MessageId");
                e.Property(x => x.FileName).HasColumnType("varchar(400)").IsRequired();
                e.Property(x => x.SourceType).HasColumnType("varchar(10)");
                e.Property(x => x.SourceSystem).HasColumnType("varchar(100)");
                e.Property(x => x.PaymentStatus).HasColumnType("varchar(10)");
                e.Property(x => x.CreatedDate).HasColumnType("datetime");
            });
        }
    }
}
