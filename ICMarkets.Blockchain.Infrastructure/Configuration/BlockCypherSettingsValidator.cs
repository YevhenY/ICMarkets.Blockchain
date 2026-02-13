using Microsoft.Extensions.Options;
using ICMarkets.Blockchain.Domain.Validation;

namespace ICMarkets.Blockchain.Infrastructure.Configuration
{
    /// <summary>
    /// Validates BlockCypherSettings configuration at startup.
    /// Ensures all settings are within acceptable ranges for production use.
    /// </summary>
    public class BlockCypherSettingsValidator : IValidateOptions<BlockCypherSettings>
    {
        public ValidateOptionsResult Validate(string? name, BlockCypherSettings options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("BlockCypher settings are missing.");

            if (options.Endpoints == null || !options.Endpoints.Any())
                return ValidateOptionsResult.Fail("BlockCypher:Endpoints must contain at least one endpoint.");

            foreach (var kvp in options.Endpoints)
            {
                var symbol = kvp.Key;
                var url = kvp.Value;

                // Use shared symbol validation from domain
                var symbolError = SymbolValidator.GetValidationError(symbol, $"BlockCypher:Endpoints key '{symbol}'");
                if (symbolError != null)
                    return ValidateOptionsResult.Fail(symbolError);

                // Validate URL
                if (string.IsNullOrWhiteSpace(url))
                    return ValidateOptionsResult.Fail($"BlockCypher:Endpoints value for '{symbol}' must not be empty.");

                if (url.Length > BlockchainDataRules.RequestUrl.MaxLength)
                    return ValidateOptionsResult.Fail($"BlockCypher:Endpoints value for '{symbol}' exceeds maximum length of {BlockchainDataRules.RequestUrl.MaxLength} characters.");

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return ValidateOptionsResult.Fail($"BlockCypher:Endpoints value for '{symbol}' is not a valid HTTP/HTTPS URL: {url}.");
            }

            // Validate RequestsPerSecond
            if (options.RequestsPerSecond <= 0)
                return ValidateOptionsResult.Fail("BlockCypher:RequestsPerSecond must be greater than 0.");

            if (options.RequestsPerSecond > 100)
                return ValidateOptionsResult.Fail("BlockCypher:RequestsPerSecond should not exceed 100 to avoid rate limiting.");

            // Validate RequestsPerHour
            if (options.RequestsPerHour <= 0)
                return ValidateOptionsResult.Fail("BlockCypher:RequestsPerHour must be greater than 0.");

            // Validate MaxConcurrentRequests
            if (options.MaxConcurrentRequests <= 0)
                return ValidateOptionsResult.Fail("BlockCypher:MaxConcurrentRequests must be greater than 0.");

            if (options.MaxConcurrentRequests > 10)
                return ValidateOptionsResult.Fail("BlockCypher:MaxConcurrentRequests should not exceed 10 to prevent resource exhaustion.");

            // Validate HttpTimeoutSeconds
            if (options.HttpTimeoutSeconds <= 0)
                return ValidateOptionsResult.Fail("BlockCypher:HttpTimeoutSeconds must be greater than 0.");

            if (options.HttpTimeoutSeconds > 300)
                return ValidateOptionsResult.Fail("BlockCypher:HttpTimeoutSeconds should not exceed 300 seconds (5 minutes).");

            // Validate TokenCheckDelayMs
            if (options.TokenCheckDelayMs < 10)
                return ValidateOptionsResult.Fail("BlockCypher:TokenCheckDelayMs should be at least 10ms to avoid excessive CPU usage.");

            if (options.TokenCheckDelayMs > 1000)
                return ValidateOptionsResult.Fail("BlockCypher:TokenCheckDelayMs should not exceed 1000ms (1 second).");

            // Validate DefaultCircuitBreakerDurationSeconds
            if (options.DefaultCircuitBreakerDurationSeconds <= 0)
                return ValidateOptionsResult.Fail("BlockCypher:DefaultCircuitBreakerDurationSeconds must be greater than 0.");

            if (options.DefaultCircuitBreakerDurationSeconds > 86400)
                return ValidateOptionsResult.Fail("BlockCypher:DefaultCircuitBreakerDurationSeconds should not exceed 86400 seconds (24 hours).");

            // Validate InitialRemainingRequests
            if (options.InitialRemainingRequests < 0)
                return ValidateOptionsResult.Fail("BlockCypher:InitialRemainingRequests must be non-negative.");

            // Logical validation: InitialRemainingRequests should match RequestsPerHour
            if (options.InitialRemainingRequests != options.RequestsPerHour)
                return ValidateOptionsResult.Fail($"BlockCypher:InitialRemainingRequests ({options.InitialRemainingRequests}) should match RequestsPerHour ({options.RequestsPerHour}).");

            return ValidateOptionsResult.Success;
        }
    }
}
