using FluentAssertions;
using ICMarkets.Blockchain.API.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Extensions;

/// <summary>
/// Unit tests for CorsServiceExtensions.
/// </summary>
public class CorsServiceExtensionsTests
{
    [Fact]
    public void AddConfiguredCors_WithValidConfiguration_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowCredentials"] = "true",
            ["Cors:PreflightMaxAgeSeconds"] = "600"
        });

        // Act
        services.AddConfiguredCors(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddConfiguredCors_WithWildcardOrigin_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "*",
            ["Cors:AllowCredentials"] = "false"
        });

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddConfiguredCors_WithWildcardAndCredentials_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "*",
            ["Cors:AllowCredentials"] = "true"
        });

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot use AllowCredentials with AllowAnyOrigin*");
    }

    [Fact]
    public void AddConfiguredCors_WithEmptyOrigins_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>());

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AllowedOrigins must contain at least one entry*");
    }

    [Fact]
    public void AddConfiguredCors_WithMultipleOrigins_RegistersSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "https://icmarkets.com",
            ["Cors:AllowedOrigins:1"] = "https://app.icmarkets.com",
            ["Cors:AllowedOrigins:2"] = "https://*.icmarkets.com",
            ["Cors:AllowCredentials"] = "true"
        });

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetDefaultCorsPolicyName_ReturnsExpectedName()
    {
        // Act
        var policyName = CorsServiceExtensions.GetDefaultCorsPolicyName();

        // Assert
        policyName.Should().Be("DefaultCorsPolicy");
    }

    [Fact]
    public void AddConfiguredCors_WithCustomMethods_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:AllowedMethods:0"] = "GET",
            ["Cors:AllowedMethods:1"] = "POST",
            ["Cors:AllowCredentials"] = "false"
        });

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddConfiguredCors_WithExposedHeaders_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string>
        {
            ["Cors:AllowedOrigins:0"] = "https://example.com",
            ["Cors:ExposedHeaders:0"] = "X-Pagination",
            ["Cors:ExposedHeaders:1"] = "X-Total-Count"
        });

        // Act
        var act = () => services.AddConfiguredCors(configuration);

        // Assert
        act.Should().NotThrow();
    }

    #region Helper Methods

    private static IConfiguration CreateConfiguration(Dictionary<string, string> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings!)
            .Build();
    }

    #endregion
}
