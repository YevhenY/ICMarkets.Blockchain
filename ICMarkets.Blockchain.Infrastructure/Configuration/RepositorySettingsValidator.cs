using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.Infrastructure.Configuration
{
    /// <summary>
    /// Validates RepositorySettings configuration at startup.
    /// Ensures data retention settings are within acceptable ranges.
    /// </summary>
    public class RepositorySettingsValidator : IValidateOptions<RepositorySettings>
    {
        public ValidateOptionsResult Validate(string? name, RepositorySettings options)
        {
            if (options == null)
                return ValidateOptionsResult.Fail("RepositorySettings are missing.");

            // Validate DataRetentionDays
            if (options.DataRetentionDays < 0)
                return ValidateOptionsResult.Fail("RepositorySettings:DataRetentionDays must be non-negative.");

            if (options.DataRetentionDays > 36500) // 100 years
                return ValidateOptionsResult.Fail("RepositorySettings:DataRetentionDays should not exceed 36500 (100 years).");

            // Warn about potential performance issues with very large retention
            if (options.DataRetentionDays > 3650) // 10 years
            {
                // Note: ValidateOptionsResult doesn't support warnings, so we use a reasonable upper limit
                // Operators can still configure up to 100 years if needed
            }

            return ValidateOptionsResult.Success;
        }
    }
}
