using FluentAssertions;
using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Integration;

/// <summary>
/// Integration tests for the Infrastructure layer with configurable data retention.
/// Tests the Repository pattern, Entity Framework Core, SQLite database operations,
/// OData query composition, and 30-day retention cutoff functionality.
/// </summary>
public class BlockchainRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly RepositorySettings _defaultSettings;

    public BlockchainRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _defaultSettings = new RepositorySettings { DataRetentionDays = 30 };

        using var context = new AppDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    #region Helper Methods

    private BlockchainRepository CreateRepository(AppDbContext context)
    {
        return new BlockchainRepository(context, Options.Create(_defaultSettings));
    }

    private BlockchainRepository CreateRepository(AppDbContext context, int retentionDays)
    {
        var settings = new RepositorySettings { DataRetentionDays = retentionDays };
        return new BlockchainRepository(context, Options.Create(settings));
    }

    private async Task SeedRecentDataAsync(AppDbContext context, string symbol, int count)
    {
        var baseTime = DateTime.UtcNow;
        var testData = new List<BlockchainData>();

        for (int i = 0; i < count; i++)
        {
            testData.Add(new BlockchainData
            {
                Symbol = symbol,
                JsonResponse = $"{{\"index\":{i}}}",
                RequestUrl = $"https://test.com/{symbol}/{i}",
                CreatedAt = baseTime.AddSeconds(-i)
            });
        }

        await context.BlockchainData.AddRangeAsync(testData);
        await context.SaveChangesAsync();
    }

    private async Task SeedDataWithTimestampsAsync(AppDbContext context, params (string Symbol, DateTime CreatedAt)[] entries)
    {
        var testData = entries.Select(e => new BlockchainData
        {
            Symbol = e.Symbol,
            JsonResponse = $"{{\"symbol\":\"{e.Symbol}\"}}",
            RequestUrl = $"https://test.com/{e.Symbol}",
            CreatedAt = e.CreatedAt
        }).ToList();

        await context.BlockchainData.AddRangeAsync(testData);
        await context.SaveChangesAsync();
    }

    #endregion

    #region AddRangeAsync Tests

    [Fact]
    public async Task AddRangeAsync_SingleEntity_SavesSuccessfully()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        var testData = new List<BlockchainData>
        {
            new()
            {
                Symbol = "BTC",
                JsonResponse = "{\"height\":800000}",
                RequestUrl = "https://api.blockcypher.com/v1/btc/main",
                CreatedAt = DateTime.UtcNow
            }
        };

        await repo.AddRangeAsync(testData);

        var count = await context.BlockchainData.CountAsync();
        count.Should().Be(1);

        var saved = await context.BlockchainData.FirstAsync();
        saved.Symbol.Should().Be("BTC");
        saved.JsonResponse.Should().Be("{\"height\":800000}");
        saved.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddRangeAsync_MultipleEntities_SavesAllSuccessfully()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        var testData = new List<BlockchainData>
        {
            new() { Symbol = "BTC", JsonResponse = "{}", RequestUrl = "https://test.com/btc", CreatedAt = DateTime.UtcNow },
            new() { Symbol = "ETH", JsonResponse = "{}", RequestUrl = "https://test.com/eth", CreatedAt = DateTime.UtcNow },
            new() { Symbol = "DASH", JsonResponse = "{}", RequestUrl = "https://test.com/dash", CreatedAt = DateTime.UtcNow }
        };

        await repo.AddRangeAsync(testData);

        var count = await context.BlockchainData.CountAsync();
        count.Should().Be(3);

        var symbols = await context.BlockchainData.Select(x => x.Symbol).ToListAsync();
        symbols.Should().Contain(new[] { "BTC", "ETH", "DASH" });
    }

    [Fact]
    public async Task AddRangeAsync_EmptyList_DoesNotThrow()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        var emptyData = new List<BlockchainData>();

        Func<Task> act = async () => await repo.AddRangeAsync(emptyData);

        await act.Should().NotThrowAsync();
        var count = await context.BlockchainData.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task AddRangeAsync_WithCancellationToken_CancelsOperation()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        var cts = new CancellationTokenSource();
        var testData = Enumerable.Range(1, 100)
            .Select(i => new BlockchainData
            {
                Symbol = "BTC",
                JsonResponse = $"{{\"index\":{i}}}",
                RequestUrl = "https://test.com",
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        cts.Cancel();
        Func<Task> act = async () => await repo.AddRangeAsync(testData, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetQueryableHistory - Data Retention Tests

    [Fact]
    public async Task GetQueryableHistory_OnlyReturnsDataWithinRetentionPeriod()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context, retentionDays: 30);

        // Use a fixed reference time to avoid timing issues
        var now = DateTime.UtcNow;

        await SeedDataWithTimestampsAsync(context,
            ("BTC", now.AddDays(-31)),  // Outside retention - excluded
            ("ETH", now.AddDays(-29)),  // Well within retention - included
            ("DASH", now.AddDays(-15)), // Within retention - included
            ("LTC", now.AddDays(-1))    // Recent - included
        );

        var result = repo.GetQueryableHistory().ToList();

        result.Should().HaveCount(3);
        result.Should().NotContain(x => x.Symbol == "BTC");
        result.Should().Contain(x => x.Symbol == "ETH");
        result.Should().Contain(x => x.Symbol == "DASH");
        result.Should().Contain(x => x.Symbol == "LTC");
    }


    [Fact]
    public async Task GetQueryableHistory_WithDifferentRetentionPeriods_ReturnsCorrectData()
    {
        using var context = new AppDbContext(_options);

        await SeedDataWithTimestampsAsync(context,
            ("BTC", DateTime.UtcNow.AddDays(-10)),
            ("ETH", DateTime.UtcNow.AddDays(-5)),
            ("DASH", DateTime.UtcNow.AddDays(-2))
        );

        // 7 day retention - should exclude BTC
        var repo7Days = CreateRepository(context, retentionDays: 7);
        var result7Days = repo7Days.GetQueryableHistory().ToList();
        result7Days.Should().HaveCount(2);
        result7Days.Should().OnlyContain(x => x.Symbol == "ETH" || x.Symbol == "DASH");

        // 30 day retention - should include all
        var repo30Days = CreateRepository(context, retentionDays: 30);
        var result30Days = repo30Days.GetQueryableHistory().ToList();
        result30Days.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetQueryableHistory_WithZeroRetention_ReturnsEmptyResult()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context, retentionDays: 0);
        await SeedRecentDataAsync(context, "BTC", 5);

        var result = repo.GetQueryableHistory().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueryableHistory_WithVeryLargeRetention_ReturnsAllData()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context, retentionDays: 36500); // 100 years

        await SeedDataWithTimestampsAsync(context,
            ("BTC", DateTime.UtcNow.AddDays(-365)),
            ("ETH", DateTime.UtcNow.AddDays(-730)),
            ("DASH", DateTime.UtcNow.AddDays(-1095))
        );

        var result = repo.GetQueryableHistory().ToList();

        result.Should().HaveCount(3);
    }

    #endregion

    #region GetQueryableHistory - Basic Functionality

    [Fact]
    public void GetQueryableHistory_ReturnsIQueryable()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        var result = repo.GetQueryableHistory();

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IQueryable<BlockchainData>>();
    }

    [Fact]
    public async Task GetQueryableHistory_ReturnsDataInDescendingOrderByCreatedAt()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        await SeedDataWithTimestampsAsync(context,
            ("BTC", new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc)),
            ("BTC", new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc)), // Latest
            ("BTC", new DateTime(2026, 2, 1, 11, 0, 0, DateTimeKind.Utc))
        );

        var result = repo.GetQueryableHistory().ToList();

        result.Should().HaveCount(3);
        result[0].CreatedAt.Should().Be(new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));
        result[1].CreatedAt.Should().Be(new DateTime(2026, 2, 1, 11, 0, 0, DateTimeKind.Utc));
        result[2].CreatedAt.Should().Be(new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetQueryableHistory_EmptyDatabase_ReturnsEmptyQueryable()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        var result = repo.GetQueryableHistory().ToList();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetQueryableHistory - OData Filtering

    [Fact]
    public async Task GetQueryableHistory_ODataFilter_BySymbol()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "BTC", 3);
        await SeedRecentDataAsync(context, "ETH", 2);

        var result = repo.GetQueryableHistory()
            .Where(x => x.Symbol == "BTC")
            .ToList();

        result.Should().HaveCount(3);
        result.Should().OnlyContain(x => x.Symbol == "BTC");
    }

    [Fact]
    public async Task GetQueryableHistory_ODataFilter_ByDateRange()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        await SeedDataWithTimestampsAsync(context,
            ("BTC", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            ("BTC", new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)),
            ("BTC", new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc))
        );

        var startDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc);
        var result = repo.GetQueryableHistory()
            .Where(x => x.CreatedAt >= startDate && x.CreatedAt < endDate)
            .ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetQueryableHistory_ODataFilter_ContainsOnJsonResponse()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        var testData = new List<BlockchainData>
        {
            new() { Symbol = "BTC", JsonResponse = "{\"name\":\"Bitcoin\"}", RequestUrl = "https://test.com", CreatedAt = DateTime.UtcNow },
            new() { Symbol = "ETH", JsonResponse = "{\"name\":\"Ethereum\"}", RequestUrl = "https://test.com", CreatedAt = DateTime.UtcNow },
            new() { Symbol = "DASH", JsonResponse = "{\"name\":\"Dash\"}", RequestUrl = "https://test.com", CreatedAt = DateTime.UtcNow }
        };

        await context.BlockchainData.AddRangeAsync(testData);
        await context.SaveChangesAsync();

        var result = repo.GetQueryableHistory()
            .Where(x => x.JsonResponse.Contains("Bitcoin"))
            .ToList();

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("BTC");
    }

    #endregion

    #region GetQueryableHistory - OData Ordering & Pagination

    [Fact]
    public async Task GetQueryableHistory_ODataOrderBy_SymbolAscending()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "DASH", 1);
        await SeedRecentDataAsync(context, "BTC", 1);
        await SeedRecentDataAsync(context, "ETH", 1);

        var result = repo.GetQueryableHistory()
            .OrderBy(x => x.Symbol)
            .ToList();

        result.Should().HaveCount(3);
        result[0].Symbol.Should().Be("BTC");
        result[1].Symbol.Should().Be("DASH");
        result[2].Symbol.Should().Be("ETH");
    }

    [Fact]
    public async Task GetQueryableHistory_ODataSkipAndTop_ReturnsCorrectPage()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "BTC", 10);

        var result = repo.GetQueryableHistory()
            .Skip(5)
            .Take(3)
            .ToList();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetQueryableHistory_ODataPagination_MaintainsDescendingOrder()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "BTC", 10);

        var page1 = repo.GetQueryableHistory().Skip(0).Take(3).ToList();
        var page2 = repo.GetQueryableHistory().Skip(3).Take(3).ToList();

        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
        page1.Last().CreatedAt.Should().BeOnOrAfter(page2.First().CreatedAt);
    }

    #endregion

    #region GetQueryableHistory - AsNoTracking Behavior

    [Fact]
    public async Task GetQueryableHistory_UsesAsNoTracking_DoesNotTrackEntities()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "BTC", 3);

        var result = repo.GetQueryableHistory().ToList();

        foreach (var entity in result)
        {
            var entry = context.Entry(entity);
            entry.State.Should().Be(EntityState.Detached);
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Repository_LargeJsonResponse_SavesAndRetrievesCorrectly()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);

        var largeJson = new string('x', 50000);
        var testData = new List<BlockchainData>
        {
            new() { Symbol = "BTC", JsonResponse = largeJson, RequestUrl = "https://test.com", CreatedAt = DateTime.UtcNow }
        };

        await repo.AddRangeAsync(testData);
        var result = repo.GetQueryableHistory().First();

        result.JsonResponse.Should().Be(largeJson);
        result.JsonResponse.Length.Should().Be(50000);
    }

    [Fact]
    public async Task Repository_SpecialCharactersInJson_HandlesCorrectly()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        var jsonWithSpecialChars = "{\"symbol\":\"BTC\",\"data\":\"Test's \\\"quoted\\\" value\"}";
        var testData = new List<BlockchainData>
        {
            new() { Symbol = "BTC", JsonResponse = jsonWithSpecialChars, RequestUrl = "https://test.com", CreatedAt = DateTime.UtcNow }
        };

        await repo.AddRangeAsync(testData);
        var result = repo.GetQueryableHistory().First();

        result.JsonResponse.Should().Be(jsonWithSpecialChars);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Repository_LargeDataset_QueriesEfficiently()
    {
        using var context = new AppDbContext(_options);
        var repo = CreateRepository(context);
        await SeedRecentDataAsync(context, "BTC", 100);
        await SeedRecentDataAsync(context, "ETH", 100);

        var result = repo.GetQueryableHistory()
            .Where(x => x.Symbol == "BTC")
            .OrderByDescending(x => x.CreatedAt)
            .Skip(10)
            .Take(20)
            .ToList();

        result.Should().HaveCount(20);
        result.Should().OnlyContain(x => x.Symbol == "BTC");
    }

    #endregion
}
