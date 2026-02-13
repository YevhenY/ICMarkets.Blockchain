using FluentValidation;
using ICMarkets.Blockchain.API.Configuration;
using ICMarkets.Blockchain.API.Extensions;
using ICMarkets.Blockchain.API.HealthChecks;
using ICMarkets.Blockchain.API.Middleware;
using ICMarkets.Blockchain.Application.Behaviours;
using ICMarkets.Blockchain.Application.Features;
using ICMarkets.Blockchain.Domain.Interfaces;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Infrastructure.Persistence;
using ICMarkets.Blockchain.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;

namespace ICMarkets.Blockchain.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // SERILOG CONFIGURATION
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            // Read EnableSync configuration
            var enableSync = builder.Configuration.GetValue<bool>("EnableSync", true);

            // INFRASTRUCTURE LAYER CONFIGURATION

            // Database Context (SQLite)
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Dependency Injection
            builder.Services.AddScoped<IRepository, BlockchainRepository>();
            builder.Services.AddHttpClient();

            builder.Services.AddSingleton<IBlockCypherService, BlockCypherService>();
            builder.Services
                .AddOptions<BlockCypherSettings>()
                .Bind(builder.Configuration.GetSection("BlockCypher"))
                .ValidateOnStart();
            builder.Services.AddSingleton<IValidateOptions<BlockCypherSettings>, BlockCypherSettingsValidator>();

            Log.Information("BlockCypher service registered. Running in full mode with sync enabled.");

            builder.Services
                .AddOptions<RepositorySettings>()
                .Bind(builder.Configuration.GetSection("RepositorySettings"))
                .ValidateOnStart();

            builder.Services
                .AddOptions<ODataSettings>()
                .Bind(builder.Configuration.GetSection("OData"))
                .ValidateOnStart();

            builder.Services.AddSingleton<IValidateOptions<RepositorySettings>, RepositorySettingsValidator>();
            builder.Services.AddSingleton<IValidateOptions<ODataSettings>, ODataSettingsValidator>();

            // Health Checks with custom mode indicator
            builder.Services.AddHealthChecks()
                .AddCheck<ApplicationModeHealthCheck>("application_mode", tags: new[] { "mode" });

            // APPLICATION LAYER CONFIGURATION

            // CQRS with MediatR
            builder.Services.AddValidatorsFromAssembly(typeof(SyncBlockchainCommand).Assembly);
            builder.Services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(SyncBlockchainCommand).Assembly);
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            });

            // API LAYER CONFIGURATION

            // Add Controllers with OData support
            builder.Services.AddControllers()
                .AddOData(options => options
                    .AddRouteComponents("odata", ODataConfiguration.GetEdmModel())
                    .Select() // Enable $select
                    .Filter() // Enable $filter
                    .OrderBy() // Enable $orderby
                    .Count() // Enable $count
                    .Expand() // Enable $expand
                    .SetMaxTop(100)); // Max 100 records per request

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "ICMarkets Blockchain API",
                    Version = "v1",
                    Description = "REST API with OData support for querying blockchain data from BlockCypher. Supports filtering, sorting, pagination, and field selection using OData query syntax."
                });

                // Include XML comments
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });

            // CORS Configuration - Uses CorsServiceExtensions
            builder.Services.AddConfiguredCors(builder.Configuration);

            // BUILD THE APP
            var app = builder.Build();

            // HTTP REQUEST PIPELINE

            // Exception Handling Middleware
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            // Database Initialization
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            // Swagger (Development)
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ICMarkets Blockchain API v1");
                    options.RoutePrefix = "swagger";
                });
            }

            // CORS Middleware - Uses the default policy from CorsServiceExtensions
            app.UseCors(CorsServiceExtensions.GetDefaultCorsPolicyName());

            app.UseAuthorization();

            // Map Controllers
            app.MapControllers();

            // Health Check
            app.MapHealthChecks("/health");

            app.Run();
        }
    }
}
