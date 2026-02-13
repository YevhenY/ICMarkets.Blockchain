namespace ICMarkets.Blockchain.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration settings for BlockCypher API integration.
    /// Supports environment-specific overrides via appsettings.{Environment}.json
    /// </summary>
    public class BlockCypherSettings
    {
        /// <summary>
        /// Blockchain endpoints to fetch data from.
        /// Key: Symbol (e.g., "BTC", "ETH")
        /// Value: API URL
        /// </summary>
        public Dictionary<string, string> Endpoints { get; set; } = new();

        /// <summary>
        /// Maximum requests per second (token bucket capacity). Default: 3
        /// </summary>
        public int RequestsPerSecond { get; set; } = 3;

        /// <summary>
        /// Maximum requests per hour (BlockCypher free tier limit). Default: 100
        /// </summary>
        public int RequestsPerHour { get; set; } = 100;

        /// <summary>
        /// Maximum concurrent HTTP requests. Default: 3
        /// Prevents overwhelming the API and local resources.
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 3;

        /// <summary>
        /// HTTP client timeout in seconds. Default: 30
        /// </summary>
        public int HttpTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Token bucket refill check delay in milliseconds. Default: 50
        /// Lower values increase CPU usage but reduce latency.
        /// </summary>
        public int TokenCheckDelayMs { get; set; } = 50;

        /// <summary>
        /// Default circuit breaker lockout duration in seconds when rate limit is hit
        /// but no Retry-After header is provided. Default: 3600 (1 hour)
        /// </summary>
        public int DefaultCircuitBreakerDurationSeconds { get; set; } = 3600;

        /// <summary>
        /// Initial estimated remaining hourly requests before first API call.
        /// Should match RequestsPerHour. Default: 100
        /// </summary>
        public int InitialRemainingRequests { get; set; } = 100;
    }
}
