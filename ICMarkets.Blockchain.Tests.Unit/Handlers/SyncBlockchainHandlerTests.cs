using FluentAssertions;
using ICMarkets.Blockchain.Application.Features;
using ICMarkets.Blockchain.Domain.Entities;
using ICMarkets.Blockchain.Domain.Interfaces;
using ICMarkets.Blockchain.Tests.Unit.TestBuilders;
using Moq;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Handlers;

/// <summary>
/// Unit tests for SyncBlockchainHandler (CQRS Command Handler).
/// Uses Test Builder pattern for improved test data creation.
/// </summary>
public sealed class SyncBlockchainHandlerTests : IDisposable
{
    private readonly Mock<IRepository> _mockRepository;
    private readonly Mock<IBlockCypherService> _mockService;
    private readonly SyncBlockchainHandler _sut;

    public SyncBlockchainHandlerTests()
    {
        _mockRepository = new Mock<IRepository>();
        _mockService = new Mock<IBlockCypherService>();
        _sut = new SyncBlockchainHandler(_mockService.Object, _mockRepository.Object);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Handle_WithValidData_ReturnsSuccessWithCorrectCount(int dataCount)
    {
        // Arrange
        var testData = BlockchainDataBuilder.CreateList(dataCount);
        SetupServiceToReturn(testData);

        // Act
        var result = await _sut.Handle(new SyncBlockchainCommand(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("the operation completed successfully");
        result.Count.Should().Be(dataCount, $"exactly {dataCount} items were fetched");

        VerifyServiceCalledOnce();
        VerifyRepositorySavedData(testData);
    }

    [Fact]
    public async Task Handle_WithEmptyData_ReturnsSuccessWithZeroCountAndDoesNotSave()
    {
        // Arrange
        SetupServiceToReturn(new List<BlockchainData>());

        // Act
        var result = await _sut.Handle(new SyncBlockchainCommand(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("empty data is a valid scenario");
        result.Count.Should().Be(0, "no data was returned");

        VerifyServiceCalledOnce();
        VerifyRepositoryNeverCalled();
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), "Network error")]
    [InlineData(typeof(TimeoutException), "Request timeout")]
    public async Task Handle_WhenServiceThrowsException_PropagatesExceptionAndDoesNotSave(
        Type exceptionType,
        string errorMessage)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, errorMessage)!;
        _mockService
            .Setup(s => s.FetchAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var act = async () => await _sut.Handle(new SyncBlockchainCommand(), CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<Exception>()
            .Where(e => e.GetType() == exceptionType)
            .WithMessage($"*{errorMessage}*");

        VerifyRepositoryNeverCalled();
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_PropagatesException()
    {
        // Arrange
        var testData = BlockchainDataBuilder.CreateList(1);
        SetupServiceToReturn(testData);

        _mockRepository
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<BlockchainData>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var act = async () => await _sut.Handle(new SyncBlockchainCommand(), CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Database connection failed*");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockService
            .Setup(s => s.FetchAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _sut.Handle(new SyncBlockchainCommand(), cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_WithMultipleBlockchainSymbols_SavesAllData()
    {
        // Arrange
        var testData = new List<BlockchainData>
        {
            BlockchainDataBuilder.Default().WithSymbol("BTC").WithHeight(800000).Build(),
            BlockchainDataBuilder.Default().WithSymbol("ETH").WithHeight(500000).Build(),
            BlockchainDataBuilder.Default().WithSymbol("DASH").WithHeight(200000).Build(),
            BlockchainDataBuilder.Default().WithSymbol("LTC").WithHeight(300000).Build()
        };
        SetupServiceToReturn(testData);

        // Act
        var result = await _sut.Handle(new SyncBlockchainCommand(), CancellationToken.None);

        // Assert
        result.Count.Should().Be(4, "all four blockchain symbols were fetched");
        _mockRepository.Verify(
            r => r.AddRangeAsync(
                It.Is<IEnumerable<BlockchainData>>(d => d.Count() == 4),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should save all four items");
    }

    #region Test Helpers

    private void SetupServiceToReturn(List<BlockchainData> data)
    {
        _mockService
            .Setup(s => s.FetchAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
    }

    private void VerifyServiceCalledOnce()
    {
        _mockService.Verify(
            s => s.FetchAllAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "service should be called exactly once");
    }

    private void VerifyRepositorySavedData(List<BlockchainData> expectedData)
    {
        _mockRepository.Verify(
            r => r.AddRangeAsync(expectedData, It.IsAny<CancellationToken>()),
            Times.Once,
            "repository should save the data exactly once");
    }

    private void VerifyRepositoryNeverCalled()
    {
        _mockRepository.Verify(
            r => r.AddRangeAsync(It.IsAny<IEnumerable<BlockchainData>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "repository should not be called when service fails or returns empty data");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #endregion
}
