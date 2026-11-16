using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientsTestConstants;

namespace MedApp.UnitTests.Application.Patients.Commands.CreatePatient;

/// <summary>Adding a patient with an NHS number: validated, stored as its digits, and unique.</summary>
[TestFixture]
public sealed class CreatePatientNhsNumberTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private Patient? _added;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _added = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry _, CancellationToken _) => _added = p)
            .Returns(Task.CompletedTask);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
    }

    private static CreatePatientCommand Command(string? nhsNumber) =>
        new(ValidFirstName, ValidLastName, ValidDateOfBirth, ValidEmail, nhsNumber);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase(NhsNumber)]
    [TestCase(NhsNumberWithSpaces)]
    public void Validate_NoneOrAValidNumber_Passes(string? nhsNumber) =>
        new CreatePatientValidator().Validate(Command(nhsNumber)).IsValid.Should().BeTrue();

    [TestCase(NineDigits, NhsNumberRules.NotTenDigits)]
    [TestCase("99900000ab", NhsNumberRules.NotTenDigits)]
    [TestCase(WrongCheckDigit, NhsNumberRules.WrongCheckDigit)]
    [TestCase(CheckDigitTen, NhsNumberRules.WrongCheckDigit)]
    public void Validate_InvalidNumber_FailsOnceOnTheField(string nhsNumber, string message)
    {
        var result = new CreatePatientValidator().Validate(Command(nhsNumber));

        result.Errors.Should().ContainSingle().Which.Should().Match<FluentValidation.Results.ValidationFailure>(
            e => e.PropertyName == "NhsNumber" && e.ErrorMessage == message);
    }

    [Test]
    public async Task Handle_NumberWithSpaces_StoresItsDigits()
    {
        using var cts = new CancellationTokenSource();

        var response = await _fixture.Create<CreatePatientHandler>().Handle(Command(NhsNumberWithSpaces), cts.Token);

        _added!.NhsNumber.Should().Be(NhsNumber);
        response.NhsNumber.Should().Be(NhsNumber);
        _repository.Verify(r => r.NhsNumberInUseAsync(NhsNumber, null, cts.Token), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task Handle_NoNumber_StoresNullWithoutChecking(string? nhsNumber)
    {
        await _fixture.Create<CreatePatientHandler>().Handle(Command(nhsNumber), CancellationToken.None);

        _added!.NhsNumber.Should().BeNull();
        _repository.Verify(r => r.NhsNumberInUseAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AnotherPatientHasTheNumber_ThrowsConflictWithoutAdding()
    {
        _repository.Setup(r => r.NhsNumberInUseAsync(NhsNumber, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<CreatePatientHandler>().Handle(Command(NhsNumber), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NhsNumberRules.InUse);
        _added.Should().BeNull();
    }
}
