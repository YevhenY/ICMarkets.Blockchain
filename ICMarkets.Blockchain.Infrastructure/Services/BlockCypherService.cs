using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Domain.Interfaces;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.Infrastructure.Services
{
    /// <summary>
    /// Service for fetching blockchain data from BlockCypher API with advanced rate limiting.
    /// Implements Token Bucket algorithm, hourly tracking, and circuit breaker pattern.
    /// All configuration values are externalized to appsettings.json
    /// </summary>
    public class BlockCypherService : IBlockCypherService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Dictionary<string, string> _endpoints;
        private readonly BlockCypherSettings _settings;
        private readonly ILogger<BlockCypherService> _logger;

        // Rate Limiting Components
        private readonly TokenBucketRateLimiter _perSecondLimiter;
        private readonly HourlyRequestTracker _hourlyTracker;

        // Circuit Breaker State
        private DateTime _circuitResetTime = DateTime.MinValue;
        private readonly SemaphoreSlim _circuitLock = new(1, 1);

        // Response Header Tracking
        private int _remainingHourlyRequests;
        private DateTime _rateLimitReset = DateTime.UtcNow.Date.AddHours(1);

        public BlockCypherService(
            IHttpClientFactory httpClientFactory,
            IOptions<BlockCypherSettings> settings,
            ILogger<BlockCypherService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _endpoints = _settings.Endpoints ?? new Dictionary<string, string>();
            _remainingHourlyRequests = _settings.InitialRemainingRequests;

            // Initialize rate limiters from configuration
            _perSecondLimiter = new TokenBucketRateLimiter(
                _settings.RequestsPerSecond,
                TimeSpan.FromSeconds(1),
                _settings.TokenCheckDelayMs,
                _logger);

            _hourlyTracker = new HourlyRequestTracker(
                _settings.RequestsPerHour,
                _logger);

            _logger.LogInformation(
                "BlockCypherService initialized with {EndpointCount} endpoints. " +
                "Rate limit: {RequestsPerSecond} req/sec, {RequestsPerHour} req/hour. " +
                "Concurrency: {MaxConcurrent}, HTTP Timeout: {TimeoutSeconds}s",
                _endpoints.Count,
                _settings.RequestsPerSecond,
                _settings.RequestsPerHour,
                _settings.MaxConcurrentRequests,
                _settings.HttpTimeoutSeconds);
        }

        public async Task<List<BlockchainData>> FetchAllAsync(CancellationToken cancellationToken = default)
        {
            // 1. Check Circuit Breaker
            if (await IsCircuitOpenAsync(cancellationToken))
            {
                var remainingWait = _circuitResetTime - DateTime.UtcNow;
                _logger.LogWarning("Circuit is OPEN. Rate limit protection active. Next attempt allowed in {Minutes}m",
                    Math.Round(remainingWait.TotalMinutes, 1));
                return new List<BlockchainData>();
            }

            // 2. Check Hourly Limit
            if (!_hourlyTracker.CanMakeRequests(_endpoints.Count))
            {
                _logger.LogWarning("Hourly rate limit would be exceeded. Available: {Available}, Needed: {Needed}",
                    _hourlyTracker.RemainingRequests, _endpoints.Count);
                return new List<BlockchainData>();
            }

            // 3. Fetch all endpoints with configurable concurrency
            var results = new ConcurrentBag<BlockchainData>();
            var semaphore = new SemaphoreSlim(_settings.MaxConcurrentRequests, _settings.MaxConcurrentRequests);

            var tasks = _endpoints.Select(async endpoint =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var data = await FetchSingleSafeAsync(endpoint.Key, endpoint.Value, cancellationToken);
                    if (data != null)
                        results.Add(data);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Fetch completed. Success: {Success}/{Total}. Hourly remaining: {Remaining}",
                results.Count, _endpoints.Count, _remainingHourlyRequests);

            return results.ToList();
        }

        private async Task<BlockchainData?> FetchSingleSafeAsync(string symbol, string url, CancellationToken cancellationToken)
        {
            try
            {
                return await FetchSingleAsync(symbol, url, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("Circuit Open"))
            {
                _logger.LogWarning("Circuit breaker blocked request for {Symbol}", symbol);
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Request cancelled for {Symbol}", symbol);
                throw; // Propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch data for {Symbol} from {Url}", symbol, url);
                return null;
            }
        }

        private async Task<BlockchainData> FetchSingleAsync(string symbol, string url, CancellationToken cancellationToken)
        {
            // 1. Check circuit breaker again (might have been tripped by another request)
            if (await IsCircuitOpenAsync(cancellationToken))
                throw new HttpRequestException("Circuit Open - Rate limit protection active");

            // 2. Wait for per-second rate limit token
            await _perSecondLimiter.WaitForTokenAsync(cancellationToken);

            // 3. Record hourly request
            _hourlyTracker.RecordRequest();

            // 4. Make HTTP request with configured timeout
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(_settings.HttpTimeoutSeconds);

            using var response = await client.GetAsync(url, cancellationToken);

            // 5. Update tracking from response headers
            UpdateFromResponseHeaders(symbol, response);

            // 6. Handle rate limit response
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await TripCircuitAsync(response, cancellationToken);
                throw new HttpRequestException($"429 TooManyRequests for {symbol}");
            }

            // 7. Handle other errors
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API returned {StatusCode} for {Symbol}", response.StatusCode, symbol);
                throw new HttpRequestException($"API Error {response.StatusCode} for {symbol}");
            }

            // 8. Parse response
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Successfully fetched {Symbol}. Content length: {Length} bytes", symbol, content.Length);

            return new BlockchainData
            {
                Symbol = symbol.ToUpperInvariant(),
                RequestUrl = url,
                JsonResponse = content,
                CreatedAt = DateTime.UtcNow
            };
        }

        private void UpdateFromResponseHeaders(string symbol, HttpResponseMessage response)
        {
            try
            {
                // BlockCypher returns X-Ratelimit-Remaining header
                if (response.Headers.TryGetValues("X-Ratelimit-Remaining", out var remainingValues))
                {
                    if (int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                    {
                        _remainingHourlyRequests = remaining;
                        _logger.LogDebug("{Symbol}: Hourly requests remaining: {Remaining}", symbol, remaining);
                    }
                }

                // Log all headers in development for debugging
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    LogAllHeaders(symbol, response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse rate limit headers for {Symbol}", symbol);
            }
        }

        private async Task TripCircuitAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await _circuitLock.WaitAsync(cancellationToken);
            try
            {
                // Use configured default or Retry-After header
                int retrySeconds = _settings.DefaultCircuitBreakerDurationSeconds;

                if (response.Headers.RetryAfter != null)
                {
                    if (response.Headers.RetryAfter.Delta.HasValue)
                        retrySeconds = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                    else if (response.Headers.RetryAfter.Date.HasValue)
                        retrySeconds = (int)(response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                }

                _circuitResetTime = DateTime.UtcNow.AddSeconds(retrySeconds);

                _logger.LogCritical("CIRCUIT BREAKER TRIPPED! Rate limit reached. Locked until {Time} UTC (~{Minutes} minutes)",
                    _circuitResetTime, Math.Round(retrySeconds / 60.0, 1));
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        private async Task<bool> IsCircuitOpenAsync(CancellationToken cancellationToken)
        {
            await _circuitLock.WaitAsync(cancellationToken);
            try
            {
                return DateTime.UtcNow < _circuitResetTime;
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        private void LogAllHeaders(string symbol, HttpResponseMessage response)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- Response Headers for {symbol} ({response.StatusCode}) ---");
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            }
            _logger.LogTrace(sb.ToString());
        }
    }

    /// <summary>
    /// Token Bucket rate limiter for per-second request limiting with burst capacity.
    /// </summary>
    public class TokenBucketRateLimiter
    {
        private readonly int _maxTokens;
        private readonly TimeSpan _refillInterval;
        private readonly int _checkDelayMs;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private int _availableTokens;
        private DateTime _lastRefillTime;

        public TokenBucketRateLimiter(int maxTokens, TimeSpan refillInterval, int checkDelayMs, ILogger logger)
        {
            _maxTokens = maxTokens;
            _availableTokens = maxTokens;
            _refillInterval = refillInterval;
            _checkDelayMs = checkDelayMs;
            _lastRefillTime = DateTime.UtcNow;
            _logger = logger;
        }

        public async Task WaitForTokenAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                RefillTokens();

                // Wait until a token becomes available
                while (_availableTokens <= 0)
                {
                    _semaphore.Release();
                    await Task.Delay(_checkDelayMs, cancellationToken); // Configurable delay
                    await _semaphore.WaitAsync(cancellationToken);
                    RefillTokens();
                }

                _availableTokens--;
                _logger.LogTrace("Token consumed. Available: {Available}/{Max}", _availableTokens, _maxTokens);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void RefillTokens()
        {
            var now = DateTime.UtcNow;
            var timePassed = now - _lastRefillTime;

            if (timePassed < _refillInterval)
                return;

            var intervalsElapsed = (int)(timePassed.TotalMilliseconds / _refillInterval.TotalMilliseconds);

            if (intervalsElapsed > 0)
            {
                var tokensToAdd = Math.Min(intervalsElapsed, _maxTokens - _availableTokens);
                _availableTokens = Math.Min(_maxTokens, _availableTokens + tokensToAdd);
                _lastRefillTime = now;

                if (tokensToAdd > 0)
                {
                    _logger.LogTrace("Tokens refilled: +{Added}. Available: {Available}/{Max}",
                        tokensToAdd, _availableTokens, _maxTokens);
                }
            }
        }
    }

    /// <summary>
    /// Tracks hourly request counts with automatic reset at the top of each hour.
    /// </summary>
    public class HourlyRequestTracker
    {
        private readonly int _maxRequestsPerHour;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _requestsMadeThisHour;
        private DateTime _currentHourStart;

        public int RemainingRequests => Math.Max(0, _maxRequestsPerHour - _requestsMadeThisHour);

        public HourlyRequestTracker(int maxRequestsPerHour, ILogger logger)
        {
            _maxRequestsPerHour = maxRequestsPerHour;
            _logger = logger;
            _currentHourStart = GetCurrentHourStart();
            _requestsMadeThisHour = 0;
        }

        public bool CanMakeRequests(int count)
        {
            ResetIfNewHour();
            return _requestsMadeThisHour + count <= _maxRequestsPerHour;
        }

        public void RecordRequest()
        {
            _lock.Wait();
            try
            {
                ResetIfNewHour();
                _requestsMadeThisHour++;

                if (_requestsMadeThisHour % 10 == 0)
                {
                    _logger.LogInformation("Hourly usage: {Used}/{Max} ({Percentage}%). Resets at {ResetTime} UTC",
                        _requestsMadeThisHour, _maxRequestsPerHour,
                        Math.Round((_requestsMadeThisHour / (double)_maxRequestsPerHour) * 100, 1),
                        _currentHourStart.AddHours(1));
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        private void ResetIfNewHour()
        {
            var currentHour = GetCurrentHourStart();
            if (currentHour != _currentHourStart)
            {
                _logger.LogInformation("Hourly rate limit reset. Previous hour: {Used}/{Max} requests",
                    _requestsMadeThisHour, _maxRequestsPerHour);
                _requestsMadeThisHour = 0;
                _currentHourStart = currentHour;
            }
        }

        private static DateTime GetCurrentHourStart()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        }
    }
}
