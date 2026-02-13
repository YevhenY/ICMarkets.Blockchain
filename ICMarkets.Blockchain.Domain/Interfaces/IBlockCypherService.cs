using ICMarkets.Blockchain.Domain.Entities;

namespace ICMarkets.Blockchain.Domain.Interfaces
{
    /// <summary>
    /// Service interface for fetching blockchain data from BlockCypher API.
    /// </summary>
    public interface IBlockCypherService
    {
        /// <summary>
        /// Fetches current blockchain state for all configured endpoints.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of blockchain data snapshots</returns>
        Task<List<BlockchainData>> FetchAllAsync(CancellationToken cancellationToken = default);
    }
}
