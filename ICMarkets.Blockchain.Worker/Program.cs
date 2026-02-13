using ICMarkets.Blockchain.Application.Behaviours;
using ICMarkets.Blockchain.Application.Features;
using ICMarkets.Blockchain.Domain.Interfaces;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Infrastructure.Persistence;
using ICMarkets.Blockchain.Infrastructure.Services;
using ICMarkets.Blockchain.Worker;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

// ========================================
// SERILOG CONFIGURATION
// ========================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting ICMarkets Blockchain Worker Service");

    var builder = Host.CreateApplicationBuilder(args);

    // ========================================
    // SERILOG HOST CONFIGURATION
    // ========================================
    builder.Services.AddSerilog();

    // ========================================
    // INFRASTRUCTURE LAYER CONFIGURATION
    // ========================================

    // Database Context (SQLite)
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Repository Pattern
    builder.Services.AddScoped<IRepository, BlockchainRepository>();

    // HTTP Client for BlockCypher API
    builder.Services.AddHttpClient();

    // BlockCypher Service (Singleton for rate limiting state)
    builder.Services.AddSingleton<IBlockCypherService, BlockCypherService>();

    // Configuration Options with Validation
    builder.Services
        .AddOptions<BlockCypherSettings>()
        .Bind(builder.Configuration.GetSection("BlockCypher"))
        .ValidateOnStart();

    builder.Services
        .AddOptions<RepositorySettings>()
        .Bind(builder.Configuration.GetSection("RepositorySettings"))
        .ValidateOnStart();

    builder.Services
        .AddOptions<WorkerSettings>()
        .Bind(builder.Configuration.GetSection("Worker"))
        .ValidateOnStart();

    // Configuration Validators
    builder.Services.AddSingleton<IValidateOptions<BlockCypherSettings>, BlockCypherSettingsValidator>();
    builder.Services.AddSingleton<IValidateOptions<RepositorySettings>, RepositorySettingsValidator>();

    // ========================================
    // APPLICATION LAYER CONFIGURATION
    // ========================================

    // FluentValidation
    builder.Services.AddValidatorsFromAssembly(typeof(SyncBlockchainCommand).Assembly);

    // MediatR with Pipeline Behaviors
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(SyncBlockchainCommand).Assembly);
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    });

    // ========================================
    // BACKGROUND SERVICE REGISTRATION
    // ========================================
    builder.Services.AddHostedService<BlockchainSyncWorker>();

    // ========================================
    // BUILD AND RUN
    // ========================================
    var host = builder.Build();

    // Database Initialization
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
        Log.Information("Database initialized successfully");
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker service terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
