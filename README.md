ICMarkets Blockchain Web API - Technical Assessment
====================================================================

Project Overview
--------------------------------------------------------------------

This .NET 8.0 Web API solution demonstrates enterprise-grade 
blockchain data aggregation and querying capabilities with optional 
microservices architecture. The application fetches real-time 
blockchain information from the BlockCypher API for multiple 
cryptocurrencies (BTC, ETH, DASH, LTC) and provides advanced OData 
query capabilities with historical data tracking.

The implementation showcases Clean Architecture principles, multiple 
design patterns, comprehensive testing strategies, production-ready 
containerization with Docker on Linux, and optional API Gateway 
pattern for horizontal scaling and load balancing.


Requirements Compliance
--------------------------------------------------------------------

This project fulfills specified technical requirements:

1. .NET Core Application Architecture
   - Built with .NET 8.0 targeting Linux containers
   - Clean Architecture with vertical slicing
   - SOLID principles applied throughout all layers
   - Clear separation of concerns across Domain, Application, 
     Infrastructure, API, Gateway, and Worker layers

2. API Endpoints with Swagger Documentation
   - All HTTP endpoints exposed via Swagger/OpenAPI
   - Interactive API documentation available at /swagger
   - Comprehensive XML documentation for all public interfaces
   - Request/response models with validation annotations

3. Database Storage with Timestamps
   - SQLite database with Entity Framework Core 8.0
   - CreatedAt timestamp automatically recorded for all entries
   - Historical data persisted with full audit trail
   - Composite indexing on Symbol and CreatedAt for performance

4. Historical Data Query with Default Ordering
   - History endpoint returns data sorted by CreatedAt descending
   - Most recent blockchain snapshots appear first by default
   - Default ordering implemented at repository level
   - Clients can override with explicit OData orderby parameter

5. Health Checks and CORS Configuration
   - Health check endpoint at /health with database connectivity
   - Configurable CORS policy supporting multiple origins
   - Preflight request caching with configurable max age
   - Environment-specific CORS settings

6. Dependency Injection and Pipeline Features
   - Built-in ASP.NET Core DI container configuration
   - Structured logging with Serilog (Console and File sinks)
   - AutoMapper equivalent manual mapping in handlers
   - JSON serialization with System.Text.Json
   - FluentValidation with automatic pipeline behavior
   - MediatR pipeline behaviors for logging and validation

7. Testing Projects
   - Unit Tests: ICMarkets.Blockchain.Tests.Unit
   - Integration Tests: ICMarkets.Blockchain.Tests.Integration
   - Functional Tests: ICMarkets.Blockchain.Tests.Functional
   - Code coverage with Coverlet
   - Test frameworks: xUnit, FluentAssertions, Moq

8. Runtime Profiles on .NET and Docker (Linux)
   - Multiple launch profiles in launchSettings.json
   - Docker multi-stage builds for optimized images
   - Development and Production runtime configurations
   - Docker Compose orchestration for all deployment modes
   - Linux base images (mcr.microsoft.com/dotnet/aspnet:8.0)
   - Environment-specific behavior and logging levels

9. Optional: API Gateway (IMPLEMENTED)
   - YARP (Yet Another Reverse Proxy) implementation
   - Load balancing with Round Robin strategy
   - Rate limiting (60 requests/minute per IP)
   - Health check integration with backend services
   - Support for multiple API instances with shared database
   - Dedicated background worker for BlockCypher sync
   - Three deployment architectures available


Solution Architecture
--------------------------------------------------------------------

The solution follows Clean Architecture with six distinct layers:

ICMarkets.Blockchain.Domain
  - Core business entities: BlockchainData
  - Repository interfaces: IRepository
  - Service interfaces: IBlockCypherService
  - Domain validation rules and constants
  - No external dependencies

