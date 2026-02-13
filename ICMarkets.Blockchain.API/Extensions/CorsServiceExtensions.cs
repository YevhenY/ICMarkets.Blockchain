using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ICMarkets.Blockchain.API.Extensions;

/// <summary>
/// CORS configuration settings bound from appsettings.json.
/// Kept in API layer as CORS is a web/API concern, not infrastructure.
/// </summary>
public class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public string[] AllowedMethods { get; set; } = Array.Empty<string>();
    public string[] AllowedHeaders { get; set; } = Array.Empty<string>();
    public string[] ExposedHeaders { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = false;
    public int PreflightMaxAgeSeconds { get; set; } = 600;
}

/// <summary>
/// Extension methods for configuring CORS services.
/// </summary>
public static class CorsServiceExtensions
{
    private const string DefaultPolicyName = "DefaultCorsPolicy";

    /// <summary>
    /// Adds CORS services with configuration from appsettings.
    /// </summary>
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsSettings = configuration
            .GetSection("Cors")
            .Get<CorsSettings>() ?? new CorsSettings();

        ValidateCorsSettings(corsSettings);

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, policy =>
            {
                ConfigureCorsPolicy(policy, corsSettings);
            });
        });

        return services;
    }

    /// <summary>
    /// Gets the default CORS policy name for middleware pipeline.
    /// </summary>
    public static string GetDefaultCorsPolicyName() => DefaultPolicyName;

    private static void ValidateCorsSettings(CorsSettings settings)
    {
        if (settings.AllowedOrigins == null || settings.AllowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "CORS configuration error: AllowedOrigins must contain at least one entry. " +
                "Use '*' for development or specify exact origins for production.");
        }

        var allowAnyOrigin = settings.AllowedOrigins.Contains("*");

        if (settings.AllowCredentials && allowAnyOrigin)
        {
            throw new InvalidOperationException(
                "CORS configuration error: Cannot use AllowCredentials with AllowAnyOrigin ('*'). " +
                "Specify exact origins or disable credentials.");
        }
    }

    private static void ConfigureCorsPolicy(
        CorsPolicyBuilder policy,
        CorsSettings settings)
    {
        var allowAnyOrigin = settings.AllowedOrigins.Contains("*");

        // Configure Origins
        if (allowAnyOrigin)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(settings.AllowedOrigins)
                  .SetIsOriginAllowedToAllowWildcardSubdomains();
        }

        // Configure Methods
        if (settings.AllowedMethods?.Length > 0)
        {
            policy.WithMethods(settings.AllowedMethods);
        }
        else
        {
            policy.AllowAnyMethod();
        }

        // Configure Headers
        if (settings.AllowedHeaders?.Length > 0)
        {
            policy.WithHeaders(settings.AllowedHeaders);
        }
        else
        {
            policy.AllowAnyHeader();
        }

        // Configure Credentials (only if not using AllowAnyOrigin)
        if (settings.AllowCredentials && !allowAnyOrigin)
        {
            policy.AllowCredentials();
        }

        // Configure Exposed Headers
        if (settings.ExposedHeaders?.Length > 0)
        {
            policy.WithExposedHeaders(settings.ExposedHeaders);
        }

        // Configure Preflight Cache
        if (settings.PreflightMaxAgeSeconds > 0)
        {
            policy.SetPreflightMaxAge(TimeSpan.FromSeconds(settings.PreflightMaxAgeSeconds));
        }
    }
}
