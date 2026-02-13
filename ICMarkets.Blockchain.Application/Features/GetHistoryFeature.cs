using ICMarkets.Blockchain.Domain.Interfaces;
using MediatR;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ICMarkets.Blockchain.Application.Features
{
    /// <summary>
    /// Data Transfer Object for blockchain history records exposed via OData.
    /// </summary>
    public class BlockchainDto
    {
        /// <summary>
        /// Unique identifier for the blockchain record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Blockchain symbol (e.g., BTC, ETH, DASH, LTC).
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the data was fetched from BlockCypher API.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The URL that was called to fetch this data.
        /// </summary>
        public string RequestUrl { get; set; } = string.Empty;

        /// <summary>
        /// row data stored as it is from the BlockCypher API response.
        /// </summary>
        public JsonNode? Data { get; set; }
    }

    /// <summary>
    /// Query to retrieve blockchain history as IQueryable for OData processing.
    /// </summary>
    public class GetHistoryQuery : IRequest<IQueryable<BlockchainDto>>
    {
        // No parameters - OData will handle filtering, paging, sorting, etc.
    }

    /// <summary>
    /// Handler for retrieving blockchain history.
    /// Returns IQueryable to allow OData query composition.
    /// </summary>
    public class GetHistoryHandler : IRequestHandler<GetHistoryQuery, IQueryable<BlockchainDto>>
    {
        private readonly IRepository _repository;

        public GetHistoryHandler(IRepository repository)
        {
            _repository = repository;
        }

        public Task<IQueryable<BlockchainDto>> Handle(GetHistoryQuery request, CancellationToken cancellationToken)
        {
            // Get queryable data from repository
            var data = _repository.GetQueryableHistory();

            // Map to DTOs
            var dtos = data.Select(e => new BlockchainDto
            {
                Id = e.Id,
                Symbol = e.Symbol,
                CreatedAt = e.CreatedAt,
                RequestUrl = e.RequestUrl,
                Data = TryParseJson(e.JsonResponse)
            });

            return Task.FromResult(dtos);
        }

        private static JsonNode? TryParseJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                return new JsonObject
                {
                    ["error"] = "JSON parse failed",
                    ["rawData"] = json,
                    ["message"] = ex.Message
                };
            }
        }
    }
}
