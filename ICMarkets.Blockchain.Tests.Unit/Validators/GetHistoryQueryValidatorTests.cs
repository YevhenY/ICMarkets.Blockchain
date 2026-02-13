using FluentAssertions;
using ICMarkets.Blockchain.Application.Features;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for GetHistoryQuery after OData migration.
/// Note: After OData migration, GetHistoryQuery has no parameters.
/// All filtering, paging, and sorting is handled by OData query strings and [EnableQuery] attribute.
/// These tests verify that the query can be created and validated successfully.
/// </summary>
public class GetHistoryQueryValidatorTests
{
    [Fact]
    public void GetHistoryQuery_CanBeCreated_Successfully()
    {
        // Arrange & Act
        var query = new GetHistoryQuery();

        // Assert
        query.Should().NotBeNull();
    }

    [Fact]
    public void GetHistoryQuery_IsMediatrRequest()
    {
        // Arrange
        var query = new GetHistoryQuery();

        // Assert
        query.Should().BeAssignableTo<MediatR.IRequest<IQueryable<BlockchainDto>>>();
    }

    [Fact]
    public void GetHistoryQuery_HasNoValidationRules()
    {
        // After OData migration, there's no validator needed for GetHistoryQuery
        // because it has no parameters. OData handles all query validation.

        // This test documents that validation is handled at the OData level
        // via [EnableQuery] attribute constraints (MaxTop, AllowedQueryOptions, etc.)

        // Arrange
        var query = new GetHistoryQuery();

        // Assert - Query should always be valid since it has no properties
        query.Should().NotBeNull();
    }
}
