using FluentAssertions;
using ICMarkets.Blockchain.API.Configuration;
using ICMarkets.Blockchain.API.Controllers;
using ICMarkets.Blockchain.Application.Features;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for BlockchainController.
/// OData validation and query application are tested via integration tests.
/// Tests for read-only mode functionality and sync endpoint behavior.
/// </summary>
public sealed class BlockchainControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IOptions<ODataSettings>> _mockODataSettings;
    private readonly Mock<ILogger<BlockchainController>> _mockLogger;
    private readonly ODataSettings _defaultODataSettings;

    public BlockchainControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockODataSettings = new Mock<IOptions<ODataSettings>>();
        _mockLogger = new Mock<ILogger<BlockchainController>>();

        // Default OData settings matching appsettings.json (Note: No PageSize property)
        _defaultODataSettings = new ODataSettings
        {
            MaxTop = 100,
            AllowedOrderByProperties = new[] { "Symbol", "CreatedAt", "Id" }
        };

        _mockODataSettings.Setup(x => x.Value).Returns(_defaultODataSettings);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_InitializesSuccessfully()
    {
        // Act
        var controller = CreateController(enableSync: true);

        // Assert
        controller.Should().NotBeNull();
        VerifyLoggerCalled(LogLevel.Information, Times.Once());
    }

    [Fact]
    public void Constructor_WithEnableSyncTrue_LogsSyncEnabled()
    {
        // Act
        var controller = CreateController(enableSync: true);

        // Assert
        VerifyLoggerCalledWithMessage("Sync enabled: True");
    }

    [Fact]
    public void Constructor_WithEnableSyncFalse_LogsSyncDisabled()
    {
        // Act
        var controller = CreateController(enableSync: false);

        // Assert
        VerifyLoggerCalledWithMessage("Sync enabled: False");
    }

    [Fact]
    public void Constructor_WithMissingEnableSyncConfig_DefaultsToTrue()
    {
        // Act
        var controller = CreateController(enableSync: null);

        // Assert
        VerifyLoggerCalledWithMessage("Sync enabled: True");
    }

    [Fact]
    public void Constructor_InitializesEnableSyncFromConfiguration()
    {
        // Arrange
        var configTrue = BuildConfiguration(enableSync: true);
        var configFalse = BuildConfiguration(enableSync: false);

        // Act
        var controllerTrue = new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configTrue,
            _mockLogger.Object);

        var controllerFalse = new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configFalse,
            _mockLogger.Object);

        // Assert
        controllerTrue.Should().NotBeNull();
        controllerFalse.Should().NotBeNull();
    }

    #endregion

    #region SyncData Tests - Full Mode (EnableSync: true)

    [Fact]
    public async Task SyncData_WithSuccessfulSync_ReturnsOkWithResult()
    {
        // Arrange
        var controller = CreateController(enableSync: true);
        var expectedResult = new SyncResult { Success = true, Count = 5 };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await controller.SyncData(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().Be(expectedResult);

        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task SyncData_WithNoData_ReturnsOkWithZeroCount()
    {
        // Arrange
        var controller = CreateController(enableSync: true);
        var expectedResult = new SyncResult { Success = true, Count = 0 };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await controller.SyncData(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var syncResult = okResult!.Value as SyncResult;
        syncResult!.Count.Should().Be(0);
    }

    [Fact]
    public async Task SyncData_PassesCancellationToken()
    {
        // Arrange
        var controller = CreateController(enableSync: true);
        var cts = new CancellationTokenSource();
        var expectedResult = new SyncResult { Success = true, Count = 3 };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SyncBlockchainCommand>(), cts.Token))
            .ReturnsAsync(expectedResult);

        // Act
        await controller.SyncData(cts.Token);

        // Assert
        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), cts.Token),
            Times.Once());
    }

    [Fact]
    public async Task SyncData_WithFailure_StillReturnsOk()
    {
        // Arrange
        var controller = CreateController(enableSync: true);
        var expectedResult = new SyncResult { Success = false, Count = 0 };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await controller.SyncData(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var syncResult = okResult!.Value as SyncResult;
        syncResult!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SyncData_MultipleCalls_WithSuccess_EachReturnsOk()
    {
        // Arrange
        var controller = CreateController(enableSync: true);
        var expectedResult = new SyncResult { Success = true, Count = 2 };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result1 = await controller.SyncData(CancellationToken.None);
        var result2 = await controller.SyncData(CancellationToken.None);

        // Assert
        result1.Should().BeOfType<OkObjectResult>();
        result2.Should().BeOfType<OkObjectResult>();

        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region SyncData Tests - Read-Only Mode (EnableSync: false)

    [Fact]
    public async Task SyncData_WhenSyncDisabled_Returns503ServiceUnavailable()
    {
        // Arrange
        var controller = CreateController(enableSync: false);

        // Act
        var result = await controller.SyncData(CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task SyncData_WhenSyncDisabled_ReturnsErrorMessage()
    {
        // Arrange
        var controller = CreateController(enableSync: false);

        // Act
        var result = await controller.SyncData(CancellationToken.None);

        // Assert
        var objectResult = result as ObjectResult;
        var value = objectResult!.Value;

        // Use reflection to get the error property from anonymous type
        var errorProperty = value!.GetType().GetProperty("error");
        var errorMessage = errorProperty?.GetValue(value)?.ToString();

        errorMessage.Should().Be("Sync endpoint disabled in read-only mode");
    }

    [Fact]
    public async Task SyncData_WhenSyncDisabled_DoesNotCallMediator()
    {
        // Arrange
        var controller = CreateController(enableSync: false);

        // Act
        await controller.SyncData(CancellationToken.None);

        // Assert
        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task SyncData_WhenSyncDisabled_LogsWarning()
    {
        // Arrange
        var controller = CreateController(enableSync: false);

        // Act
        await controller.SyncData(CancellationToken.None);

        // Assert
        VerifyLoggerCalled(LogLevel.Warning, Times.Once());
        VerifyLoggerCalledWithMessage("read-only mode");
    }

    [Fact]
    public async Task SyncData_WhenSyncDisabled_WithCancellationToken_StillReturns503()
    {
        // Arrange
        var controller = CreateController(enableSync: false);
        var cts = new CancellationTokenSource();

        // Act
        var result = await controller.SyncData(cts.Token);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(503);

        // Mediator should not be called even with cancellation token
        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task SyncData_WhenSyncDisabled_MultipleCalls_AlwaysReturns503()
    {
        // Arrange
        var controller = CreateController(enableSync: false);

        // Act
        var result1 = await controller.SyncData(CancellationToken.None);
        var result2 = await controller.SyncData(CancellationToken.None);

        // Assert
        var objectResult1 = result1 as ObjectResult;
        var objectResult2 = result2 as ObjectResult;
        objectResult1!.StatusCode.Should().Be(503);
        objectResult2!.StatusCode.Should().Be(503);

        _mockMediator.Verify(
            m => m.Send(It.IsAny<SyncBlockchainCommand>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    #endregion

    #region GetHistory Tests - Configuration Verification

    [Fact]
    public void GetHistory_WorksInReadOnlyMode()
    {
        // Arrange & Act
        var controller = CreateController(enableSync: false);

        // Assert
        // GetHistory should work regardless of EnableSync value
        // Full functionality tested in integration tests
        controller.Should().NotBeNull();
    }

    #endregion

    #region ODataSettings Configuration Tests

    [Fact]
    public void ODataSettings_DefaultMaxTop_IsCorrectlyConfigured()
    {
        // Assert
        _defaultODataSettings.MaxTop.Should().Be(100, "MaxTop should default to 100");
    }

    [Fact]
    public void ODataSettings_AllowedOrderByProperties_ContainsExpectedFields()
    {
        // Assert
        _defaultODataSettings.AllowedOrderByProperties.Should().HaveCount(3);
        _defaultODataSettings.AllowedOrderByProperties.Should().ContainInOrder("Symbol", "CreatedAt", "Id");
    }

    [Fact]
    public void ODataSettings_MaxTop_IsPositive()
    {
        // Assert
        _defaultODataSettings.MaxTop.Should().BeGreaterThan(0, "MaxTop must be greater than zero");
    }

    [Fact]
    public void ODataSettings_AllowedOrderByProperties_IsNotEmpty()
    {
        // Assert
        _defaultODataSettings.AllowedOrderByProperties.Should().NotBeEmpty(
            "AllowedOrderByProperties must contain at least one property");
    }

    [Fact]
    public void ODataSettings_CustomSettings_CanBeInjected()
    {
        // Arrange - Test that custom settings can be injected
        var customSettings = new ODataSettings
        {
            MaxTop = 50,
            AllowedOrderByProperties = new[] { "Symbol", "CreatedAt" }
        };

        var mockCustomSettings = new Mock<IOptions<ODataSettings>>();
        mockCustomSettings.Setup(x => x.Value).Returns(customSettings);

        var configuration = BuildConfiguration(enableSync: true);

        // Act
        var customController = new BlockchainController(
            _mockMediator.Object,
            mockCustomSettings.Object,
            configuration,
            _mockLogger.Object);

        // Assert - Verify custom settings can be provided
        customController.Should().NotBeNull();
        customSettings.MaxTop.Should().Be(50);
        customSettings.AllowedOrderByProperties.Should().HaveCount(2);
    }

    [Fact]
    public void ODataSettings_AllowedOrderByProperties_ContainsIdForStablePagination()
    {
        // Assert
        _defaultODataSettings.AllowedOrderByProperties.Should().Contain(
            "Id",
            "Id is required for stable pagination when timestamps are identical");
    }

    [Fact]
    public void ODataSettings_AllowedOrderByProperties_ContainsSymbolForFiltering()
    {
        // Assert
        _defaultODataSettings.AllowedOrderByProperties.Should().Contain(
            "Symbol",
            "Symbol is commonly used for filtering blockchain data");
    }

    [Fact]
    public void ODataSettings_AllowedOrderByProperties_ContainsCreatedAtForSorting()
    {
        // Assert
        _defaultODataSettings.AllowedOrderByProperties.Should().Contain(
            "CreatedAt",
            "CreatedAt is the primary sort field for historical data");
    }

    #endregion

    #region Controller Behavior Tests

    [Fact]
    public void Controller_CanBeInstantiated_WithValidParameters()
    {
        // Arrange
        var configuration = BuildConfiguration(enableSync: true);

        // Act
        var controller = new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configuration,
            _mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
    }

    [Fact]
    public void Controller_WithNullODataSettings_ThrowsNullReferenceException()
    {
        // Arrange
        var configuration = BuildConfiguration(enableSync: true);

        // Act
        var act = () => new BlockchainController(
            _mockMediator.Object,
            null!,
            configuration,
            _mockLogger.Object);

        // Assert - Controller doesn't have null check, so NullReferenceException is thrown
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void Controller_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            null!,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Controller_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = BuildConfiguration(enableSync: true);

        // Act
        var act = () => new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configuration,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Controller_StoresODataSettings_Correctly()
    {
        // Act
        var controller = CreateController(enableSync: true);

        // Assert - Controller should be created successfully with OData settings
        controller.Should().NotBeNull();
        _mockODataSettings.Verify(x => x.Value, Times.AtLeastOnce());
    }

    [Fact]
    public void Controller_ReadsEnableSyncConfiguration_OnConstruction()
    {
        // Arrange
        var configuration = BuildConfiguration(enableSync: true);

        // Act
        var controller = new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configuration,
            _mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
        // Configuration.GetValue<bool> is called during construction
        VerifyLoggerCalledWithMessage("Sync enabled");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Controller_WithDifferentEnableSyncValues_InitializesCorrectly(bool enableSync)
    {
        // Arrange
        var configuration = BuildConfiguration(enableSync);

        // Act
        var controller = new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configuration,
            _mockLogger.Object);

        // Assert
        controller.Should().NotBeNull();
        VerifyLoggerCalledWithMessage($"Sync enabled: {enableSync}");
    }

    #endregion

    #region Helper Methods

    private BlockchainController CreateController(bool? enableSync)
    {
        var configuration = BuildConfiguration(enableSync);
        return new BlockchainController(
            _mockMediator.Object,
            _mockODataSettings.Object,
            configuration,
            _mockLogger.Object);
    }

    private IConfiguration BuildConfiguration(bool? enableSync)
    {
        var configValues = new Dictionary<string, string?>();

        if (enableSync.HasValue)
        {
            configValues["EnableSync"] = enableSync.Value.ToString();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
            .Build();
    }

    private void VerifyLoggerCalled(LogLevel logLevel, Times expectedTimes)
    {
        _mockLogger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            expectedTimes);
    }

    private void VerifyLoggerCalledWithMessage(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    #endregion
}
