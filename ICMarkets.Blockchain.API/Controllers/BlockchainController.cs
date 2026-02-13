using ICMarkets.Blockchain.API.Configuration;
using ICMarkets.Blockchain.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace ICMarkets.Blockchain.API.Controllers
{
    /// <summary>
    /// OData-enabled controller for blockchain data operations.
    /// Follows CQRS pattern with MediatR for all data operations.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BlockchainController : ODataController
    {
        private readonly IMediator _mediator;
        private readonly ODataSettings _odataSettings;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BlockchainController> _logger;
        private readonly bool _enableSync;

        public BlockchainController(
            IMediator mediator,
            IOptions<ODataSettings> odataSettings,
            IConfiguration configuration,
            ILogger<BlockchainController> logger)
        {
            _mediator = mediator;
            _odataSettings = odataSettings.Value;
            _configuration = configuration;
            _logger = logger;
            _enableSync = configuration.GetValue<bool>("EnableSync", true);

            // Structured logging in constructor
            _logger.LogInformation("BlockchainController initialized. Sync enabled: {EnableSync}", _enableSync);
        }

        /// <summary>
        /// Syncs blockchain data from BlockCypher API for all configured blockchains.
        /// Fetches current state from BTC, ETH, DASH, LTC, DOGE and stores in database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync result with success status and count of records synced</returns>
        /// <response code="200">Successfully synced blockchain data</response>
        /// <response code="503">Sync endpoint disabled in read-only mode</response>
        /// <response code="500">Internal server error during sync</response>
        /// <remarks>
        /// This endpoint is only available when EnableSync configuration is true.
        /// Returns 503 Service Unavailable in read-only mode.
        /// </remarks>
        [HttpPost("sync")]
        [ApiExplorerSettings(IgnoreApi = false)]
        public async Task<IActionResult> SyncData(CancellationToken cancellationToken)
        {
            // Check if sync is enabled
            if (!_enableSync)
            {
                _logger.LogWarning("Sync endpoint called but sync is disabled (read-only mode)");
                return StatusCode(503, new { error = "Sync endpoint disabled in read-only mode" });
            }

            // CQRS Command Pattern
            var result = await _mediator.Send(new SyncBlockchainCommand(), cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Retrieves blockchain history with full OData query support.
        /// Supports filter, orderby, top, skip, select, count for flexible querying.
        /// </summary>
        /// <returns>Collection of blockchain history records</returns>
        /// <response code="200">Successfully retrieved blockchain history</response>
        /// <remarks>
        /// <![CDATA[
        /// Example OData queries:
        ///
        /// GET /api/Blockchain/odata/BlockchainHistory
        /// GET /api/Blockchain/odata/BlockchainHistory?$filter=Symbol eq 'BTC'
        /// GET /api/Blockchain/odata/BlockchainHistory?$orderby=CreatedAt desc&$top=10
        /// GET /api/Blockchain/odata/BlockchainHistory?$filter=Symbol in ('BTC','ETH')&$skip=10&$top=5
        /// GET /api/Blockchain/odata/BlockchainHistory?$select=Symbol,CreatedAt
        ///
        /// Available fields:
        /// - Id (Guid): Unique identifier
        /// - Symbol (string): Blockchain symbol (BTC, ETH, etc.)
        /// - CreatedAt (DateTime): When data was fetched
        /// - RequestUrl (string): BlockCypher API URL used
        /// - Data (string): Raw JSON response from BlockCypher API (supports contains)
        /// ]]>
        /// </remarks>
        [HttpGet("odata/BlockchainHistory")]
        public async Task<IActionResult> GetHistory(
            [SwaggerIgnore] ODataQueryOptions<BlockchainDto> queryOptions,
            CancellationToken cancellationToken)
        {
            var queryable = await _mediator.Send(new GetHistoryQuery(), cancellationToken);

            var validationSettings = new ODataValidationSettings
            {
                MaxTop = _odataSettings.MaxTop,
                AllowedQueryOptions = AllowedQueryOptions.All
            };

            foreach (var property in _odataSettings.AllowedOrderByProperties)
                validationSettings.AllowedOrderByProperties.Add(property);

            queryOptions.Validate(validationSettings);
            if (queryOptions.Top == null)
            {
                queryable = queryable.Take(_odataSettings.MaxTop);
            }

            var querySettings = new ODataQuerySettings
            {
                EnsureStableOrdering = false
            };

            var result = queryOptions.ApplyTo(queryable, querySettings);

            if (result is IQueryable<BlockchainDto> itemsQueryable  && 
                itemsQueryable.Provider is IAsyncQueryProvider)
            {
                var items = await itemsQueryable.ToListAsync(cancellationToken);
                return new JsonResult(items);
            }

            return new JsonResult(result);
        }
    }
}