ICMarkets.Blockchain.Application
  - CQRS implementation with MediatR
  - Commands: SyncBlockchainCommand (write operations)
  - Queries: GetHistoryQuery (read operations)
  - DTOs: BlockchainDto with manual mapping
  - FluentValidation rules for all commands
  - Pipeline behaviors: ValidationBehaviour, LoggingBehaviour

ICMarkets.Blockchain.Infrastructure
  - Entity Framework Core with SQLite provider
  - Repository implementation: BlockchainRepository
  - BlockCypher service with rate limiting
  - Token bucket pattern for API throttling
  - Circuit breaker for resilient external calls
  - Database migrations and configuration

ICMarkets.Blockchain.API
  - ASP.NET Core 8.0 Web API controllers
  - OData v9.0 integration for advanced querying
  - Swagger/OpenAPI documentation generation
  - Serilog structured logging configuration
  - Health checks and CORS middleware
  - Exception handling middleware
  - Configurable read-only mode support

ICMarkets.Blockchain.Gateway (Optional)
  - YARP reverse proxy for load balancing
  - Round Robin distribution across API instances
  - User-level rate limiting (60 requests/minute per IP)
  - Active health checks for backend services
  - Request routing and aggregation
  - TLS termination support

ICMarkets.Blockchain.Worker (Optional)
  - Background service for periodic BlockCypher sync
  - PeriodicTimer-based scheduling (configurable interval)
  - MediatR integration for command execution
  - Serilog structured logging
  - Graceful shutdown with CancellationToken
  - Isolated from read API instances


Architecture Deployment Modes
--------------------------------------------------------------------

The solution supports three distinct deployment architectures to
demonstrate scalability and separation of concerns:


Architecture Level 1: Single Instance (Development)
--------------------------------------------------------------------

```
┌─────────┐
│ Clients │
└────┬────┘
     │
┌────▼────────────────────┐
│   API Instance          │
│ - Handles Requests      │
│ - BlockCypher Sync      │
│ - OData Queries         │
└────┬────────────────────┘
     │
┌────▼────────────────────┐
│   SQLite Database       │
└─────────────────────────┘
```

Description:
  Single container deployment with all functionality in one service.
  Suitable for development, testing, and low-traffic scenarios.

Features:
  - Direct client access to API
  - Swagger UI enabled
  - All endpoints available (sync + query)
  - Minimal resource requirements
  - Simple debugging and development

Deployment:
  docker-compose up -d

Access Points:
  - API/Swagger: http://localhost:8080
  - Health: http://localhost:8080/health
  - OData: http://localhost:8080/api/Blockchain/odata/BlockchainHistory
  - Sync: POST http://localhost:8080/api/blockchain/sync


Architecture Level 2: Gateway with Multiple Writers (Scaling)
--------------------------------------------------------------------
```
  ┌─────────┐
  │ Clients │
  └────┬────┘
       │
  ┌────▼─────────────────────┐
  │   API Gateway (YARP)     │
  │   - Load Balancing       │
  │   - Rate Limiting        │
  │   - Health Checks        │
  └────┬─────────────────────┘
       │
       ├────────────┬────────────┐
       │            │            │
  ┌────▼────┐  ┌───▼─────┐  ┌──▼──────┐
  │ API #1  │  │ API #2  │  │ API #N  │
  │ (Write) │  │ (Write) │  │ (Write) │
  └────┬────┘  └───┬─────┘  └──┬──────┘
       │           │            │
       └───────────┴────────────┘
                   │
          ┌────────▼────────┐
          │ SQLite Database │
          │   (Shared)      │
          └─────────────────┘
```

Description:
  Multiple API instances behind gateway with shared database. Each
  instance can perform both read and write operations. Provides
  horizontal scaling for increased request throughput.

Features:
  - Load balanced request distribution
  - Gateway-level rate limiting per IP
  - Active health monitoring of backends
  - Each API instance can sync BlockCypher
  - Suitable for moderate traffic with coordination

Limitations:
  - Multiple instances competing for BlockCypher rate limits
  - Potential for duplicate sync operations
  - Shared rate limit pool across instances

