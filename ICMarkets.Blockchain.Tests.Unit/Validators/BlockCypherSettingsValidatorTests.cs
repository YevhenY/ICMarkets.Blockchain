using FluentAssertions;
using ICMarkets.Blockchain.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for BlockCypherSettingsValidator.
/// Tests IValidateOptions implementation for all configuration validation rules.
/// Updated to test all new configurable settings.
/// </summary>
public class BlockCypherSettingsValidatorTests
{
    private readonly BlockCypherSettingsValidator _validator;

    public BlockCypherSettingsValidatorTests()
    {
        _validator = new BlockCypherSettingsValidator();
    }

    #region Valid Configuration Tests

    [Fact]
    public void Validate_WithValidSettings_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidSymbolsAndHyphens_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC-TEST3", "https://api.blockcypher.com/v1/btc/test3" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithValidSymbolsAndUnderscores_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "TEST_COIN", "https://api.example.com/v1/testcoin/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAllConfigurationProperties_ReturnsSuccess()
    {
        // Arrange
        var settings = new BlockCypherSettings
        {
            Endpoints = new Dictionary<string, string>
            {
                { "BTC", "https://api.blockcypher.com/v1/btc/main" },
                { "ETH", "https://api.blockcypher.com/v1/eth/main" }
            },
            RequestsPerSecond = 5,
            RequestsPerHour = 200,
            MaxConcurrentRequests = 5,
            HttpTimeoutSeconds = 60,
            TokenCheckDelayMs = 100,
            DefaultCircuitBreakerDurationSeconds = 1800,
            InitialRemainingRequests = 200
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Null/Empty Settings Tests

    [Fact]
    public void Validate_WithNullSettings_ReturnsFailure()
    {
        // Act
        var result = _validator.Validate(null, null!);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Be("BlockCypher settings are missing.");
    }

    [Fact]
    public void Validate_WithNullEndpoints_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = null!;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must contain at least one endpoint");
    }

    [Fact]
    public void Validate_WithEmptyEndpoints_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>();

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must contain at least one endpoint");
    }

    #endregion

    #region Symbol Validation Tests

