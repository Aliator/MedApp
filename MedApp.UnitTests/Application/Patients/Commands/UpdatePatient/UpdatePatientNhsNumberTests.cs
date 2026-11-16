using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Patients.Commands.UpdatePatient;

/// <summary>Editing a patient's NHS number: left alone, changed, removed, or refused when another patient has it.</summary>
[TestFixture]
public sealed class UpdatePatientNhsNumberTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private Patient _patient = null!;
    private ActivityEntry? _entry;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _entry = null;
        _patient = PatientsTestConstants.CreateValidPatient();
        _patient.NhsNumber = NhsNumber;
        _repository.Setup(r => r.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient _, ActivityEntry e, CancellationToken _) => _entry = e)
            .Returns(Task.CompletedTask);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
    }

    private UpdatePatientCommand Command(string? nhsNumber) => new(_patient.Id, null, null, null, null, nhsNumber);

    private Task<MedApp.Contracts.Patients.Responses.PatientResponse?> Handle(string? nhsNumber, CancellationToken ct = default) =>
        _fixture.Create<UpdatePatientHandler>().Handle(Command(nhsNumber), ct);

    [Test]
    public void Validate_OnlyTheNhsNumber_IsEnoughToUpdate()
    {
        new UpdatePatientValidator().Validate(Command(OtherNhsNumber)).IsValid.Should().BeTrue();
        new UpdatePatientValidator().Validate(Command("")).IsValid.Should().BeTrue("an empty number removes it");
    }

    [Test]
    public void Validate_WrongCheckDigit_FailsOnTheField()
    {
        var result = new UpdatePatientValidator().Validate(Command(WrongCheckDigit));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "NhsNumber" && e.ErrorMessage == NhsNumberRules.WrongCheckDigit);
    }

    [Test]
    public async Task Handle_NumberNotGiven_LeavesIt()
    {
        var response = await _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(_patient.Id, "Renamed", null, null, null), CancellationToken.None);

        response!.NhsNumber.Should().Be(NhsNumber);
        _entry!.Details.Should().Be("""{"fields":["firstName"]}""");
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Handle_EmptyNumber_RemovesIt(string empty)
    {
        var response = await Handle(empty);

        response!.NhsNumber.Should().BeNull();
        _patient.NhsNumber.Should().BeNull();
        _entry!.Details.Should().Be("""{"fields":["nhsNumber"]}""");
    }

    [Test]
    public async Task Handle_NewNumber_StoresItsDigits_AndRecordsTheFieldNotTheNumber()
    {
        using var cts = new CancellationTokenSource();

        var response = await Handle("999 000 0026", cts.Token);

        response!.NhsNumber.Should().Be(OtherNhsNumber);
        _entry!.Details.Should().Be("""{"fields":["nhsNumber"]}""").And.NotContain(OtherNhsNumber);
        _repository.Verify(r => r.NhsNumberInUseAsync(OtherNhsNumber, _patient.Id, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_SameNumberWithSpaces_ChangesNothing()
    {
        await Handle(NhsNumberWithSpaces);

        _entry!.Details.Should().Be("""{"fields":[]}""");
        _repository.Verify(r => r.NhsNumberInUseAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AnotherPatientHasTheNumber_ThrowsConflictWithoutSaving()
    {
        _repository.Setup(r => r.NhsNumberInUseAsync(OtherNhsNumber, _patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handle(OtherNhsNumber);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(NhsNumberRules.InUse);
        _entry.Should().BeNull();
    }
}