Deployment:
  docker-compose -f docker-compose.gateway.yml up -d
  (Development mode with 2 API instances)

  docker-compose -f docker-compose.gateway.prod.yml up -d
  (Production mode with 2 API instances)

Access Points:
  - Gateway: http://localhost:5000 (dev) or http://localhost:9000 (prod)
  - Health: http://localhost:5000/health
  - OData: http://localhost:5000/api/Blockchain/odata/BlockchainHistory
  - Sync: POST http://localhost:5000/api/blockchain/sync


Architecture Level 3: Gateway + Worker (Production Microservices)
--------------------------------------------------------------------
```
  ┌─────────┐
  │ Clients │
  └────┬────┘
       │
  ┌────▼─────────────────────┐
  │   API Gateway (YARP)     │
  │   - Load Balancing       │
  │   - Rate Limiting        │
  │   - Health Checks        │
  └────┬─────────────────────┘
       │
       ├────────────┬────────────┬────────────┐
       │            │            │            │
  ┌────▼────┐  ┌───▼─────┐  ┌──▼──────┐  ┌──▼──────┐
  │ API #1  │  │ API #2  │  │ API #3  │  │ API #N  │
  │ (Read)  │  │ (Read)  │  │ (Read)  │  │ (Read)  │
  └────┬────┘  └───┬─────┘  └──┬──────┘  └──┬──────┘
       │           │            │            │
       └───────────┴────────────┴────────────┘
                   │                         │
          ┌────────▼────────┐               │
          │ SQLite Database │◄──────────────┘
          │   (Shared)      │
          └────────▲────────┘
                   │
          ┌────────┴────────┐
          │  Sync Worker    │
          │  (Background)   │
          │  - BlockCypher  │
          │  - Periodic Job │
          └─────────────────┘
```

Description:
  Optimal production architecture with read-write separation. API
  instances serve queries only (read-only mode). Dedicated background
  worker handles BlockCypher synchronization exclusively.

Features:
  - True read-write separation (CQRS at infrastructure level)
  - API instances horizontally scalable without sync conflicts
  - Single worker respects BlockCypher rate limits efficiently
  - Gateway provides unified entry point
  - Worker operates independently on schedule

Benefits:
  - No BlockCypher rate limit competition between instances
  - API instances are stateless and lightweight
  - Worker can be scaled vertically for sync performance
  - Clear separation of concerns
  - Optimal resource utilization

Deployment:
  # Start gateway with read-only APIs
  docker-compose -f docker-compose.gateway.prod.yml up -d
  
  # Start background sync worker separately
  docker-compose -f docker-compose.worker.prod.yml up -d

Access Points:
  - Gateway: http://localhost:9000
  - Health: http://localhost:9000/health
  - OData: http://localhost:9000/api/Blockchain/odata/BlockchainHistory
  - Sync: Not exposed (handled by worker automatically)

Worker Configuration:
  - Sync Interval: Configurable in appsettings (default 3 minutes)
  - Isolated logging in ./data/prod/worker/logs
  - Automatic retry on failures
  - Circuit breaker integration


BlockCypher API Integration
--------------------------------------------------------------------

The application integrates with five BlockCypher API endpoints:

1. https://api.blockcypher.com/v1/eth/main (Ethereum)
2. https://api.blockcypher.com/v1/dash/main (Dash)
3. https://api.blockcypher.com/v1/btc/main (Bitcoin)
4. https://api.blockcypher.com/v1/btc/test3 (Bitcoin Testnet)
5. https://api.blockcypher.com/v1/ltc/main (Litecoin)

Rate Limiting Configuration:
  - 3 requests per second (token bucket)
  - 100 requests per hour (BlockCypher free tier)
  - Automatic circuit breaker on HTTP 429 responses
  - Configurable timeout and retry policies

