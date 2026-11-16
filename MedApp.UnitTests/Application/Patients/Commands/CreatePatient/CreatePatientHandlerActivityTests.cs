using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.CreatePatient;

/// <summary>What creating a patient records in the activity log.</summary>
[TestFixture]
public sealed class CreatePatientHandlerActivityTests
{
    [Test]
    public async Task Handle_ValidCommand_SavesAPatientCreatedEntryWithThePatient()
    {
        var fixture = AutoFixtureFactory.Create();
        var repository = new Mock<IPatientRepository>();
        Patient? saved = null;
        ActivityEntry? entry = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry e, CancellationToken _) => (saved, entry) = (p, e))
            .Returns(Task.CompletedTask);
        fixture.Inject(repository.Object);
        fixture.Inject<IActivityLog>(new RecordingActivityLog());

        await fixture.Create<CreatePatientHandler>().Handle(
            new CreatePatientCommand(PatientsTestConstants.ValidFirstName, PatientsTestConstants.ValidLastName,
                PatientsTestConstants.ValidDateOfBirth, PatientsTestConstants.ValidEmail),
            CancellationToken.None);

        entry!.Action.Should().Be(ActivityAction.PatientCreated);
        entry.PatientId.Should().Be(saved!.Id);
        entry.UserId.Should().Be(ActivityTestConstants.UserId);
        entry.Details.Should().Be("{}");
    }
}
