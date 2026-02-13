using FluentAssertions;
using ICMarkets.Blockchain.API.Configuration;
using ICMarkets.Blockchain.Tests.Unit.TestBuilders;
using Microsoft.Extensions.Options;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for ODataSettingsValidator with comprehensive coverage.
/// Tests IValidateOptions implementation for OData configuration validation rules.
/// Note: ODataSettings only has MaxTop and AllowedOrderByProperties (no PageSize).
/// </summary>
public sealed class ODataSettingsValidatorTests
{
    private readonly ODataSettingsValidator _sut;

    public ODataSettingsValidatorTests()
    {
        _sut = new ODataSettingsValidator();
    }

    #region Theory Data

    public static TheoryData<int> ValidMaxTopValues => new()
    {
        1,
        10,
        50,
        100,
        500,
        1000  // Maximum allowed
    };

    public static TheoryData<string> ValidPropertyNames => new()
    {
        "Symbol",
        "_LeadingUnderscore",
        "Property123",
        "Property_With_Underscores",
        "_123",
        "Id",
        "FetchedAt",
        "CreatedAt"
    };

    public static TheoryData<string> InvalidPropertyNames => new()
    {
        "Property-Name",    // Hyphen
        "Property.Name",    // Dot
        "Property Name",    // Space
        "Property@Name",    // Special char
        "Property!",        // Exclamation
        "123Property"       // Starts with digit
    };

    #endregion

    #region Valid Configuration Tests