Data Storage:
  - Complete JSON response preserved in database
  - CreatedAt timestamp in UTC for temporal queries
  - RequestUrl stored for traceability
  - Symbol field for filtering by blockchain type


API Endpoints
--------------------------------------------------------------------

1. Health Check
   Method: GET
   Path: /health
   Description: Verifies database connectivity and service health
   Response: {"status":"Healthy","duration":"00:00:00.123"}

2. Sync Blockchain Data
   Method: POST
   Path: /api/blockchain/sync
   Description: Fetches current data from all BlockCypher endpoints
   Response: {"success":true,"count":5}
   Note: Only available in write mode (Level 1, Level 2)
         Disabled in Level 3 read-only APIs

3. Query Blockchain History (OData)
   Method: GET
   Path: /api/Blockchain/odata/BlockchainHistory
   Description: Retrieves historical data with OData query support
   Features: Filtering, sorting, pagination, projection, counting


OData Query Capabilities
--------------------------------------------------------------------

The /api/Blockchain/odata/BlockchainHistory endpoint supports OData 
v4 query syntax for flexible data retrieval. All queries respect the 
default descending order by CreatedAt timestamp unless explicitly 
overridden.

Default Behavior (No Query Parameters):

  GET /api/Blockchain/odata/BlockchainHistory

  Returns all blockchain history records with:
  - Ordering: CreatedAt DESC (newest records first)
  - Client-driven paging using $top and $skip 
  - All fields: Id, Symbol, CreatedAt, RequestUrl, Data

  Important: The default descending order by CreatedAt ensures the 
  most recent blockchain snapshots appear first. This ordering is 
  implemented at the repository level in GetQueryableHistory method.

Filter by Single Symbol:

  GET /api/Blockchain/odata/BlockchainHistory?$filter=Symbol eq 'BTC'

  Returns only Bitcoin blockchain records.

Filter by Multiple Symbols:

  GET /api/Blockchain/odata/BlockchainHistory?
      $filter=Symbol in ('BTC','ETH')

  Returns records for Bitcoin and Ethereum blockchains.

Date Range Filtering:

  GET /api/Blockchain/odata/BlockchainHistory?
      $filter=CreatedAt gt 2026-02-01T00:00:00Z

  Returns records created after specified date.

Complex Filtering (Multiple Conditions):

  GET /api/Blockchain/odata/BlockchainHistory?
      $filter=Symbol eq 'BTC' and CreatedAt gt 2026-02-01T00:00:00Z

  Returns Bitcoin records created after the specified date.

Explicit Sorting (Override Default):

  GET /api/Blockchain/odata/BlockchainHistory?$orderby=CreatedAt asc

  Returns oldest records first, overriding default descending order.

Sorting by Multiple Fields:

  GET /api/Blockchain/odata/BlockchainHistory?
      $orderby=Symbol asc,CreatedAt desc

  Sorts alphabetically by symbol, then by time descending within 
  each symbol group.

Pagination (First Page):

  GET /api/Blockchain/odata/BlockchainHistory?$top=5

  Returns first 5 records (newest first by default).

Pagination (Skip and Take):

  GET /api/Blockchain/odata/BlockchainHistory?$skip=10&$top=5

  Skips first 10 records, returns next 5 records.

Field Projection (Select):

  GET /api/Blockchain/odata/BlockchainHistory?
      $select=Symbol,CreatedAt

  Returns only Symbol and CreatedAt fields, reducing payload size.

Combined Query (Filter, Sort, Page, Select):

  GET /odata/BlockchainHistory?$filter=Symbol eq 'BTC'&
      $orderby=CreatedAt desc&$top=10&
      $select=Symbol,CreatedAt,Data

  Comprehensive query demonstrating multiple OData features:
  - Filters to Bitcoin only
  - Sorts newest first (explicit override of default)
  - Returns up to 10 records (capped by MaxTop)
  - Projects only Symbol, CreatedAt, and Data fields


