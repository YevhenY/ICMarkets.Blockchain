namespace ICMarkets.Blockchain.Gateway.Configuration;

/// <summary>
/// Configuration settings for API Gateway rate limiting.
/// </summary>
public sealed class RateLimitSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Gets or sets the time window in seconds for rate limiting.
    /// Default is 60 seconds (1 minute).
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of requests allowed per window.
    /// Default is 60 requests per window.
    /// </summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>
    /// Gets or sets the maximum number of queued requests.
    /// Default is 10 requests.
    /// </summary>
    public int QueueLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets the default retry after period in seconds when rate limit is exceeded.
    /// Default is 60 seconds.
    /// </summary>
    public int DefaultRetryAfterSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the HTTP status code to return when rate limit is exceeded.
    /// Default is 429 (Too Many Requests).
    /// </summary>
    public int RejectionStatusCode { get; set; } = 429;

    /// <summary>
    /// Gets or sets the error message to display when rate limit is exceeded.
    /// </summary>
    public string RejectionMessage { get; set; } = "Too many requests. Please try again later.";

    /// <summary>
    /// Gets or sets the error title to display when rate limit is exceeded.
    /// </summary>
    public string RejectionError { get; set; } = "Rate limit exceeded";
}
