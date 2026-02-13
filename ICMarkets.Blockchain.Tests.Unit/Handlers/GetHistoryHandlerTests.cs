using FluentAssertions;
using ICMarkets.Blockchain.Application.Features;
using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Domain.Interfaces;
using Moq;
using System.Text.Json.Nodes;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Handlers
{
    /// <summary>
    /// Unit tests for GetHistoryHandler (CQRS Query Handler) after refactoring.
    /// Tests the handler's ability to return IQueryable for OData processing.
    /// Data property is now a parsed JsonNode instead of a raw JSON string.
    /// </summary>
    public class GetHistoryHandlerTests
    {
        private readonly Mock<IRepository> _mockRepository;
        private readonly GetHistoryHandler _handler;

        public GetHistoryHandlerTests()
        {
            _mockRepository = new Mock<IRepository>();
            _handler = new GetHistoryHandler(_mockRepository.Object);
        }

        [Fact]
        public async Task HandleWithValidRequestReturnsQueryable()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                CreateBlockchainData("BTC", "{\"height\":800000}"),
                CreateBlockchainData("ETH", "{\"height\":900000}")
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeAssignableTo<IQueryable<BlockchainDto>>();

            var materializedResult = result.ToList();
            materializedResult.Should().HaveCount(2);
        }

        [Fact]
        public async Task HandleWithNoDataReturnsEmptyQueryable()
        {
            // Arrange
            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(new List<BlockchainData>().AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ToList().Should().BeEmpty();
        }

        [Fact]
        public async Task HandleMapsBlockchainDataToDtoCorrectly()
        {
            // Arrange
            var createdAt = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);
            var jsonResponse = "{\"name\":\"Bitcoin\",\"height\":800000}";
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Symbol = "BTC",
                    RequestUrl = "https://api.blockcypher.com/v1/btc/main",
                    JsonResponse = jsonResponse,
                    CreatedAt = createdAt
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert
            dto.Id.Should().Be(testData[0].Id);
            dto.Symbol.Should().Be("BTC");
            dto.RequestUrl.Should().Be("https://api.blockcypher.com/v1/btc/main");
            dto.CreatedAt.Should().Be(createdAt);

            // Verify Data is parsed as JsonNode
            dto.Data.Should().NotBeNull();
            dto.Data.Should().BeAssignableTo<JsonNode>();
            dto.Data!["name"]!.GetValue<string>().Should().Be("Bitcoin");
            dto.Data!["height"]!.GetValue<int>().Should().Be(800000);
        }

        [Fact]
        public async Task HandleWithValidJsonReturnsParsedJsonNode()
        {
            // Arrange
            var jsonResponse = "{\"name\":\"Bitcoin\",\"height\":800000,\"hash\":\"abc123\"}";
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Symbol = "BTC",
                    RequestUrl = "https://test.com",
                    JsonResponse = jsonResponse,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert
            dto.Data.Should().NotBeNull();
            dto.Data.Should().BeAssignableTo<JsonObject>();

            // Content checks
            dto.Data!["name"]!.GetValue<string>().Should().Be("Bitcoin");
            dto.Data!["hash"]!.GetValue<string>().Should().Be("abc123");
        }

        [Fact]
        public async Task HandleWithInvalidJsonReturnsErrorObject()
        {
            // Arrange
            var invalidJson = "invalid json";
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Symbol = "BTC",
                    RequestUrl = "https://test.com",
                    JsonResponse = invalidJson,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert - Should return error object with details
            dto.Data.Should().NotBeNull();
            dto.Data.Should().BeAssignableTo<JsonObject>();
            dto.Data!["error"]!.GetValue<string>().Should().Be("JSON parse failed");
            dto.Data!["rawData"]!.GetValue<string>().Should().Be(invalidJson);
        }

        [Fact]
        public async Task HandleWithNullJsonReturnsNull()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Symbol = "BTC",
                    RequestUrl = "https://test.com",
                    JsonResponse = null!,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert
            dto.Data.Should().BeNull();
        }

        [Fact]
        public async Task HandleWithEmptyJsonReturnsNull()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Symbol = "BTC",
                    RequestUrl = "https://test.com",
                    JsonResponse = string.Empty,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert
            dto.Data.Should().BeNull();
        }

        [Fact]
        public async Task HandleReturnsQueryableAllowingODataFiltering()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                CreateBlockchainData("BTC", "{\"height\":800000}"),
                CreateBlockchainData("ETH", "{\"height\":900000}"),
                CreateBlockchainData("BTC", "{\"height\":800001}")
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Apply OData-style filtering
            var btcOnly = result.Where(x => x.Symbol == "BTC").ToList();

            // Assert
            btcOnly.Should().HaveCount(2);
            btcOnly.Should().AllSatisfy(dto => dto.Symbol.Should().Be("BTC"));
        }

        [Fact]
        public async Task HandleReturnsQueryableAllowingODataOrdering()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Symbol = "BTC",
                    RequestUrl = "https://test.com",
                    JsonResponse = "{}",
                    CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)
                },
                new()
                {
                    Symbol = "ETH",
                    RequestUrl = "https://test.com",
                    JsonResponse = "{}",
                    CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                },
                new()
                {
                    Symbol = "DASH",
                    RequestUrl = "https://test.com",
                    JsonResponse = "{}",
                    CreatedAt = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc)
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Apply OData-style ordering
            var ordered = result.OrderBy(x => x.CreatedAt).ToList();

            // Assert
            ordered[0].CreatedAt.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
            ordered[1].CreatedAt.Should().Be(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
            ordered[2].CreatedAt.Should().Be(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public async Task HandleReturnsQueryableAllowingODataPagination()
        {
            // Arrange
            var testData = Enumerable.Range(1, 15)
                .Select(i => CreateBlockchainData("BTC", $"{{\"height\":{800000 + i}}}"))
                .ToList();

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Apply OData-style pagination
            var page2 = result.Skip(5).Take(5).ToList();

            // Assert
            page2.Should().HaveCount(5);
        }

        [Fact]
        public async Task HandleReturnsQueryableAllowingODataProjection()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                CreateBlockchainData("BTC", "{\"height\":800000}"),
                CreateBlockchainData("ETH", "{\"height\":900000}")
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Apply OData-style projection (select)
            var symbolsOnly = result.Select(x => new { x.Symbol, x.CreatedAt }).ToList();

            // Assert
            symbolsOnly.Should().HaveCount(2);
            symbolsOnly[0].Symbol.Should().Be("BTC");
            symbolsOnly[1].Symbol.Should().Be("ETH");
        }

        [Fact]
        public void HandleCallsRepositoryGetQueryableHistory()
        {
            // Arrange
            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(new List<BlockchainData>().AsQueryable());

            // Act
            var result = _handler.Handle(new GetHistoryQuery(), CancellationToken.None);

            // Assert
            _mockRepository.Verify(r => r.GetQueryableHistory(), Times.Once);
        }

        [Fact]
        public async Task HandleWithMultipleSymbolsReturnsAllData()
        {
            // Arrange
            var testData = new List<BlockchainData>
            {
                CreateBlockchainData("BTC", "{\"height\":800000}"),
                CreateBlockchainData("ETH", "{\"height\":900000}"),
                CreateBlockchainData("DASH", "{\"height\":700000}"),
                CreateBlockchainData("LTC", "{\"height\":600000}")
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var allResults = result.ToList();

            // Assert
            allResults.Should().HaveCount(4);
            allResults.Select(x => x.Symbol).Should().Contain(new[] { "BTC", "ETH", "DASH", "LTC" });
        }

        [Fact]
        public async Task HandlePreservesAllProperties()
        {
            // Arrange
            var id = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;
            var testData = new List<BlockchainData>
            {
                new()
                {
                    Id = id,
                    Symbol = "BTC",
                    RequestUrl = "https://api.blockcypher.com/v1/btc/main",
                    JsonResponse = "{\"height\":800000}",
                    CreatedAt = createdAt
                }
            };

            _mockRepository
                .Setup(r => r.GetQueryableHistory())
                .Returns(testData.AsQueryable());

            // Act
            var result = await _handler.Handle(new GetHistoryQuery(), CancellationToken.None);
            var dto = result.First();

            // Assert - All properties correctly mapped
            dto.Id.Should().Be(id);
            dto.Symbol.Should().Be("BTC");
            dto.CreatedAt.Should().Be(createdAt);
            dto.RequestUrl.Should().Be("https://api.blockcypher.com/v1/btc/main");

            // Data is parsed
            dto.Data.Should().NotBeNull();
            dto.Data!["height"]!.GetValue<int>().Should().Be(800000);
        }

        #region Helper Methods

        private static BlockchainData CreateBlockchainData(string symbol, string jsonResponse)
        {
            return new BlockchainData
            {
                Symbol = symbol,
                RequestUrl = $"https://test.com/{symbol.ToLower()}",
                JsonResponse = jsonResponse,
                CreatedAt = DateTime.UtcNow
            };
        }

        #endregion
    }
}
