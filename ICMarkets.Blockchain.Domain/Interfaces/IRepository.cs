using ICMarkets.Blockchain.Domain.Entities;

namespace ICMarkets.Blockchain.Domain.Interfaces
{
    /// <summary>
    /// Repository interface for blockchain data operations.
    /// </summary>
    public interface IRepository
    {
        /// <summary>
        /// Adds a collection of blockchain data to the database.
        /// </summary>
        Task AddRangeAsync(IEnumerable<BlockchainData> data, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a queryable collection of all blockchain data.
        /// Used for OData query composition.
        /// </summary>
        IQueryable<BlockchainData> GetQueryableHistory();
    }
}
