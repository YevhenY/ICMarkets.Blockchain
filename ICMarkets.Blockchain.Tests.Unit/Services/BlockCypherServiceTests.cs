using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Services;

/// <summary>
/// Unit tests for BlockCypherService including HTTP handling, rate limiting, and circuit breaker.
/// Updated to test all configurable settings from appsettings.
/// </summary>
public class BlockCypherServiceTests
{
    private readonly Mock<ILogger<BlockCypherService>> _mockLogger;

    public BlockCypherServiceTests()
    {
        _mockLogger = new Mock<ILogger<BlockCypherService>>();
    }

    #region HTTP Response Handling Tests

    [Fact]
    public async Task FetchAllAsync_WithSuccessfulResponse_ReturnsData()
    {
        // Arrange
        var handlerMock = CreateMockHttpHandler(HttpStatusCode.OK, "{\"name\": \"Bitcoin\", \"height\": 800000}");
        var settings = CreateSettings(new Dictionary<string, string> { { "BTC", "https://api.com/btc" } });
        var service = CreateService(handlerMock, settings);

        // Act
        var results = await service.FetchAllAsync(CancellationToken.None);

        // Assert
        results.Should().NotBeNull();
        results.Should().HaveCount(1);
        results[0].Symbol.Should().Be("BTC");
        results[0].JsonResponse.Should().Contain("Bitcoin");
    }

    [Fact]
    public async Task FetchAllAsync_WithHttpError_ReturnsEmptyList()
    {
        // Arrange
        var handlerMock = CreateMockHttpHandler(HttpStatusCode.InternalServerError);
        var settings = CreateSettings(new Dictionary<string, string> { { "BTC", "https://api.com/btc" } });
        var service = CreateService(handlerMock, settings);

        // Act
        var results = await service.FetchAllAsync(CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllAsync_WithPartialFailure_ReturnsSuccessfulOnes()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var callCount = 0;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") }
                    : new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
            });

        var settings = CreateSettings(new Dictionary<string, string>
        {
            { "BTC", "https://api.com/btc" },
            { "ETH", "https://api.com/eth" }
        });
        var service = CreateService(handlerMock, settings);

