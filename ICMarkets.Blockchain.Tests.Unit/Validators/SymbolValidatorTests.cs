using FluentAssertions;
using ICMarkets.Blockchain.Domain.Validation;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for SymbolValidator domain validation logic.
/// Tests business rules for blockchain symbol validation with comprehensive coverage.
/// </summary>
public sealed class SymbolValidatorTests
{
    #region Theory Data Classes

    public static TheoryData<string> ValidSymbols => new()
    {
        "BTC",
        "ETH",
        "DASH",
        "LTC",
        "DOGE",
        "BTC-TEST3",
        "TEST_COIN",
        "ABC",                      // Min length boundary (3)
        "ABCDEFGHIJ1234567890",     // Max length boundary (20)
        "BtC",                      // Mixed case
        "BTC2",                     // With numbers
        "TEST_123-COIN"             // Mixed special chars
    };

    public static TheoryData<string?> InvalidSymbols => new()
    {
        null,
        "",
        "   ",
        "AB",                       // Too short
        "ABCDEFGHIJ12345678901",    // Too long (21)
        "BTC!",
        "BTC@USD",
        "BTC USD",
        "BTC.USD",
        "BTC/USD",
        "BTC#123"
    };

    #endregion

    #region IsValid Tests

    [Theory]
    [MemberData(nameof(ValidSymbols))]
    public void IsValid_WithValidSymbol_ReturnsTrue(string symbol)
    {
        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeTrue($"\"{symbol}\" is a valid symbol format");
    }

    [Theory]
    [MemberData(nameof(InvalidSymbols))]
    public void IsValid_WithInvalidSymbol_ReturnsFalse(string? symbol)
    {
        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeFalse($"\"{symbol ?? "<null>"}\" violates symbol validation rules");
    }

    [Theory]
    [InlineData("BTC-TEST", true)]
    [InlineData("TEST_COIN", true)]
    [InlineData("BTC!", false)]
    [InlineData("BTC@USD", false)]
    public void IsValid_WithSpecialCharacters_ReturnsExpectedResult(string symbol, bool expected)
    {
        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().Be(expected, $"symbol \"{symbol}\" should {(expected ? "pass" : "fail")} validation");
    }

    #endregion

    #region GetValidationError Tests

    [Theory]
    [InlineData(null, "Symbol is required.")]
    [InlineData("", "Symbol is required.")]
    [InlineData("   ", "Symbol is required.")]
    public void GetValidationError_WithNullOrEmpty_ReturnsRequiredMessage(string? symbol, string expected)
    {
        // Act
        var error = SymbolValidator.GetValidationError(symbol);

        // Assert
        error.Should().Be(expected);
    }

    [Theory]
    [InlineData("A", "Symbol must be between 3 and 20 characters.")]
    [InlineData("AB", "Symbol must be between 3 and 20 characters.")]
    [InlineData("ABCDEFGHIJ12345678901", "Symbol must be between 3 and 20 characters.")]
    public void GetValidationError_WithInvalidLength_ReturnsLengthMessage(string symbol, string expected)
    {
        // Act
        var error = SymbolValidator.GetValidationError(symbol);

        // Assert
        error.Should().Be(expected);
    }

    [Theory]
    [InlineData("BTC!", "Symbol must contain only letters, digits, hyphens, or underscores.")]
    [InlineData("BTC@USD", "Symbol must contain only letters, digits, hyphens, or underscores.")]
    [InlineData("BTC USD", "Symbol must contain only letters, digits, hyphens, or underscores.")]
    [InlineData("BTC.COM", "Symbol must contain only letters, digits, hyphens, or underscores.")]
    public void GetValidationError_WithInvalidCharacters_ReturnsPatternMessage(string symbol, string expected)
    {
        // Act
        var error = SymbolValidator.GetValidationError(symbol);

        // Assert
        error.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ValidSymbols))]
    public void GetValidationError_WithValidSymbol_ReturnsNull(string symbol)
    {
        // Act
        var error = SymbolValidator.GetValidationError(symbol);

        // Assert
        error.Should().BeNull($"\"{symbol}\" is valid and should have no error");
    }

    [Fact]
    public void GetValidationError_WithCustomFieldName_UsesCustomFieldNameInMessage()
    {
        // Arrange
        const string symbol = "AB";
        const string customFieldName = "Blockchain Symbol";

        // Act
        var error = SymbolValidator.GetValidationError(symbol, customFieldName);

        // Assert
        error.Should().StartWith(customFieldName, "custom field name should be used in error message");
        error.Should().Be("Blockchain Symbol must be between 3 and 20 characters.");
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void IsValid_WithExactMinLength_ReturnsTrue()
    {
        // Arrange
        const string symbol = "ABC"; // Exactly 3 characters

        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeTrue("minimum length boundary should be valid");
    }

    [Fact]
    public void IsValid_WithExactMaxLength_ReturnsTrue()
    {
        // Arrange
        const string symbol = "ABCDEFGHIJ1234567890"; // Exactly 20 characters

        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeTrue("maximum length boundary should be valid");
        symbol.Length.Should().Be(20, "verify test data is correct");
    }

    [Fact]
    public void IsValid_WithOneBelowMinLength_ReturnsFalse()
    {
        // Arrange
        const string symbol = "AB"; // 2 characters

        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeFalse("one character below minimum should be invalid");
    }

    [Fact]
    public void IsValid_WithOneAboveMaxLength_ReturnsFalse()
    {
        // Arrange
        const string symbol = "ABCDEFGHIJ12345678901"; // 21 characters

        // Act
        var result = SymbolValidator.IsValid(symbol);

        // Assert
        result.Should().BeFalse("one character above maximum should be invalid");
        symbol.Length.Should().Be(21, "verify test data is correct");
    }

    #endregion

    #region Constants Tests

    [Fact]
    public void MinLength_IsThree()
    {
        // Assert
        SymbolValidator.MinLength.Should().Be(3, "minimum symbol length is defined as 3");
    }

    [Fact]
    public void MaxLength_IsTwenty()
    {
        // Assert
        SymbolValidator.MaxLength.Should().Be(20, "maximum symbol length matches database schema");
    }

    [Fact]
    public void Constants_MatchBlockchainDataRules()
    {
        // Assert
        SymbolValidator.MinLength.Should().Be(
            BlockchainDataRules.Symbol.MinLength,
            "validator constants should match domain rules");

        SymbolValidator.MaxLength.Should().Be(
            BlockchainDataRules.Symbol.MaxLength,
            "validator constants should match domain rules");
    }

    #endregion
}
