using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.API.Configuration;

namespace ICMarkets.Blockchain.Tests.Unit.TestBuilders;

public class BlockCypherSettingsBuilder
{
    private Dictionary<string, string> _endpoints = new()
    {
        { "BTC", "https://api.blockcypher.com/v1/btc/main" },
        { "ETH", "https://api.blockcypher.com/v1/eth/main" }
    };
    private int _requestsPerSecond = 3;
    private int _requestsPerHour = 100;
    private int _maxConcurrentRequests = 3;
    private int _httpTimeoutSeconds = 30;
    private int _tokenCheckDelayMs = 50;
    private int _defaultCircuitBreakerDurationSeconds = 3600;
    private int _initialRemainingRequests = 100;

    public BlockCypherSettingsBuilder WithEndpoints(Dictionary<string, string> endpoints)
    {
        _endpoints = endpoints;
        return this;
    }

    public BlockCypherSettingsBuilder WithEndpoint(string symbol, string url)
    {
        _endpoints[symbol] = url;
        return this;
    }

    public BlockCypherSettingsBuilder WithRequestsPerSecond(int value)
    {
        _requestsPerSecond = value;
        return this;
    }

    public BlockCypherSettingsBuilder WithRequestsPerHour(int value)
    {
        _requestsPerHour = value;
        _initialRemainingRequests = value;
        return this;
    }

    public BlockCypherSettingsBuilder WithMaxConcurrentRequests(int value)
    {
        _maxConcurrentRequests = value;
        return this;
    }

    public BlockCypherSettingsBuilder WithHttpTimeoutSeconds(int value)
    {
        _httpTimeoutSeconds = value;
        return this;
    }

    public BlockCypherSettings Build()
    {
        return new BlockCypherSettings
        {
            Endpoints = _endpoints,
            RequestsPerSecond = _requestsPerSecond,
            RequestsPerHour = _requestsPerHour,
            MaxConcurrentRequests = _maxConcurrentRequests,
            HttpTimeoutSeconds = _httpTimeoutSeconds,
            TokenCheckDelayMs = _tokenCheckDelayMs,
            DefaultCircuitBreakerDurationSeconds = _defaultCircuitBreakerDurationSeconds,
            InitialRemainingRequests = _initialRemainingRequests
        };
    }

    public static BlockCypherSettingsBuilder Default() => new();
}

/// <summary>
/// Builder for ODataSettings test instances.
/// Note: ODataSettings only has MaxTop and AllowedOrderByProperties (no PageSize).
/// </summary>
public class ODataSettingsBuilder
{
    private int _maxTop = 100;
    private string[] _allowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" };

    public ODataSettingsBuilder WithMaxTop(int value)
    {
        _maxTop = value;
        return this;
    }

    public ODataSettingsBuilder WithAllowedOrderByProperties(params string[] properties)
    {
        _allowedOrderByProperties = properties;
        return this;
    }

    public ODataSettings Build()
    {
        return new ODataSettings
        {
            MaxTop = _maxTop,
            AllowedOrderByProperties = _allowedOrderByProperties
        };
    }

    public static ODataSettingsBuilder Default() => new();
}

public class RepositorySettingsBuilder
{
    private int _dataRetentionDays = 30;

    public RepositorySettingsBuilder WithDataRetentionDays(int days)
    {
        _dataRetentionDays = days;
        return this;
    }

    public RepositorySettings Build()
    {
        return new RepositorySettings
        {
            DataRetentionDays = _dataRetentionDays
        };
    }

    public static RepositorySettingsBuilder Default() => new();
}
