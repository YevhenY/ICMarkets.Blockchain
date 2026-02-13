namespace ICMarkets.Blockchain.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration for repository behavior.
    /// </summary>
    public class RepositorySettings
    {
        /// <summary>
        /// Number of days of historical data to retain in query results.
        /// Older data is excluded from GetQueryableHistory to improve performance.
        /// Default: 30 days.
        /// </summary>
        public int DataRetentionDays { get; set; } = 30;
    }
}
