namespace ICMarkets.Blockchain.Worker;

/// <summary>
/// Configuration settings for the blockchain synchronization worker.
/// </summary>
public class WorkerSettings
{
    /// <summary>
    /// Interval between synchronization operations in minutes.
    /// Default: 3 minutes
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 3;
}