String Functions (Contains):

  GET /api/Blockchain/odata/BlockchainHistory?
      $filter=contains(Symbol,'BT')

  Returns records where Symbol contains 'BT' (BTC, BTCTEST).

String Functions (StartsWith):

  GET /api/Blockchain/odata/BlockchainHistory?
      $filter=startswith(Symbol,'BTC')

  Returns records where Symbol starts with 'BTC'.


OData Query Constraints

  - MaxTop: 100 (maximum records per request; $top is validated server-side)
  - Paging: Client-driven via $top and $skip. Clients must specify $top for predictable page sizes.
  - AllowedOrderByProperties: Symbol, CreatedAt, Id
  - AllowedLogicalOperators: eq, ne, gt, ge, lt, le, and, or
  - AllowedFunctions: contains, startswith, endswith
  - Arithmetic operators: Disabled for security



Database Schema
--------------------------------------------------------------------

Table: BlockchainData

Column          Type              Constraints
------          ----              -----------
Id              UNIQUEIDENTIFIER  PRIMARY KEY (Sequential GUID)
Symbol          NVARCHAR(20)      NOT NULL
RequestUrl      NVARCHAR(500)     NOT NULL
JsonResponse    NVARCHAR(MAX)     NOT NULL
CreatedAt       DATETIME          NOT NULL

Indexes:
  - PRIMARY KEY on Id (Clustered)
  - COMPOSITE INDEX on (Symbol, CreatedAt) for efficient filtering
    and sorting operations

Migration Strategy:
  - EF Core Code-First Migrations
  - SQLite for all deployment modes
  - Shared database file across multiple API instances


Solution Structure
--------------------------------------------------------------------

The complete solution contains the following projects:

ICMarkets.Blockchain.Domain/
  - Entities/
    - BlockchainData.cs
  - Interfaces/
    - IRepository.cs
    - IBlockCypherService.cs
  - ICMarkets.Blockchain.Domain.csproj

ICMarkets.Blockchain.Application/
  - Commands/
    - SyncBlockchainCommand.cs
    - SyncBlockchainHandler.cs
    - SyncBlockchainValidator.cs
  - Queries/
    - GetHistoryQuery.cs
    - GetHistoryHandler.cs
  - DTOs/
    - BlockchainDto.cs
  - Behaviors/
    - ValidationBehaviour.cs
    - LoggingBehaviour.cs
  - ICMarkets.Blockchain.Application.csproj

ICMarkets.Blockchain.Infrastructure/
  - Data/
    - AppDbContext.cs
    - Migrations/
  - Repositories/
    - BlockchainRepository.cs
  - Services/
    - BlockCypherService.cs
  - Configuration/
    - BlockCypherSettings.cs
    - RepositorySettings.cs
    - ODataSettings.cs
  - Validators/
    - BlockCypherSettingsValidator.cs
    - RepositorySettingsValidator.cs
    - ODataSettingsValidator.cs
  - ICMarkets.Blockchain.Infrastructure.csproj

ICMarkets.Blockchain.API/
  - Controllers/
    - BlockchainController.cs
  - Configuration/
    - ODataConfiguration.cs
  - Properties/
    - launchSettings.json
  - appsettings.json
  - appsettings.Development.json
  - appsettings.Production.json
  - Program.cs
  - Dockerfile
  - ICMarkets.Blockchain.API.csproj

ICMarkets.Blockchain.Gateway/
  - appsettings.json
  - appsettings.Development.json
  - appsettings.Production.json
  - Program.cs
  - Dockerfile
  - ICMarkets.Blockchain.Gateway.csproj

ICMarkets.Blockchain.Worker/
  - Workers/
    - BlockchainSyncWorker.cs
  - appsettings.json
  - appsettings.Development.json
  - appsettings.Production.json
  - Program.cs
  - Dockerfile.Worker
  - ICMarkets.Blockchain.Worker.csproj

