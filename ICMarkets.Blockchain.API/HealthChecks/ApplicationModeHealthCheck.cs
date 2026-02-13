using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ICMarkets.Blockchain.API.HealthChecks
{
    /// <summary>
    /// Health check that reports the application operating mode (Full or Read-Only).
    /// </summary>
    public class ApplicationModeHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApplicationModeHealthCheck> _logger;

        public ApplicationModeHealthCheck(
            IConfiguration configuration,
            ILogger<ApplicationModeHealthCheck> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var enableSync = _configuration.GetValue<bool>("EnableSync", true);
                var mode = enableSync ? "Full" : "Read-Only";

                var data = new Dictionary<string, object>
                {
                    { "Mode", mode },
                    { "EnableSync", enableSync },
                    { "Description", enableSync
                        ? "Application is running in full mode with sync enabled"
                        : "Application is running in read-only mode with sync disabled" }
                };

                _logger.LogDebug("Health check executed. Mode: {Mode}", mode);

                return Task.FromResult(
                    HealthCheckResult.Healthy(
                        $"Mode: {mode}",
                        data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking application mode");
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Failed to determine application mode",
                        ex));
            }
        }
    }
}
