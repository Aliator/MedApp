using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Behaviors;
using Moq;

namespace MedApp.UnitTests.Application.Common.Behaviors;

[TestFixture]
public sealed class ValidationBehaviourTests
{
    private static readonly object Request = new();

    [Test]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behaviour = new ValidationBehaviour<object, string>(Array.Empty<IValidator<object>>());

        var nextCalled = false;

        var result = await behaviour.Handle(Request, Next, CancellationToken.None);

        result.Should().Be("ok");
        nextCalled.Should().BeTrue();
        return;

        Task<string> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    [Test]
    public async Task Handle_ValidatorsPass_CallsNext()
    {
        var validator = CreateValidatorReturning(new ValidationResult());
        var behaviour = new ValidationBehaviour<object, string>([validator.Object]);

        var nextCalled = false;

        var result = await behaviour.Handle(Request, Next, CancellationToken.None);

        result.Should().Be("ok");
        nextCalled.Should().BeTrue();

        validator.As<IValidator>().Verify(
            v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        return;

        Task<string> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    [Test]
    public async Task Handle_ValidationFails_ThrowsValidationException_AndDoesNotCallNext()
    {
        var validationResult = new ValidationResult([new ValidationFailure("Property", "error")]);
        var validator = CreateValidatorReturning(validationResult);
        var behaviour = new ValidationBehaviour<object, string>([validator.Object]);

        var nextCalled = false;

        var act = async () => await behaviour.Handle(Request, Next, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainSingle(f => f.PropertyName == "Property" && f.ErrorMessage == "error");

        nextCalled.Should().BeFalse();

        validator.As<IValidator>().Verify(
            v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        return;

        Task<string> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    private static Mock<IValidator<object>> CreateValidatorReturning(ValidationResult result)
    {
        var validator = new Mock<IValidator<object>>();

        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        validator.As<IValidator>()
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return validator;
    }
}