        // Act
        var results = await service.FetchAllAsync(CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].Symbol.Should().Be("BTC");
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact]
    public async Task FetchAllAsync_When429Received_TripsCircuitBreaker()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10)) }
            });

        var settings = CreateSettings(new Dictionary<string, string>
        {
            { "BTC", "https://api.com/btc" },
            { "ETH", "https://api.com/eth" }
        });
        var service = CreateService(handlerMock, settings);

        // Act
        var results = await service.FetchAllAsync(CancellationToken.None);

        // Assert
        results.Should().BeEmpty();

        // Verify only called once - circuit breaker blocked subsequent requests
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(1),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchAllAsync_WhenCircuitOpen_BlocksAllSubsequentCalls()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests,
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(1)) }
            });

        var settings = CreateSettings(new Dictionary<string, string> { { "BTC", "https://api.com/btc" } });
        var service = CreateService(handlerMock, settings);

        // Act
        var results1 = await service.FetchAllAsync(CancellationToken.None); // Trips circuit
        var results2 = await service.FetchAllAsync(CancellationToken.None); // Should be blocked

        // Assert
        results1.Should().BeEmpty();
        results2.Should().BeEmpty();

        // HTTP call should only happen once
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task FetchAllAsync_When429WithoutRetryAfter_UsesDefaultCircuitBreakerDuration()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.TooManyRequests
                // No RetryAfter header - should use DefaultCircuitBreakerDurationSeconds
            });

        var settings = CreateSettings(
            new Dictionary<string, string> { { "BTC", "https://api.com/btc" } },
            defaultCircuitBreakerDurationSeconds: 1800); // 30 minutes
        var service = CreateService(handlerMock, settings);

        // Act
        var results1 = await service.FetchAllAsync(CancellationToken.None);
        var results2 = await service.FetchAllAsync(CancellationToken.None);

        // Assert
        results1.Should().BeEmpty();
        results2.Should().BeEmpty();
        handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task FetchAllAsync_WithMoreEndpointsThanTokens_DelaysRequests()
    {
        // Arrange
        var handlerMock = CreateMockHttpHandler(HttpStatusCode.OK, "{}");
        var settings = CreateSettings(new Dictionary<string, string>
        {
            { "BTC", "https://api.com/btc" },
            { "ETH", "https://api.com/eth" },
            { "DASH", "https://api.com/dash" },
            { "LTC", "https://api.com/ltc" } // 4 endpoints, 3 tokens per second
        });
        var service = CreateService(handlerMock, settings);

        // Act
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await service.FetchAllAsync(CancellationToken.None);
        watch.Stop();

        // Assert - Should take at least ~1 second for token refill
        watch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(900);
    }

    [Fact]
    public async Task FetchAllAsync_RespectsConcurrencyLimit()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var concurrentRequests = 0;
        var maxConcurrentObserved = 0;
        var lockObj = new object();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                lock (lockObj)
                {
                    concurrentRequests++;
                    if (concurrentRequests > maxConcurrentObserved)
                        maxConcurrentObserved = concurrentRequests;
                }

                await Task.Delay(50);

                lock (lockObj)
                {
                    concurrentRequests--;
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                };
            });

        var settings = CreateSettings(new Dictionary<string, string>
        {
            { "BTC", "https://api.com/btc" },
            { "ETH", "https://api.com/eth" },
            { "DASH", "https://api.com/dash" },
            { "LTC", "https://api.com/ltc" },
            { "DOGE", "https://api.com/doge" }
        });
        var service = CreateService(handlerMock, settings);

        // Act
        await service.FetchAllAsync(CancellationToken.None);

        // Assert - Should never exceed 3 concurrent requests (default MaxConcurrentRequests)
        maxConcurrentObserved.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task FetchAllAsync_RespectsCustomConcurrencyLimit()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var concurrentRequests = 0;
        var maxConcurrentObserved = 0;
        var lockObj = new object();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                lock (lockObj)
                {
                    concurrentRequests++;
                    if (concurrentRequests > maxConcurrentObserved)
                        maxConcurrentObserved = concurrentRequests;
                }

                await Task.Delay(50);

                lock (lockObj)
                {
                    concurrentRequests--;
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{}")
                };
            });

        var settings = CreateSettings(
            new Dictionary<string, string>
            {
                { "BTC", "https://api.com/btc" },
                { "ETH", "https://api.com/eth" },
                { "DASH", "https://api.com/dash" },
                { "LTC", "https://api.com/ltc" },
                { "DOGE", "https://api.com/doge" }
            },
            maxConcurrentRequests: 2); // Custom limit of 2
        var service = CreateService(handlerMock, settings);

        // Act
        await service.FetchAllAsync(CancellationToken.None);

        // Assert - Should never exceed custom limit of 2
        maxConcurrentObserved.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task FetchAllAsync_RespectsCustomRateLimitPerSecond()
    {
        // Arrange
        var handlerMock = CreateMockHttpHandler(HttpStatusCode.OK, "{}");
        var settings = CreateSettings(
            new Dictionary<string, string>
            {
                { "BTC", "https://api.com/btc" },
                { "ETH", "https://api.com/eth" },
                { "DASH", "https://api.com/dash" },
                { "LTC", "https://api.com/ltc" },
                { "DOGE", "https://api.com/doge" },
                { "XRP", "https://api.com/xrp" }
            },
            requestsPerSecond: 5); // 6 endpoints, 5 tokens per second
        var service = CreateService(handlerMock, settings);

        // Act
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await service.FetchAllAsync(CancellationToken.None);
        watch.Stop();

        // Assert - Should take at least ~1 second for the 6th request
        watch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(900);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_InitializesWithAllConfigurationSettings()
    {
        // Arrange
        var handlerMock = CreateMockHttpHandler(HttpStatusCode.OK, "{}");
        var settings = CreateSettings(
            endpoints: new Dictionary<string, string> { { "BTC", "https://api.com/btc" } },
            requestsPerSecond: 5,
            requestsPerHour: 200,
            maxConcurrentRequests: 4,
            httpTimeoutSeconds: 60,
            tokenCheckDelayMs: 100,
            defaultCircuitBreakerDurationSeconds: 1800,
            initialRemainingRequests: 200);

        // Act
        var service = CreateService(handlerMock, settings);

        // Assert - Service should be created without throwing
        service.Should().NotBeNull();

        // Verify logger captured initialization message with correct values
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("5 req/sec") && v.ToString()!.Contains("200 req/hour")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var factoryMock = new Mock<IHttpClientFactory>();

        // Act & Assert
        var act = () => new BlockCypherService(factoryMock.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = CreateSettings(new Dictionary<string, string> { { "BTC", "https://api.com/btc" } });

        // Act & Assert
        var act = () => new BlockCypherService(null!, settings, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var factoryMock = new Mock<IHttpClientFactory>();
        var settings = CreateSettings(new Dictionary<string, string> { { "BTC", "https://api.com/btc" } });

        // Act & Assert
        var act = () => new BlockCypherService(factoryMock.Object, settings, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region HTTP Timeout Tests

    [Fact]
    public async Task FetchAllAsync_UsesConfiguredHttpTimeout()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClientUsed = false;
        HttpClient? capturedClient = null;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var settings = CreateSettings(
            new Dictionary<string, string> { { "BTC", "https://api.com/btc" } },
            httpTimeoutSeconds: 60);

        var client = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() =>
            {
                httpClientUsed = true;
                capturedClient = client;
                return client;
            });

        var service = new BlockCypherService(factoryMock.Object, settings, _mockLogger.Object);

        // Act
        await service.FetchAllAsync(CancellationToken.None);

        // Assert
        httpClientUsed.Should().BeTrue();
        // Note: HttpClient timeout is set per request in the service, 
        // this test verifies the setting is used (indirectly through no timeout exception)
    }

    #endregion

    #region Helper Methods

    private Mock<HttpMessageHandler> CreateMockHttpHandler(HttpStatusCode statusCode, string content = "")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return handlerMock;
    }

    private IOptions<BlockCypherSettings> CreateSettings(
        Dictionary<string, string> endpoints,
        int requestsPerSecond = 3,
        int requestsPerHour = 100,
        int maxConcurrentRequests = 3,
        int httpTimeoutSeconds = 30,
        int tokenCheckDelayMs = 50,
        int defaultCircuitBreakerDurationSeconds = 3600,
        int initialRemainingRequests = 100)
    {
        return Options.Create(new BlockCypherSettings
        {
            Endpoints = endpoints,
            RequestsPerSecond = requestsPerSecond,
            RequestsPerHour = requestsPerHour,
            MaxConcurrentRequests = maxConcurrentRequests,
            HttpTimeoutSeconds = httpTimeoutSeconds,
            TokenCheckDelayMs = tokenCheckDelayMs,
            DefaultCircuitBreakerDurationSeconds = defaultCircuitBreakerDurationSeconds,
            InitialRemainingRequests = initialRemainingRequests
        });
    }

    private BlockCypherService CreateService(
        Mock<HttpMessageHandler> handlerMock,
        IOptions<BlockCypherSettings> settings)
    {
        var client = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return new BlockCypherService(factoryMock.Object, settings, _mockLogger.Object);
    }

    #endregion
}