ICMarkets.Blockchain.Tests.Unit/
  - Handlers/
    - SyncBlockchainHandlerTests.cs
  - Validators/
    - BlockCypherSettingsValidatorTests.cs
    - RepositorySettingsValidatorTests.cs
    - ODataSettingsValidatorTests.cs
    - SymbolValidatorTests.cs
    - SyncBlockchainValidatorTests.cs
  - ICMarkets.Blockchain.Tests.Unit.csproj

ICMarkets.Blockchain.Tests.Integration/
  - Repositories/
    - BlockchainRepositoryTests.cs
  - ICMarkets.Blockchain.Tests.Integration.csproj

ICMarkets.Blockchain.Tests.Functional/
  - API/
    - BlockchainApiTests.cs
  - ICMarkets.Blockchain.Tests.Functional.csproj

Root Directory Files:
  - ICMarkets.Blockchain.sln
  - docker-compose.yml (Level 1: Single instance)
  - docker-compose.prod.yml (Level 1: Production single instance)
  - docker-compose.gateway.yml (Level 2: Gateway dev with 2 APIs)
  - docker-compose.gateway.prod.yml (Level 2: Gateway prod with 2 APIs)
  - docker-compose.worker.yml (Level 3: Worker development)
  - docker-compose.worker.prod.yml (Level 3: Worker production)
  - README.md


Docker Compose Configurations
--------------------------------------------------------------------

docker-compose.yml (Development - Single Instance):
  Deploys single API container with full functionality (read+write).
  Includes Swagger UI, verbose logging, and development database.
  Exposes ports 8080 (HTTP) and 8081 (HTTPS).

docker-compose.prod.yml (Production - Single Instance):
  Production-optimized single API container on port 9080.
  Minimal logging, Swagger disabled, release build configuration.

docker-compose.gateway.yml (Development - Gateway + 2 APIs):
  Gateway container with YARP load balancing across 2 API instances.
  Gateway exposed on port 5000, API instances on internal network.
  All containers in write mode with shared development database.
  Suitable for testing load balancing and multi-instance behavior.

docker-compose.gateway.prod.yml (Production - Gateway + 2 APIs):
  Production gateway deployment with 2 backend API instances.
  Gateway exposed on port 9000, release builds, minimal logging.
  Shared production database with separate log volumes per instance.

docker-compose.worker.yml (Development - Background Worker):
  Standalone background worker for periodic BlockCypher sync.
  Connects to shared development database, verbose logging.
  Operates independently of API instances, configurable sync interval.

docker-compose.worker.prod.yml (Production - Background Worker):
  Production background worker with release build and minimal logging.
  Designed to run alongside gateway deployment (Level 3 architecture).
  Shares production database, isolated worker log volume.


Setup Instructions
--------------------------------------------------------------------

Prerequisites:
  - .NET 8.0 SDK or later
  - Docker Desktop (optional for containerization)
  - Git for version control

Clone Repository:

  git clone https://github.com/YevhenY/ICMarkets.Blockchain.git
  cd ICMarkets.Blockchain

Restore Dependencies:

  dotnet restore

Run Application (Local Development):

  dotnet run --project ICMarkets.Blockchain.API

  Access points:
  - Swagger: https://localhost:7201/swagger
  - API: https://localhost:7201
  - Health: https://localhost:7201/health
  - OData: https://localhost:7201/api/Blockchain/odata/BlockchainHistory

Run Application (Docker - Level 1: Single Instance):

  docker-compose up -d

  Access points:
  - Swagger: http://localhost:8080/swagger
  - API: http://localhost:8080
  - Health: http://localhost:8080/health
  - OData: http://localhost:8080/api/Blockchain/odata/BlockchainHistory

Run Application (Docker - Level 2: Gateway with Multiple Writers):

  Development:
    docker-compose -f docker-compose.gateway.yml up -d

  Production:
    docker-compose -f docker-compose.gateway.prod.yml up -d

  Access points (dev):
  - Gateway: http://localhost:5000
  - Health: http://localhost:5000/health
  - OData: http://localhost:5000/api/Blockchain/odata/BlockchainHistory

  Access points (prod):
  - Gateway: http://localhost:9000
  - Health: http://localhost:9000/health
  - OData: http://localhost:9000/api/Blockchain/odata/BlockchainHistory

