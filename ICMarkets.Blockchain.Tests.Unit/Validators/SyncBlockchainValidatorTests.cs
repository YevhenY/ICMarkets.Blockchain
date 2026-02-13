using FluentAssertions;
using FluentValidation;
using ICMarkets.Blockchain.Application.Features;
using Xunit;

namespace ICMarkets.Blockchain.Tests.Unit.Validators;

/// <summary>
/// Unit tests for SyncBlockchainValidator.
/// This validator has no rules but is tested for completeness and future-proofing.
/// </summary>
public sealed class SyncBlockchainValidatorTests
{
    private readonly SyncBlockchainValidator _sut;

    public SyncBlockchainValidatorTests()
    {
        _sut = new SyncBlockchainValidator();
    }

    [Fact]
    public void Validate_WithDefaultCommand_AlwaysReturnsValid()
    {
        // Arrange
        var command = new SyncBlockchainCommand();

        // Act
        var result = _sut.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue("this validator has no validation rules");
        result.Errors.Should().BeEmpty("no validation rules means no errors");
    }

    [Fact]
    public void Validator_HasNoValidationRules()
    {
        // Arrange & Act
        var descriptor = _sut.CreateDescriptor();
        var memberValidators = descriptor.GetValidatorsForMember(string.Empty);

        // Assert
        memberValidators.Should().BeEmpty("this validator intentionally has no rules");
    }

    [Fact]
    public void Validator_IsOfCorrectType()
    {
        // Assert
        _sut.Should().BeAssignableTo<AbstractValidator<SyncBlockchainCommand>>(
            "validator must implement the correct FluentValidation base type");
    }
}
