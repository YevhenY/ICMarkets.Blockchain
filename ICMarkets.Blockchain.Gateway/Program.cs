using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using ICMarkets.Blockchain.Gateway.Configuration;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ICMarkets.Blockchain.Gateway")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    Log.Information("Starting ICMarkets Blockchain API Gateway");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Configure Rate Limiting Settings
    builder.Services.AddOptions<RateLimitSettings>()
        .Bind(builder.Configuration.GetSection(RateLimitSettings.SectionName))
        .ValidateOnStart();

    builder.Services.AddSingleton<IValidateOptions<RateLimitSettings>, RateLimitSettingsValidator>();

    // Add YARP Reverse Proxy
    var proxyBuilder = builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // Add Health Checks
    builder.Services.AddHealthChecks();

    // Configure Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        var rateLimitSettings = builder.Configuration
            .GetSection(RateLimitSettings.SectionName)
            .Get<RateLimitSettings>() ?? new RateLimitSettings();

        options.RejectionStatusCode = rateLimitSettings.RejectionStatusCode;

        // Per-IP rate limiting with configured values
        options.AddFixedWindowLimiter("user-level", limiterOptions =>
        {
            limiterOptions.Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds);
            limiterOptions.PermitLimit = rateLimitSettings.PermitLimit;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = rateLimitSettings.QueueLimit;
        });

        // Global rate limiting policy applied by IP address
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ipAddress,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds),
                    PermitLimit = rateLimitSettings.PermitLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = rateLimitSettings.QueueLimit
                });
        });

        options.OnRejected = async (context, cancellationToken) =>
        {
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Log.Warning("Rate limit exceeded for IP: {IpAddress}. Path: {Path}", ipAddress, context.HttpContext.Request.Path);

            context.HttpContext.Response.StatusCode = rateLimitSettings.RejectionStatusCode;
            context.HttpContext.Response.ContentType = "application/json";

            var response = new
            {
                error = rateLimitSettings.RejectionError,
                message = rateLimitSettings.RejectionMessage,
                retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? retryAfter.TotalSeconds
                    : rateLimitSettings.DefaultRetryAfterSeconds
            };

            await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        };
    });

    var app = builder.Build();

    // Validate rate limiting settings at startup
    var rateLimitOptions = app.Services.GetRequiredService<IOptions<RateLimitSettings>>();
    Log.Information("Rate limiting configured: {PermitLimit} requests per {WindowSeconds} seconds",
        rateLimitOptions.Value.PermitLimit,
        rateLimitOptions.Value.WindowSeconds);

    // Global error handling
    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Internal Server Error",
                message = "An unexpected error occurred. Please try again later.",
                traceId = context.TraceIdentifier
            });
        }
    });

    // Request logging middleware
    app.Use(async (context, next) =>
    {
        var startTime = DateTime.UtcNow;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        Log.Information("Incoming request: {Method} {Path} from {IpAddress}",
            context.Request.Method,
            context.Request.Path,
            ipAddress);

        await next();

        var duration = DateTime.UtcNow - startTime;
        Log.Information("Request completed: {Method} {Path} {StatusCode} in {Duration}ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            duration.TotalMilliseconds);
    });

    // Apply rate limiting
    app.UseRateLimiter();

    // Health check endpoint
    app.MapHealthChecks("/health").WithMetadata(new { Description = "Gateway health status" });

    // Map reverse proxy routes
    app.MapReverseProxy(proxyPipeline =>
    {
        proxyPipeline.Use(async (context, next) =>
        {
            Log.Debug("Proxying request to: {Destination}", context.Request.Path);
            await next();
            Log.Debug("Proxy response received with status: {StatusCode}", context.Response.StatusCode);
        });
    });

    Log.Information("ICMarkets Blockchain API Gateway started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