    [Fact]
    public void Validate_WithSymbolTooShort_ReturnsFailure()
    {
        // Arrange - 2 characters (below min of 3)
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "AB", "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must be between 3 and 20 characters");
    }

    [Fact]
    public void Validate_WithSymbolTooLong_ReturnsFailure()
    {
        // Arrange - 21 characters (above max of 20)
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "ABCDEFGHIJ12345678901", "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must be between 3 and 20 characters");
    }

    [Fact]
    public void Validate_WithSymbolExactly20Characters_ReturnsSuccess()
    {
        // Arrange - Exactly 20 characters (boundary test)
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "ABCDEFGHIJ1234567890", "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithSymbolExactly3Characters_ReturnsSuccess()
    {
        // Arrange - Exactly 3 characters (boundary test)
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "ABC", "https://api.blockcypher.com/v1/abc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithSymbolContainingInvalidCharacters_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC!", "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must contain only letters, digits, hyphens, or underscores");
    }

    [Theory]
    [InlineData("BTC@USD")]
    [InlineData("BTC USD")]
    [InlineData("BTC.COM")]
    [InlineData("BTC/USD")]
    public void Validate_WithSymbolContainingSpecialCharacters_ReturnsFailure(string symbol)
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { symbol, "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
    }

    #endregion

    #region URL Validation Tests

    [Fact]
    public void Validate_WithEmptyUrl_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("must not be empty");
    }

    [Fact]
    public void Validate_WithInvalidUrl_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "not-a-valid-url" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("is not a valid HTTP/HTTPS URL");
    }

    [Fact]
    public void Validate_WithNonHttpUrl_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "ftp://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("is not a valid HTTP/HTTPS URL");
    }

    [Fact]
    public void Validate_WithHttpUrl_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "http://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithHttpsUrl_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "https://api.blockcypher.com/v1/btc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Rate Limit Tests

    [Fact]
    public void Validate_WithZeroRequestsPerSecond_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerSecond = 0;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RequestsPerSecond must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeRequestsPerSecond_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerSecond = -1;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RequestsPerSecond must be greater than 0");
    }

    [Fact]
    public void Validate_WithRequestsPerSecondOver100_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerSecond = 101;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should not exceed 100");
    }

    [Fact]
    public void Validate_WithZeroRequestsPerHour_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerHour = 0;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RequestsPerHour must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeRequestsPerHour_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerHour = -100;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RequestsPerHour must be greater than 0");
    }

    #endregion

    #region MaxConcurrentRequests Validation Tests

    [Fact]
    public void Validate_WithZeroMaxConcurrentRequests_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.MaxConcurrentRequests = 0;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxConcurrentRequests must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeMaxConcurrentRequests_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.MaxConcurrentRequests = -1;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxConcurrentRequests must be greater than 0");
    }

    [Fact]
    public void Validate_WithMaxConcurrentRequestsOver10_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.MaxConcurrentRequests = 11;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should not exceed 10");
    }

    [Fact]
    public void Validate_WithMaxConcurrentRequestsExactly10_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.MaxConcurrentRequests = 10;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region HttpTimeoutSeconds Validation Tests

    [Fact]
    public void Validate_WithZeroHttpTimeoutSeconds_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.HttpTimeoutSeconds = 0;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HttpTimeoutSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeHttpTimeoutSeconds_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.HttpTimeoutSeconds = -1;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("HttpTimeoutSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_WithHttpTimeoutSecondsOver300_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.HttpTimeoutSeconds = 301;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should not exceed 300 seconds");
    }

    [Fact]
    public void Validate_WithHttpTimeoutSecondsExactly300_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.HttpTimeoutSeconds = 300;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region TokenCheckDelayMs Validation Tests

    [Fact]
    public void Validate_WithTokenCheckDelayMsBelow10_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.TokenCheckDelayMs = 9;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should be at least 10ms");
    }

    [Fact]
    public void Validate_WithTokenCheckDelayMsExactly10_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.TokenCheckDelayMs = 10;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTokenCheckDelayMsOver1000_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.TokenCheckDelayMs = 1001;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should not exceed 1000ms");
    }

    [Fact]
    public void Validate_WithTokenCheckDelayMsExactly1000_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.TokenCheckDelayMs = 1000;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region DefaultCircuitBreakerDurationSeconds Validation Tests

    [Fact]
    public void Validate_WithZeroDefaultCircuitBreakerDurationSeconds_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.DefaultCircuitBreakerDurationSeconds = 0;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultCircuitBreakerDurationSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_WithNegativeDefaultCircuitBreakerDurationSeconds_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.DefaultCircuitBreakerDurationSeconds = -1;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DefaultCircuitBreakerDurationSeconds must be greater than 0");
    }

    [Fact]
    public void Validate_WithDefaultCircuitBreakerDurationSecondsOver86400_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.DefaultCircuitBreakerDurationSeconds = 86401; // Over 24 hours

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should not exceed 86400 seconds");
    }

    [Fact]
    public void Validate_WithDefaultCircuitBreakerDurationSecondsExactly86400_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.DefaultCircuitBreakerDurationSeconds = 86400; // Exactly 24 hours

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region InitialRemainingRequests Validation Tests

    [Fact]
    public void Validate_WithNegativeInitialRemainingRequests_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.InitialRemainingRequests = -1;

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("InitialRemainingRequests must be non-negative");
    }

    [Fact]
    public void Validate_WithInitialRemainingRequestsNotMatchingRequestsPerHour_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerHour = 100;
        settings.InitialRemainingRequests = 50; // Mismatch

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("should match RequestsPerHour");
    }

    [Fact]
    public void Validate_WithInitialRemainingRequestsMatchingRequestsPerHour_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.RequestsPerHour = 200;
        settings.InitialRemainingRequests = 200; // Match

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Multiple Endpoints Tests

    [Fact]
    public void Validate_WithMultipleValidEndpoints_ReturnsSuccess()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "https://api.blockcypher.com/v1/btc/main" },
            { "ETH", "https://api.blockcypher.com/v1/eth/main" },
            { "DASH", "https://api.blockcypher.com/v1/dash/main" },
            { "LTC", "https://api.blockcypher.com/v1/ltc/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithOneInvalidEndpointAmongMany_ReturnsFailure()
    {
        // Arrange
        var settings = CreateValidSettings();
        settings.Endpoints = new Dictionary<string, string>
        {
            { "BTC", "https://api.blockcypher.com/v1/btc/main" },
            { "INVALID!", "https://api.blockcypher.com/v1/eth/main" },
            { "DASH", "https://api.blockcypher.com/v1/dash/main" }
        };

        // Act
        var result = _validator.Validate(null, settings);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("INVALID!");
    }

    #endregion

    #region Helper Methods

    private BlockCypherSettings CreateValidSettings()
    {
        return new BlockCypherSettings
        {
            Endpoints = new Dictionary<string, string>
            {
                { "BTC", "https://api.blockcypher.com/v1/btc/main" },
                { "ETH", "https://api.blockcypher.com/v1/eth/main" }
            },
            RequestsPerSecond = 3,
            RequestsPerHour = 100,
            MaxConcurrentRequests = 3,
            HttpTimeoutSeconds = 30,
            TokenCheckDelayMs = 50,
            DefaultCircuitBreakerDurationSeconds = 3600,
            InitialRemainingRequests = 100
        };
    }

    #endregion
}
