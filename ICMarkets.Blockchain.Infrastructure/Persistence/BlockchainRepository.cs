using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Domain.Interfaces;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.Infrastructure.Persistence
{
    public class BlockchainRepository : IRepository
    {
        private readonly AppDbContext _context;
        private readonly int _dataRetentionDays;

        public BlockchainRepository(
            AppDbContext context,
            IOptions<RepositorySettings> settings)
        {
            _context = context;
            _dataRetentionDays = settings.Value.DataRetentionDays;
        }

        public async Task AddRangeAsync(IEnumerable<BlockchainData> data, CancellationToken cancellationToken = default)
        {
            await _context.BlockchainData.AddRangeAsync(data, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Returns IQueryable for OData query composition.
        /// Data is filtered to the configured retention period (default 30 days).
        /// Ordered by CreatedAt descending. Uses AsNoTracking for read performance.
        /// </summary>
        public IQueryable<BlockchainData> GetQueryableHistory()
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_dataRetentionDays);

            return _context.BlockchainData
                .AsNoTracking()
                .Where(x => x.CreatedAt >= cutoffDate)
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id);
        }
    }
}
