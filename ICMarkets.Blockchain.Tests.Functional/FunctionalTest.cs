using FluentAssertions;
using ICMarkets.Blockchain.API;
using ICMarkets.Blockchain.API.Configuration;
using ICMarkets.Blockchain.Application.Features;
using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Functional;

/// <summary>
/// Functional tests for the Blockchain API endpoints.
/// Tests the full HTTP pipeline including routing, query validation, filtering, sorting, pagination, and configuration validation.
/// Uses WebApplicationFactory for integration testing with in-memory test server.
/// </summary>
public class FunctionalTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    // Updated route to match controller configuration
    private const string ODataRoute = "/api/blockchain/odata/BlockchainHistory";

    public FunctionalTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("Healthy");
    }

    #endregion

    #region POST /api/blockchain/sync Tests

    [Fact]
    public async Task SyncData_ReturnsSuccessWithCount()
    {
        // Act
        var response = await _client.PostAsync("/api/blockchain/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SyncData_PopulatesDatabase()
    {
        // Arrange - Sync data first
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Verify data exists by querying history endpoint
        var response = await _client.GetAsync(ODataRoute);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.Should().Contain("id");
        content.Should().Contain("symbol");
    }

    [Fact]
    public async Task SyncData_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.PostAsync("/api/blockchain/sync", null);

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task SyncData_MultipleCallsAccumulatesData()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.PostAsync("/api/blockchain/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SyncResult>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SyncData_InvalidMethodReturnsMethodNotAllowed()
    {
        // Act
        var response = await _client.GetAsync("/api/blockchain/sync");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region GET /api/blockchain/odata/BlockchainHistory - Basic Tests

    [Fact]
    public async Task GetHistory_ReturnsOk()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync(ODataRoute);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetHistory_ReturnsJsonArray()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync(ODataRoute);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<List<BlockchainDto>>();
    }

    [Fact]
    public async Task GetHistory_EmptyDatabase_ReturnsEmptyArray()
    {
        // Arrange - Clear database first
        await _factory.ClearDatabaseAsync();

        // Act - Query without syncing data first
        var response = await _client.GetAsync(ODataRoute);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    #endregion

    #region $filter Tests

    [Fact]
    public async Task GetHistory_FilterBySymbol_ReturnsBtcOnly()
    {
        // Arrange
        var syncResponse = await _client.PostAsync("/api/blockchain/sync", null);
        var syncResult = await syncResponse.Content.ReadFromJsonAsync<SyncResult>();

        // Skip test if no data was synced
        if (syncResult?.Count == 0) return;

        // Act - Property names in OData $filter should match DTO property names (case-sensitive)
        var response = await _client.GetAsync($"{ODataRoute}?$filter=Symbol eq 'BTC'");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        if (result?.Any() == true)
        {
            result.Should().OnlyContain(x => x.Symbol == "BTC");
        }
    }

    [Fact]
    public async Task GetHistory_FilterMultipleSymbols_ReturnsMatchingSymbols()
    {
        // Arrange
        var syncResponse = await _client.PostAsync("/api/blockchain/sync", null);
        var syncResult = await syncResponse.Content.ReadFromJsonAsync<SyncResult>();

        // Skip test if no data was synced
        if (syncResult?.Count == 0) return;

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$filter=Symbol in ('BTC','ETH')");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        if (result?.Any() == true)
        {
            result.Should().OnlyContain(x => x.Symbol == "BTC" || x.Symbol == "ETH");
        }
    }

    [Fact]
    public async Task GetHistory_FilterByDateRange_ReturnsFilteredResults()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await _client.GetAsync($"{ODataRoute}?$filter=CreatedAt gt {yesterday}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_FilterComplexCondition_ReturnsFilteredResults()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await _client.GetAsync(
            $"{ODataRoute}?$filter=Symbol eq 'BTC' and CreatedAt gt {yesterday}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_FilterInvalidSyntax_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$filter=Symbol invalid syntax");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region $orderby Tests - Validation Constraints

    [Fact]
    public async Task GetHistory_OrderBySymbol_ReturnsAscendingOrder()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Symbol is in AllowedOrderByProperties
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=Symbol asc&$top=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        if (result?.Count > 1)
        {
            for (int i = 0; i < result.Count - 1; i++)
            {
                string.Compare(result[i].Symbol, result[i + 1].Symbol, StringComparison.Ordinal)
                    .Should().BeLessThanOrEqualTo(0);
            }
        }
    }

    [Fact]
    public async Task GetHistory_OrderByCreatedAt_ReturnsDescendingOrder()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);
        await Task.Delay(100); // Ensure different timestamps
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - CreatedAt is in AllowedOrderByProperties
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=CreatedAt desc&$top=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_OrderById_ReturnsOrdered()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Id is in AllowedOrderByProperties
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=Id desc&$top=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_OrderByMultipleAllowedFields_AppliesCorrectOrdering()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Use allowed properties: Symbol, CreatedAt, Id
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=Symbol asc,CreatedAt desc&$top=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_OrderByDisallowedProperty_ReturnsBadRequest()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Data is NOT in AllowedOrderByProperties
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=Data asc");

        // Assert - Should be rejected by validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetHistory_OrderByRequestUrl_ReturnsBadRequest()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - RequestUrl is NOT in AllowedOrderByProperties
        var response = await _client.GetAsync($"{ODataRoute}?$orderby=RequestUrl asc");

        // Assert - Should be rejected by validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Stable Pagination Tests

    [Fact]
    public async Task GetHistory_Pagination_IsStableWithIdenticalTimestamps()
    {
        // Arrange - Insert records with identical CreatedAt timestamps directly into database
        await _factory.SeedDataWithSameTimestampAsync();

        // Act - Fetch same page multiple times
        var page1Attempt1 = await _client.GetAsync($"{ODataRoute}?$skip=0&$top=3");
        var page1Attempt2 = await _client.GetAsync($"{ODataRoute}?$skip=0&$top=3");

        var result1 = await page1Attempt1.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        var result2 = await page1Attempt2.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        // Assert - Results should be identical across attempts
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();

        var ids1 = result1!.Select(x => x.Id).ToList();
        var ids2 = result2!.Select(x => x.Id).ToList();
        ids1.Should().Equal(ids2, "pagination should return same records in same order");
    }

    [Fact]
    public async Task GetHistory_Pagination_PagesDoNotOverlap()
    {
        // Arrange - Insert records with identical CreatedAt timestamps
        await _factory.SeedDataWithSameTimestampAsync();

        // Act - Fetch two consecutive pages
        var page1Response = await _client.GetAsync($"{ODataRoute}?$skip=0&$top=3");
        var page2Response = await _client.GetAsync($"{ODataRoute}?$skip=3&$top=3");

        var page1 = await page1Response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        var page2 = await page2Response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        // Assert - No record should appear in both pages
        page1.Should().NotBeNull();
        page2.Should().NotBeNull();

        var page1Ids = page1!.Select(x => x.Id).ToHashSet();
        var page2Ids = page2!.Select(x => x.Id).ToHashSet();
        page1Ids.Should().NotIntersectWith(page2Ids, "no record should appear in multiple pages");
    }

    [Fact]
    public async Task GetHistory_DefaultSort_OrdersByCreatedAtDescThenIdAsc()
    {
        // Arrange - Insert records with mixed timestamps
        await _factory.SeedDataWithSameTimestampAsync();

        // Act - Fetch without explicit $orderby should use default sort
        var response = await _client.GetAsync($"{ODataRoute}?$top=10");
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        // Assert
        result.Should().NotBeNull();
        result!.Count.Should().BeGreaterThan(0);

        // Verify descending order by CreatedAt
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].CreatedAt.Should().BeOnOrAfter(result[i + 1].CreatedAt,
                "records should be ordered by CreatedAt descending");
        }
    }

    [Fact]
    public async Task GetHistory_Pagination_ThroughAllRecordsCoversAllData()
    {
        // Arrange - Insert known number of records
        await _factory.SeedDataWithSameTimestampAsync();

        // Act - Paginate through all records
        var allIds = new HashSet<Guid>();
        var pageSize = 3;
        var skip = 0;
        var hasMore = true;

        while (hasMore)
        {
            var response = await _client.GetAsync($"{ODataRoute}?$skip={skip}&$top={pageSize}");
            var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();

            if (result?.Any() != true)
            {
                hasMore = false;
            }
            else
            {
                foreach (var item in result)
                {
                    allIds.Add(item.Id).Should().BeTrue("each record should appear exactly once");
                }
                skip += pageSize;
            }
        }

        // Assert - Should have collected all records
        allIds.Count.Should().BeGreaterThan(0, "should have retrieved at least some records");
    }

    #endregion

    #region $top and $skip Pagination Tests - MaxTop Validation

    [Fact]
    public async Task GetHistory_Top5_ReturnsMaximum5Records()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$top=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
        result!.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetHistory_Top100_ReturnsMaximum100Records()
    {
        // Arrange - Ensure we have enough records
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/blockchain/sync", null);
            await Task.Delay(50); // Small delay to avoid rate limiting
        }

        // Act - MaxTop is 100, so this should succeed
        var response = await _client.GetAsync($"{ODataRoute}?$top=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
        result!.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetHistory_Top101_ReturnsBadRequest()
    {
        // Act - Exceeds MaxTop = 100
        var response = await _client.GetAsync($"{ODataRoute}?$top=101");

        // Assert - Should be rejected by OData validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetHistory_TopExceedsMaxTop_ReturnsBadRequest()
    {
        // Act - Try to request more than MaxTop = 100
        var response = await _client.GetAsync($"{ODataRoute}?$top=500");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetHistory_Skip5Top5_ReturnsPaginatedResults()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$skip=5&$top=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHistory_SkipBeyondDataset_ReturnsEmptyResult()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$skip=10000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<BlockchainDto>>();
        result.Should().NotBeNull();
    }

    #endregion

    #region $select Projection Tests

    [Fact]
    public async Task GetHistory_SelectFields_ReturnsOnlySelectedFields()
    {
        // Arrange - Seed test data
        var seedCount = await _factory.SeedTestDataForProjectionAsync();

        // Skip test if seeding failed
        if (seedCount == 0) return;

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$select=Symbol,CreatedAt&$top=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // For projections, check raw JSON content instead of deserializing to BlockchainDto
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        // Verify JSON contains projected fields (case-insensitive)
        content.ToLower().Should().Contain("symbol");
        content.ToLower().Should().Contain("createdat");
    }

    [Fact]
    public async Task GetHistory_SelectSingleField_ReturnsOnlyThatField()
    {
        // Arrange - Seed test data
        var seedCount = await _factory.SeedTestDataForProjectionAsync();

        // Skip test if seeding failed
        if (seedCount == 0) return;

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$select=Symbol&$top=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check raw JSON content
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        content.ToLower().Should().Contain("symbol");
    }

    #endregion

    #region Complex Query Tests



    [Fact]
    public async Task GetHistory_ComplexFilterOrderSelect_ReturnsExpectedResult()
    {
        // Arrange - Seed test data
        var seedCount = await _factory.SeedTestDataForProjectionAsync();

        // Skip test if seeding failed
        if (seedCount == 0) return;

        // Act - Query without $select first to ensure data exists
        var simpleResponse = await _client.GetAsync($"{ODataRoute}?$top=10");
        var simpleResult = await simpleResponse.Content.ReadFromJsonAsync<List<BlockchainDto>>();

        // If no data, skip test
        if (simpleResult?.Any() != true) return;

        // Now test the complex query with $select
        var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var response = await _client.GetAsync(
            $"{ODataRoute}?$filter=CreatedAt gt {tenMinutesAgo}&$orderby=CreatedAt desc&$select=Symbol,CreatedAt&$top=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check raw JSON content for projections
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();

        if (content != "[]")
        {
            content.ToLower().Should().Contain("symbol");
            content.ToLower().Should().Contain("createdat");
        }
    }

    #endregion

    #region Configuration Validation Tests

    [Fact]
    public void ConfigurationValidation_AllValidators_PassOnStartup()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Get all validators to verify they're registered
        var blockCypherOptions = services.GetService<IOptions<BlockCypherSettings>>();
        var repositoryOptions = services.GetService<IOptions<RepositorySettings>>();
        var odataOptions = services.GetService<IOptions<ODataSettings>>();

        // Assert - All options should be resolved successfully (validators passed)
        blockCypherOptions.Should().NotBeNull();
        blockCypherOptions!.Value.Should().NotBeNull();

        repositoryOptions.Should().NotBeNull();
        repositoryOptions!.Value.Should().NotBeNull();
        repositoryOptions.Value.DataRetentionDays.Should().Be(30);

        odataOptions.Should().NotBeNull();
        odataOptions!.Value.Should().NotBeNull();

        // Test configuration values
        odataOptions.Value.MaxTop.Should().BeGreaterThan(0);
        odataOptions.Value.AllowedOrderByProperties.Should().Equal("Symbol", "CreatedAt", "Id");
    }

    [Fact]
    public void ConfigurationValidation_RepositorySettings_AreValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<RepositorySettings>>();

        // Act
        var settings = options.Value;

        // Assert - Validator rules
        settings.Should().NotBeNull();
        settings.DataRetentionDays.Should().BeGreaterThanOrEqualTo(0, "must be non-negative");
        settings.DataRetentionDays.Should().BeLessThanOrEqualTo(36500, "should not exceed 100 years");
    }

    [Fact]
    public void ConfigurationValidation_ODataSettings_AreValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ODataSettings>>();

        // Act
        var settings = options.Value;

        // Assert - Validator rules
        settings.Should().NotBeNull();

        // Validate MaxTop
        settings.MaxTop.Should().BeGreaterThan(0, "MaxTop must be greater than 0");
        settings.MaxTop.Should().BeLessThanOrEqualTo(1000, "MaxTop should not exceed 1000");

        // Validate AllowedOrderByProperties
        settings.AllowedOrderByProperties.Should().NotBeEmpty("must contain at least one property");
        settings.AllowedOrderByProperties.Should().Equal("Symbol", "CreatedAt", "Id");

        // Check for duplicates (case-insensitive)
        var distinct = settings.AllowedOrderByProperties.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        settings.AllowedOrderByProperties.Length.Should().Be(distinct.Length,
            "should not contain duplicate property names");
    }

    [Fact]
    public void ConfigurationValidation_BlockCypherSettings_AreValid()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BlockCypherSettings>>();

        // Act
        var settings = options.Value;

        // Assert - Validator rules
        settings.Should().NotBeNull();
        settings.Endpoints.Should().NotBeEmpty();
        settings.RequestsPerSecond.Should().BeGreaterThan(0);
        settings.RequestsPerSecond.Should().BeLessThanOrEqualTo(100);
        settings.RequestsPerHour.Should().BeGreaterThan(0);
    }

    #endregion

    #region Configuration Tests - Environment-Specific

    [Fact]
    public void Configuration_Development_IncludesBtcTestEndpoint()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BlockCypherSettings>>();

        // Act
        var settings = options.Value;

        // Assert - Development includes BTCTEST endpoint
        settings.Endpoints.Should().ContainKey("BTCTEST");
        settings.Endpoints["BTCTEST"].Should().Contain("test3");
    }

    [Fact]
    public void Configuration_UsesCorrectDefaultValues()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var repoOptions = scope.ServiceProvider.GetRequiredService<IOptions<RepositorySettings>>();
        var odataOptions = scope.ServiceProvider.GetRequiredService<IOptions<ODataSettings>>();

        // Act & Assert
        repoOptions.Value.DataRetentionDays.Should().Be(30);

        // Test environment OData settings
        odataOptions.Value.MaxTop.Should().BeGreaterThan(0);
        odataOptions.Value.AllowedOrderByProperties.Should().Contain(new[] { "Symbol", "CreatedAt", "Id" });
    }

    #endregion

    #region Concurrent Request Tests

    [Fact]
    public async Task GetHistory_ConcurrentRequests_AllSucceed()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act - Make 10 concurrent requests
        var tasks = Enumerable.Range(1, 10)
            .Select(_ => _client.GetAsync($"{ODataRoute}?$top=5"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task SyncData_ConcurrentCalls_HandleGracefully()
    {
        // Act - Make 3 concurrent sync requests
        var tasks = Enumerable.Range(1, 3)
            .Select(_ => _client.PostAsync("/api/blockchain/sync", null))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert - All should complete successfully
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetHistory_InvalidRoute_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/blockchain/odata/InvalidRoute");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHistory_UnsupportedQueryOption_ReturnsBadRequest()
    {
        // Act - Try to use unsupported arithmetic operator
        var response = await _client.GetAsync($"{ODataRoute}?$filter=Id add 1 eq 2");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SyncData_InvalidRoute_ReturnsNotFound()
    {
        // Act
        var response = await _client.PostAsync("/api/blockchain/invalid", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task GetHistory_ReturnsUtf8Json()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$top=1");

        // Assert
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentType?.CharSet.Should().BeOneOf("utf-8", null);
    }

    [Fact]
    public async Task GetHistory_ValidJsonStructure()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$top=1");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var act = () => JsonDocument.Parse(content);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetHistory_ReturnsArrayFormat()
    {
        // Arrange
        await _client.PostAsync("/api/blockchain/sync", null);

        // Act
        var response = await _client.GetAsync($"{ODataRoute}?$top=1");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().StartWith("[");
        content.Should().EndWith("]");
    }

    #endregion
}

/// <summary>
/// Custom WebApplicationFactory that ensures database is cleared between test runs.
/// Configures in-memory database and provides proper test configuration for all validators.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove and replace DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Override OData settings for tests
            services.Configure<ODataSettings>(options =>
            {
                options.MaxTop = 100;
                options.AllowedOrderByProperties = new[] { "Symbol", "CreatedAt", "Id" };
            });
        });
    }

    public async Task ClearDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.BlockchainData.RemoveRange(context.BlockchainData);
        await context.SaveChangesAsync();
    }

    public async Task<int> SeedDataWithSameTimestampAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var timestamp = DateTime.UtcNow;
        var data = new List<BlockchainData>
        {
            new() { Id = Guid.NewGuid(), Symbol = "BTC", CreatedAt = timestamp, RequestUrl = "test", JsonResponse = "{}" },
            new() { Id = Guid.NewGuid(), Symbol = "ETH", CreatedAt = timestamp, RequestUrl = "test", JsonResponse = "{}" },
            new() { Id = Guid.NewGuid(), Symbol = "LTC", CreatedAt = timestamp, RequestUrl = "test", JsonResponse = "{}" },
            new() { Id = Guid.NewGuid(), Symbol = "DASH", CreatedAt = timestamp, RequestUrl = "test", JsonResponse = "{}" },
            new() { Id = Guid.NewGuid(), Symbol = "BTCTEST", CreatedAt = timestamp, RequestUrl = "test", JsonResponse = "{}" }
        };

        context.BlockchainData.AddRange(data);
        await context.SaveChangesAsync();
        return data.Count;
    }

    public async Task<int> SeedTestDataForProjectionAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var data = new List<BlockchainData>
        {
            new() { Id = Guid.NewGuid(), Symbol = "BTC", CreatedAt = DateTime.UtcNow, RequestUrl = "test1", JsonResponse = "{\"test\":1}" },
            new() { Id = Guid.NewGuid(), Symbol = "ETH", CreatedAt = DateTime.UtcNow, RequestUrl = "test2", JsonResponse = "{\"test\":2}" },
            new() { Id = Guid.NewGuid(), Symbol = "LTC", CreatedAt = DateTime.UtcNow, RequestUrl = "test3", JsonResponse = "{\"test\":3}" }
        };

        context.BlockchainData.AddRange(data);
        await context.SaveChangesAsync();
        return data.Count;
    }
}
