using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace ICMarkets.Blockchain.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<BlockchainData> BlockchainData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BlockchainData>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasValueGenerator<Microsoft.EntityFrameworkCore.ValueGeneration.SequentialGuidValueGenerator>();

                // PRIMARY INDEX: Optimized for GetQueryableHistory query
                entity.HasIndex(e => new { e.CreatedAt, e.Id })
                    .IsDescending(true, false); // CreatedAt DESC, Id ASC

                // SECONDARY INDEX: For filtering by Symbol and time range
                entity.HasIndex(e => new { e.Symbol, e.CreatedAt })
                    .IsDescending(false, true); // Symbol ASC, CreatedAt DESC

                // Database respects domain rules
                entity.Property(e => e.Symbol)
                    .IsRequired()
                    .HasMaxLength(BlockchainDataRules.Symbol.MaxLength);

                entity.Property(e => e.RequestUrl)
                    .IsRequired()
                    .HasMaxLength(BlockchainDataRules.RequestUrl.MaxLength);

                entity.Property(e => e.JsonResponse)
                    .IsRequired();
            });
        }
    }
}
