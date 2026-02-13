using FluentAssertions;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using ICMarkets.Blockchain.Tests.Unit.TestBuilders;
using Microsoft.Extensions.Options;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for RepositorySettingsValidator.
/// Tests IValidateOptions implementation using Test Builder pattern.
/// </summary>
public sealed class RepositorySettingsValidatorTests
{
    private readonly RepositorySettingsValidator _sut;

    public RepositorySettingsValidatorTests()
    {
        _sut = new RepositorySettingsValidator();
    }

    #region Theory Data

    public static TheoryData<int> ValidRetentionDays => new()
    {
        0,      // Minimum (no retention)
        1,      // One day
        7,      // One week
        30,     // One month (default)
        90,     // Three months
        365,    // One year
        730,    // Two years
        3650,   // Ten years
        36500   // Maximum (100 years)
    };

    public static TheoryData<int> InvalidRetentionDays => new()
    {
        -1,
        -10,
        -100,
        36501,
        50000,
        100000
    };

    #endregion

    #region Valid Configuration Tests

    [Theory]
    [MemberData(nameof(ValidRetentionDays))]
    public void Validate_WithValidRetentionDays_ReturnsSuccess(int days)
    {
        // Arrange
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(days)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue($"{days} days is within valid range [0, 36500]");
    }

    [Fact]
    public void Validate_WithDefaultSettings_ReturnsSuccess()
    {
        // Arrange
        var settings = new RepositorySettings(); // Uses default: 30 days

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("default settings should be valid");
        settings.DataRetentionDays.Should().Be(30, "default retention is 30 days");
    }

    #endregion

    #region Invalid Configuration Tests

    [Fact]
    public void Validate_WithNullSettings_ReturnsFailure()
    {
        // Act
        var result = _sut.Validate(null, null!);

        // Assert
        result.Failed.Should().BeTrue("null settings are invalid");
        result.FailureMessage.Should().Be("RepositorySettings are missing.");
    }

    [Theory]
    [MemberData(nameof(InvalidRetentionDays))]
    public void Validate_WithInvalidRetentionDays_ReturnsFailure(int days)
    {
        // Arrange
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(days)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue($"{days} days is outside valid range [0, 36500]");
    }

    [Theory]
    [InlineData(-1, "must be non-negative")]
    [InlineData(-100, "must be non-negative")]
    public void Validate_WithNegativeRetention_ReturnsFailureWithCorrectMessage(int days, string expectedPhrase)
    {
        // Arrange
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(days)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(expectedPhrase);
    }

    [Theory]
    [InlineData(36501, "should not exceed 36500")]
    [InlineData(100000, "100 years")]
    public void Validate_WithExcessiveRetention_ReturnsFailureWithCorrectMessage(int days, string expectedPhrase)
    {
        // Arrange
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(days)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(expectedPhrase);
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void Validate_WithZeroRetention_MeansNoHistoricalDataReturned()
    {
        // Arrange
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(0)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue(
            "zero retention is valid and means only current snapshot, no history");
    }

    [Fact]
    public void Validate_WithMaximumRetention_AllowsHundredYears()
    {
        // Arrange
        const int hundredYearsInDays = 36500;
        var settings = RepositorySettingsBuilder.Default()
            .WithDataRetentionDays(hundredYearsInDays)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("100 years is the maximum allowed retention period");
    }

    #endregion
}
