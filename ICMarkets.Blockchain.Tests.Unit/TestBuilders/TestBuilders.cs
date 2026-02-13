using ICMarkets.Blockchain.Domain.Entities;

namespace ICMarkets.Blockchain.Tests.Unit.TestBuilders;

/// <summary>
/// Builder pattern for creating BlockchainData test instances with fluent API.
/// </summary>
public class BlockchainDataBuilder
{
    private string _symbol = "BTC";
    private string _requestUrl = "https://api.test.com/btc";
    private string _jsonResponse = "{\"height\": 800000, \"name\": \"Bitcoin\"}";
    private DateTime _createdAt = DateTime.UtcNow;

    public BlockchainDataBuilder WithSymbol(string symbol)
    {
        _symbol = symbol;
        return this;
    }

    public BlockchainDataBuilder WithRequestUrl(string requestUrl)
    {
        _requestUrl = requestUrl;
        return this;
    }

    public BlockchainDataBuilder WithJsonResponse(string jsonResponse)
    {
        _jsonResponse = jsonResponse;
        return this;
    }

    public BlockchainDataBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public BlockchainDataBuilder WithHeight(int height)
    {
        _jsonResponse = $"{{\"height\": {height}, \"name\": \"{_symbol}\"}}";
        return this;
    }

    public BlockchainData Build()
    {
        return new BlockchainData
        {
            Symbol = _symbol,
            RequestUrl = _requestUrl,
            JsonResponse = _jsonResponse,
            CreatedAt = _createdAt
        };
    }

    public static BlockchainDataBuilder Default() => new();

    public static List<BlockchainData> CreateList(int count, params string[] symbols)
    {
        if (symbols.Length == 0)
            symbols = new[] { "BTC", "ETH", "DASH", "LTC", "DOGE" };

        var data = new List<BlockchainData>();
        for (int i = 0; i < count; i++)
        {
            var symbol = symbols[i % symbols.Length];
            data.Add(new BlockchainDataBuilder()
                .WithSymbol(symbol)
                .WithRequestUrl($"https://api.test.com/{symbol.ToLower()}")
                .WithHeight(100000 + i)
                .Build());
        }
        return data;
    }
}
