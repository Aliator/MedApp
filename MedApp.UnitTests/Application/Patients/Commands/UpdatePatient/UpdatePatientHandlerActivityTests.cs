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

namespace MedApp.UnitTests.Application.Patients.Commands.UpdatePatient;

/// <summary>What editing a patient records in the activity log, and when it's refused.</summary>
[TestFixture]
public sealed class UpdatePatientHandlerActivityTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private ActivityEntry? _entry;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _entry = null;
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient _, ActivityEntry e, CancellationToken _) => _entry = e)
            .Returns(Task.CompletedTask);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
    }

    private Patient Given(Patient patient)
    {
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        return patient;
    }

    [Test]
    public async Task Handle_ChangedFields_RecordsTheirNames_NeverTheValues()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        await _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(patient.Id, "Changedfirst", PatientsTestConstants.ValidLastName, new DateOnly(1990, 2, 3), "changed@example.com"),
            CancellationToken.None);

        _entry!.Action.Should().Be(ActivityAction.PatientUpdated);
        _entry.PatientId.Should().Be(patient.Id);
        _entry.Details.Should().Be("""{"fields":["firstName","dateOfBirth","email"]}""");
        _entry.Details.Should().NotContain("Changedfirst").And.NotContain("changed@example.com");
    }

    [Test]
    public async Task Handle_NothingChanged_RecordsNoFields()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        await _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(patient.Id, patient.FirstName, null, patient.DateOfBirth, null), CancellationToken.None);

        _entry!.Details.Should().Be("""{"fields":[]}""");
    }

    [Test]
    public async Task Handle_LastNameChanged_RecordsIt()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        await _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(patient.Id, null, "Otherlast", null, null), CancellationToken.None);

        _entry!.Details.Should().Be("""{"fields":["lastName"]}""");
    }

    [Test]
    public async Task Handle_RestrictedRecord_IsRefused()
    {
        var patient = Given(PrivacyTestConstants.CreateRestrictedPatient());

        var act = () => _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(patient.Id, "Changed", null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AnonymisedRecord_IsRefused()
    {
        var patient = Given(PrivacyTestConstants.CreateAnonymisedPatient());

        var act = () => _fixture.Create<UpdatePatientHandler>().Handle(
            new UpdatePatientCommand(patient.Id, "Changed", null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
        patient.FirstName.Should().Be(PatientsTestConstants.ValidFirstName);
    }
}