Run Application (Docker - Level 3: Gateway + Worker):

  # Start gateway with read-only API instances
  docker-compose -f docker-compose.gateway.prod.yml up -d

  # Start background worker for sync
  docker-compose -f docker-compose.worker.prod.yml up -d

  Access points:
  - Gateway: http://localhost:9000
  - Health: http://localhost:9000/health
  - OData: http://localhost:9000/api/Blockchain/odata/BlockchainHistory
  - Sync endpoint: Not exposed (automatic background sync)

  Worker monitoring:
  - View logs: docker-compose -f docker-compose.worker.prod.yml logs -f
  - Check sync status: Logs show periodic sync attempts every 3 minutes


Testing
--------------------------------------------------------------------

Run All Tests:

  dotnet test

Run Unit Tests:

  dotnet test ICMarkets.Blockchain.Tests.Unit

Run Integration Tests:

  dotnet test ICMarkets.Blockchain.Tests.Integration

Run Functional Tests:

  dotnet test ICMarkets.Blockchain.Tests.Functional

Generate Code Coverage:

  dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov


Design Patterns Implementation
--------------------------------------------------------------------

1. CQRS (Command Query Responsibility Segregation)
   Implementation:
   - Commands: SyncBlockchainCommand (write operations)
   - Queries: GetHistoryQuery (read operations)
   Benefit: Clear separation of read and write concerns with
            optimized query models

2. Repository Pattern
   Implementation:
   - Interface: IRepository in Domain layer
   - Implementation: BlockchainRepository in Infrastructure layer
   - Methods: AddRangeAsync, GetQueryableHistory
   Benefit: Data access abstraction enabling testability and
            flexibility to change data stores

3. Unit of Work Pattern
   Implementation:
   - DbContext serves as Unit of Work
   - Transactional consistency via SaveChangesAsync
   Benefit: Ensures atomic database operations across multiple
            repository calls

4. Pipeline Pattern (MediatR Behaviors)
   Implementation:
   - ValidationBehaviour: Pre-execution validation with
     FluentValidation
   - LoggingBehaviour: Request and response logging
   Benefit: Cross-cutting concerns handled uniformly across all
            requests without cluttering handlers

5. Circuit Breaker Pattern
   Implementation:
   - BlockCypherService tracks consecutive API failures
   - Circuit opens on HTTP 429 rate limit responses
   - Automatic recovery after timeout period
   Benefit: Protects against cascading failures and respects
            external API rate limits

6. Token Bucket Pattern
   Implementation:
   - Per-second rate limiting: 3 requests/second
   - Hourly request tracking: 100 requests/hour
   - Semaphore-based concurrent request limiting
   Benefit: Smooth rate limiting without traffic bursts while
            respecting BlockCypher API quotas

7. API Gateway Pattern (Optional)
   Implementation:
   - YARP reverse proxy for request routing
   - Round Robin load balancing across backends
   - Active health checks and automatic failover
   - Gateway-level rate limiting per client IP
   Benefit: Centralized entry point, horizontal scaling support,
            traffic management, and request aggregation

8. Background Service Pattern (Optional)
   Implementation:
   - PeriodicTimer for scheduled BlockCypher sync
   - MediatR integration for command execution
   - Graceful shutdown with CancellationToken
   Benefit: Isolated sync operations, efficient rate limit usage,
            separation from request-serving instances


Asynchronous Programming Patterns
--------------------------------------------------------------------

Task-Based Asynchronous Pattern (TAP):
  - All I/O operations use async/await keywords
  - CancellationToken support throughout request pipeline
  - Proper exception handling in async contexts
  - ConfigureAwait(false) for library code

