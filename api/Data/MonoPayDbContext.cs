using Microsoft.EntityFrameworkCore;
using MonoPayAggregator.Models;

namespace MonoPayAggregator.Data
{
    /// <summary>
    /// Represents the Entity Framework Core database context for MonoPay.
    /// It defines DbSets for users, payments and email verifications. When
    /// running EF migrations this context will generate the necessary
    /// database schema. Replace connection string values in appsettings.json
    /// to point at your SQL Server instance.
    /// </summary>
    public class MonoPayDbContext : DbContext
    {
        public MonoPayDbContext(DbContextOptions<MonoPayDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<PaymentTransaction> Payments => Set<PaymentTransaction>();
        public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure simple indexes and keys
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<EmailVerification>().HasIndex(v => v.Token).IsUnique();
        }
    }
}