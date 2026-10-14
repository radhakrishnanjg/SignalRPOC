using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BNB.UsecaseRepository.EF
{
    // Maps the existing table; the schema is owned by the SQL scripts, not by EF migrations.
    public class PaymentDbContext(IConfiguration config) : DbContext
    {
        public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (options.IsConfigured) return;
            var connectionString = config.GetConnectionString("PaymentConnection")
                ?? throw new InvalidOperationException("Connection string 'PaymentConnection' not found.");
            options.UseSqlServer(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionDetail>(e =>
            {
                e.ToTable("TransactionDetails", "Payment");
                e.HasKey(x => x.TransactionId);
                e.Property(x => x.TransactionId).ValueGeneratedOnAdd();
                e.Property(x => x.FileName).HasColumnType("varchar(400)").IsRequired();
                e.Property(x => x.SourceType).HasColumnType("varchar(10)");
                e.Property(x => x.SourceSystem).HasColumnType("varchar(100)");
                e.Property(x => x.PaymentStatus).HasColumnType("varchar(10)");
                e.Property(x => x.CreatedDate).HasColumnType("datetime");
            });
        }
    }
}
