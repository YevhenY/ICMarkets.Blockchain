using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.Gateway.Configuration;

/// <summary>
/// Validates <see cref="RateLimitSettings"/> at startup.
/// </summary>
public sealed class RateLimitSettingsValidator : IValidateOptions<RateLimitSettings>
{
    public ValidateOptionsResult Validate(string? name, RateLimitSettings options)
    {
        if (options.WindowSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.WindowSeconds)} must be greater than 0. Current value: {options.WindowSeconds}");
        }

        if (options.WindowSeconds > 3600)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.WindowSeconds)} must not exceed 3600 seconds (1 hour). Current value: {options.WindowSeconds}");
        }

        if (options.PermitLimit <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.PermitLimit)} must be greater than 0. Current value: {options.PermitLimit}");
        }

        if (options.PermitLimit > 10000)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.PermitLimit)} must not exceed 10000 requests. Current value: {options.PermitLimit}");
        }

        if (options.QueueLimit < 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.QueueLimit)} must not be negative. Current value: {options.QueueLimit}");
        }

        if (options.QueueLimit > 1000)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.QueueLimit)} must not exceed 1000 requests. Current value: {options.QueueLimit}");
        }

        if (options.DefaultRetryAfterSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultRetryAfterSeconds)} must be greater than 0. Current value: {options.DefaultRetryAfterSeconds}");
        }

        if (options.RejectionStatusCode < 400 || options.RejectionStatusCode > 599)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.RejectionStatusCode)} must be a valid HTTP error status code (400-599). Current value: {options.RejectionStatusCode}");
        }

        if (string.IsNullOrWhiteSpace(options.RejectionMessage))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.RejectionMessage)} must not be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(options.RejectionError))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.RejectionError)} must not be empty or whitespace.");
        }

        return ValidateOptionsResult.Success;
    }
}