Parallel Execution:
  - Task.WhenAll for concurrent blockchain endpoint fetching
  - SemaphoreSlim for controlled concurrency (max 3 simultaneous)
  - Thread-safe collections (ConcurrentBag) for result aggregation

Example from BlockCypherService:

  var tasks = endpoints.Select(async endpoint =>
  {
      await semaphore.WaitAsync(cancellationToken);
      try
      {
          return await FetchDataAsync(endpoint, cancellationToken);
      }
      finally
      {
          semaphore.Release();
      }
  });
  return await Task.WhenAll(tasks);


Dependency Injection Configuration
--------------------------------------------------------------------

Service Lifetimes:

Scoped Services:
  - AppDbContext: Per-request database context
  - IRepository: Per-request repository instance
  Reason: Database contexts should not be shared across requests

Singleton Services:
  - IBlockCypherService: Shared rate limiting state
  - ILogger: Logging infrastructure
  - IValidateOptions: Configuration validators
  Reason: Stateful services requiring shared state or expensive
          initialization

Transient Services:
  - MediatR handlers: Command and query handlers
  - FluentValidation validators: Request validators
  Reason: Stateless services with no shared state

Configuration Binding:
  - IOptions<T> pattern for strongly-typed settings
  - Validation on application startup with ValidateOnStart
  - Environment-specific overrides via appsettings files


Logging Configuration
--------------------------------------------------------------------

Serilog Structured Logging:

Development Environment:
  - Console sink: Colored output for development visibility
  - File sink: Rolling daily log files in /app/logs directory
  - Minimum level: Verbose (includes Debug messages)
  - Format: Human-readable text with timestamps

Production Environment:
  - Console sink: Plain text for container log aggregation
  - File sink: Rolling daily log files in /app/logs directory
  - Minimum level: Information (excludes Debug/Verbose)
  - Format: Structured JSON for log analysis tools

Log Enrichers:
  - Request correlation ID for tracing requests
  - Environment name (Development/Production)
  - Machine name for distributed deployments
  - Thread ID for concurrency debugging

Log Categories:
  - HTTP requests and responses with status codes
  - MediatR command and query execution with timing
  - Entity Framework Core database queries
  - BlockCypher API calls with rate limit tracking
  - Exception details with stack traces
  - YARP proxy requests and routing decisions (Gateway)
  - Worker background job execution and scheduling


Production Considerations
--------------------------------------------------------------------

Data Persistence:
  - Docker volumes mounted for database and logs
  - Development data: ./data/dev/data
  - Production data: ./data/prod/data
  - Data survives container recreation and updates
  - Shared database volume across API instances in Level 2/3

Database Backup:
  - SQLite database file accessible on host filesystem
  - Simple file-based backup strategy: copy blockchain.sqlite
  - Backup before major updates or schema migrations
  - Consider scheduled backups for production deployments

Security:
  - Non-root container user (APP)
  - CORS configuration with explicit origins in production
  - No sensitive data in environment variables (use secrets)
  - HTTPS termination recommended at load balancer level
  - Gateway provides single attack surface for external access
  - Internal API instances not exposed to public network

Monitoring:
  - Health check endpoint for orchestration platforms
  - Structured logging for centralized log aggregation
  - Gateway logs all routing decisions and backend health
  - Worker logs sync operations and BlockCypher rate usage
  - Application Insights integration ready (add package)
  - Metrics endpoints can be added with Prometheus

Scaling:
  - Level 1: Vertical scaling (increase container resources)
  - Level 2: Horizontal scaling (add API instances to gateway)
  - Level 3: Optimal scaling (add read API instances, scale
    worker vertically if needed)
  - Stateless API design allows unlimited horizontal scaling
  - Worker should remain single instance to respect rate limits
  - Gateway can be scaled with external load balancer if needed


Contact
--------------------------------------------------------------------

Repository: https://github.com/YevhenY/ICMarkets.Blockchain