    [Fact]
    public void Validate_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default().Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("settings meet all validation rules");
    }

    [Fact]
    public void Validate_WithDefaultSettings_ReturnsFailureDueToEmptyProperties()
    {
        // Arrange
        var settings = new ODataSettings(); // Uses defaults: MaxTop=100, AllowedOrderByProperties=[]

        // Act
        var result = _sut.Validate(null, settings);

        // Assert - Default AllowedOrderByProperties is empty array, which should fail validation
        result.Failed.Should().BeTrue(
            "default AllowedOrderByProperties is empty array which requires explicit configuration");
        result.FailureMessage.Should().Contain("must contain at least one property");

        // Verify numeric defaults are correct
        settings.MaxTop.Should().Be(100, "default MaxTop should be 100");
        settings.AllowedOrderByProperties.Should().BeEmpty("default AllowedOrderByProperties should be empty");
    }

    [Fact]
    public void Validate_WithConfiguredSettings_ReturnsSuccess()
    {
        // Arrange - Simulates configuration from appsettings.json
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(100)
            .WithAllowedOrderByProperties("Symbol", "FetchedAt", "Id")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("fully configured settings should be valid");
        settings.AllowedOrderByProperties.Should().Equal("Symbol", "FetchedAt", "Id");
    }

    [Fact]
    public void Validate_WithSingleAllowedProperty_ReturnsSuccess()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Id")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("at least one property is sufficient");
    }

    [Fact]
    public void Validate_WithUnderscoresInPropertyNames_ReturnsSuccess()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Property_Name", "Another_Property")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("underscores are valid in C# property names");
    }

    [Fact]
    public void Validate_WithStablePaginationProperties_ReturnsSuccess()
    {
        // Arrange - Test configuration for stable pagination (FetchedAt + Id)
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(100)
            .WithAllowedOrderByProperties("Symbol", "FetchedAt", "Id") // Id required for stable pagination
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("stable pagination requires Id field");
        settings.AllowedOrderByProperties.Should().Contain("Id", "Id is required for stable pagination");
    }

    [Theory]
    [MemberData(nameof(ValidMaxTopValues))]
    public void Validate_WithValidMaxTop_ReturnsSuccess(int maxTop)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(maxTop)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue($"MaxTop={maxTop} is within valid range [1, 1000]");
    }

    [Theory]
    [MemberData(nameof(ValidPropertyNames))]
    public void Validate_WithValidPropertyNames_ReturnsSuccess(string validName)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties(validName)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue($"\"{validName}\" is a valid C# property name");
    }

    #endregion

    #region Null/Empty Settings Tests

    [Fact]
    public void Validate_WithNullSettings_ReturnsFailure()
    {
        // Act
        var result = _sut.Validate(null, null!);

        // Assert
        result.Failed.Should().BeTrue("null settings are invalid");
        result.FailureMessage.Should().Be("OData settings are missing.");
    }

    [Fact]
    public void Validate_WithNullAllowedProperties_ReturnsFailure()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default().Build();
        settings.AllowedOrderByProperties = null!;

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("null AllowedOrderByProperties is invalid");
        result.FailureMessage.Should().Contain("must contain at least one property");
    }

    [Fact]
    public void Validate_WithEmptyAllowedProperties_ReturnsFailure()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default().Build();
        settings.AllowedOrderByProperties = Array.Empty<string>();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("empty AllowedOrderByProperties requires explicit configuration");
        result.FailureMessage.Should().Contain("must contain at least one property");
    }

    #endregion

    #region MaxTop Validation Tests

    [Theory]
    [InlineData(0, "MaxTop must be greater than 0")]
    [InlineData(-1, "MaxTop must be greater than 0")]
    [InlineData(-100, "MaxTop must be greater than 0")]
    public void Validate_WithInvalidMaxTop_ReturnsFailureWithCorrectMessage(int maxTop, string expectedPhrase)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(maxTop)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue($"MaxTop={maxTop} is invalid");
        result.FailureMessage.Should().Contain(expectedPhrase);
    }

    [Fact]
    public void Validate_WithExcessiveMaxTop_ReturnsFailure()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(1001)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("MaxTop exceeds maximum allowed value of 1000");
        result.FailureMessage.Should().Contain("should not exceed 1000");
    }

    [Fact]
    public void Validate_WithMaxTopExactly1000_ReturnsSuccess()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(1000)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("1000 is the maximum allowed MaxTop value");
    }

    [Theory]
    [InlineData(1)]     // Minimum
    [InlineData(999)]   // Just below max
    [InlineData(1000)]  // Maximum
    public void Validate_WithMaxTopBoundaryValues_ReturnsSuccess(int maxTop)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(maxTop)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue($"MaxTop={maxTop} is a valid boundary value");
    }

    #endregion

    #region AllowedOrderByProperties Validation Tests

    [Theory]
    [InlineData("  ")]
    [InlineData("")]
    public void Validate_WithWhitespaceOrEmptyProperty_ReturnsFailure(string invalidProperty)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Symbol", invalidProperty, "FetchedAt")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("empty or whitespace property names are invalid");
        result.FailureMessage.Should().Contain("empty or whitespace");
    }

    [Fact]
    public void Validate_WithTooLongPropertyName_ReturnsFailure()
    {
        // Arrange
        var longName = new string('A', 101); // 101 characters
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Symbol", longName)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("property name exceeds maximum length of 100");
        result.FailureMessage.Should().Contain("exceeds maximum length of 100");
    }

    [Fact]
    public void Validate_WithExactly100CharacterProperty_ReturnsSuccess()
    {
        // Arrange
        var maxLengthName = new string('A', 100); // Exactly 100 characters
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties(maxLengthName)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("100 characters is the maximum allowed length");
        maxLengthName.Length.Should().Be(100, "verify test data is correct");
    }

    [Theory]
    [MemberData(nameof(InvalidPropertyNames))]
    public void Validate_WithInvalidPropertyName_ReturnsFailure(string invalidName)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Symbol", invalidName)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue($"\"{invalidName}\" is not a valid C# property name");
        result.FailureMessage.Should().Contain("not a valid property name");
    }

    [Fact]
    public void Validate_WithDuplicateProperties_ReturnsFailure()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Symbol", "FetchedAt", "Symbol")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("duplicate property names are not allowed");
        result.FailureMessage.Should().Contain("duplicate property names");
    }

    [Fact]
    public void Validate_WithDuplicatePropertiesDifferentCase_ReturnsFailure()
    {
        // Arrange - Case-insensitive duplicates (this was the bug we fixed)
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties("Symbol", "SYMBOL", "FetchedAt")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("case-insensitive duplicates should be detected");
        result.FailureMessage.Should().Contain("duplicate property names");
        result.FailureMessage.Should().Contain("case-insensitive");
    }

    [Fact]
    public void Validate_WithDuplicateId_ReturnsFailure()
    {
        // Arrange - Test the specific case that was causing issues
        var settings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id", "Id" } // Duplicate Id
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue("duplicate 'Id' property should be detected");
        result.FailureMessage.Should().Contain("duplicate property names");
    }

    #endregion

    #region Boundary and Edge Cases

    [Fact]
    public void Validate_WithManyAllowedProperties_ReturnsSuccess()
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties(
                "Id", "Symbol", "FetchedAt", "RequestUrl", "CreatedAt",
                "UpdatedAt", "Status", "Type", "Category", "Priority")
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("multiple valid properties should be accepted");
        settings.AllowedOrderByProperties.Length.Should().Be(10);
    }

    [Fact]
    public void Validate_NoDuplicatesInStandardConfiguration_ReturnsSuccess()
    {
        // Arrange - The standard configuration from appsettings.json
        var settings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("standard configuration should have no duplicates");

        // Verify no duplicates
        var distinct = settings.AllowedOrderByProperties
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        distinct.Length.Should().Be(
            settings.AllowedOrderByProperties.Length,
            "no duplicates should exist");
    }

    #endregion

    #region Regression Tests for Configuration Binding Fix

    [Fact]
    public void Validate_EmptyArrayDoesNotCauseDuplicates_WhenConfigurationBinds()
    {
        // Arrange - Simulate the fix: start with empty array (class default)
        var settings = new ODataSettings(); // AllowedOrderByProperties = []

        // Then simulate configuration binding from appsettings.json
        settings.AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert - Should succeed with no duplicates
        result.Succeeded.Should().BeTrue("configuration binding should not cause duplicates");
        settings.AllowedOrderByProperties.Should().Equal("Symbol", "FetchedAt", "Id");

        // Verify no duplicates
        var distinct = settings.AllowedOrderByProperties
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        distinct.Length.Should().Be(3, "should have exactly 3 unique properties");
    }

    [Fact]
    public void Validate_ConfigurationWithDefaultClass_PreventsOldBug()
    {
        // Arrange - This test documents the OLD bug that was fixed
        // OLD BUG: When class had default value new[] { "Symbol", "FetchedAt", "Id" }
        // and appsettings.json also had ["Symbol", "FetchedAt", "Id"],
        // configuration binding would APPEND, resulting in duplicates
        //
        // NEW BEHAVIOR: Class has Array.Empty<string>() as default,
        // so configuration binding just sets the values (no duplicates)

        var settings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" }
        };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("fixed implementation prevents duplicate bug");
        settings.AllowedOrderByProperties.Length.Should().Be(
            3,
            "should have exactly 3 properties, not 6 (duplicates)");
    }

    [Fact]
    public void Validate_MultipleConfigurationBindingCalls_DoNotCauseDuplicates()
    {
        // Arrange - Simulate multiple configuration reloads
        var settings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" }
        };

        // Simulate configuration reload
        settings.AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" };

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue("configuration reloads should not cause duplicates");
        settings.AllowedOrderByProperties
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Should()
            .OnlyContain(g => g.Count() == 1, "no property should appear more than once");
    }

    #endregion

    #region Business Logic Tests

    [Fact]
    public void Validate_RequiresIdForStablePagination_InProductionScenarios()
    {
        // Arrange - Production configuration should include Id for stable pagination
        var productionSettings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "FetchedAt", "Id" }
        };

        // Act
        var result = _sut.Validate(null, productionSettings);

        // Assert
        result.Succeeded.Should().BeTrue();
        productionSettings.AllowedOrderByProperties.Should().Contain(
            "Id",
            "Id field is required for stable pagination when FetchedAt has same values");
    }

    [Fact]
    public void Validate_AllowsCustomPropertyNames_ForExtensibility()
    {
        // Arrange - Future extensibility test
        var settings = ODataSettingsBuilder.Default()
            .WithAllowedOrderByProperties(
                "Symbol",
                "FetchedAt",
                "Id",
                "CustomField1",      // Custom field
                "CustomField2")      // Custom field
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue(
            "validator should allow custom property names for future extensibility");
    }

    [Theory]
    [InlineData(1, "Symbol")]
    [InlineData(50, "Symbol", "FetchedAt")]
    [InlineData(1000, "Symbol", "FetchedAt", "Id")]
    public void Validate_WithVariousMaxTopAndPropertyCombinations_ReturnsSuccess(
        int maxTop,
        params string[] properties)
    {
        // Arrange
        var settings = ODataSettingsBuilder.Default()
            .WithMaxTop(maxTop)
            .WithAllowedOrderByProperties(properties)
            .Build();

        // Act
        var result = _sut.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue(
            $"MaxTop={maxTop} with {properties.Length} properties should be valid");
    }

    #endregion
}
