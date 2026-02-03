using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Behaviors;
using MediatR;
using Moq;

namespace MedApp.UnitTests.Application.Common.Behaviors;

[TestFixture]
public sealed class ValidationBehaviourTests
{
    private static readonly object Request = new();

    [Test]
    public async Task Handle_NoValidators_CallsNext()
    {
        var behaviour = new ValidationBehaviour<object, string>(
            []);

        Task<string> Next(CancellationToken ct) => Task.FromResult("ok");

        var result = await behaviour.Handle(
            Request,
            Next,
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Test]
    public async Task Handle_ValidatorsPass_CallsNext()
    {
        var validator = new Mock<IValidator<object>>();
        validator
            .Setup(v => v.Validate(It.IsAny<ValidationContext<object>>()))
            .Returns(new ValidationResult());

        var behaviour = new ValidationBehaviour<object, string>(
            [validator.Object]);

        Task<string> Next(CancellationToken ct) => Task.FromResult("ok");

        var result = await behaviour.Handle(
            Request,
            Next,
            CancellationToken.None);

        result.Should().Be("ok");
    }

    [Test]
    public async Task Handle_ValidationFails_ThrowsValidationException()
    {
        var failure = new ValidationFailure("Property", "error");

        var validator = new Mock<IValidator<object>>();
        validator
            .Setup(v => v.Validate(It.IsAny<ValidationContext<object>>()))
            .Returns(new ValidationResult(new[] { failure }));

        var behaviour = new ValidationBehaviour<object, string>(
            [validator.Object]);

        Task<string> Next(CancellationToken ct) => Task.FromResult("ok");

        var act = async () =>
            await behaviour.Handle(
                Request,
                Next,
                CancellationToken.None);

        var exception = await act.Should()
            .ThrowAsync<ValidationException>();

        exception.Which.Errors.Should().ContainSingle();
    }
}
