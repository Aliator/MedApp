using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands;

/// <summary>The patient's choice of appointment messages (centre plan decision 133), on the patient form.</summary>
[TestFixture]
public sealed class PatientReminderChannelTests
{
    private Mock<IPatientRepository> _patients = null!;
    private RecordingActivityLog _activity = null!;
    private Patient? _saved;
    private ActivityEntry? _entry;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _patients.Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry e, CancellationToken _) => (_saved, _entry) = (p, e));
        _patients.Setup(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry e, CancellationToken _) => (_saved, _entry) = (p, e));
        _activity = new RecordingActivityLog();
        _saved = null;
        _entry = null;
    }

    private CreatePatientCommand NewPatient(string? channel) =>
        new(PatientsTestConstants.ValidFirstName, PatientsTestConstants.ValidLastName, PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail, ReminderChannel: channel);

    [TestCase(null, ReminderChannel.None)]
    [TestCase("Text", ReminderChannel.Text)]
    [TestCase("textAndEmail", ReminderChannel.TextAndEmail)]
    public async Task Create_SavesTheChoice(string? channel, ReminderChannel expected)
    {
        var response = await new CreatePatientHandler(_patients.Object, Mock.Of<IPracticeRepository>(), _activity)
            .Handle(NewPatient(channel), CancellationToken.None);

        _saved!.ReminderChannel.Should().Be(expected);
        response.ReminderChannel.Should().Be(expected.ToString());
    }

    [Test]
    public async Task Update_ChangedChoice_IsSavedAndLoggedByFieldName()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

        var response = await new UpdatePatientHandler(_patients.Object, Mock.Of<IPracticeRepository>(), _activity)
            .Handle(new UpdatePatientCommand(patient.Id, null, null, null, null, ReminderChannel: "Email"), CancellationToken.None);

        patient.ReminderChannel.Should().Be(ReminderChannel.Email);
        response!.ReminderChannel.Should().Be("Email");
        _entry!.Details.Should().Contain("reminderChannel").And.NotContain("Email\"");
    }

    [Test]
    public async Task Update_NoChoiceGiven_LeavesItAsItIs()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.ReminderChannel = ReminderChannel.Text;
        _patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

        await new UpdatePatientHandler(_patients.Object, Mock.Of<IPracticeRepository>(), _activity)
            .Handle(new UpdatePatientCommand(patient.Id, "Changed", null, null, null), CancellationToken.None);

        patient.ReminderChannel.Should().Be(ReminderChannel.Text);
        _entry!.Details.Should().NotContain("reminderChannel");
    }

    [TestCase("Carrier pigeon")]
    [TestCase("1")]
    [TestCase("")]
    public void Validators_UnknownChoice_Fail(string channel)
    {
        new CreatePatientValidator().TestValidate(NewPatient(channel))
            .ShouldHaveValidationErrorFor(x => x.ReminderChannel).WithErrorMessage(ReminderChannelRules.NotAChannel);
        new UpdatePatientValidator().TestValidate(new UpdatePatientCommand(Guid.NewGuid(), null, null, null, null, ReminderChannel: channel))
            .ShouldHaveValidationErrorFor(x => x.ReminderChannel);
    }

    [Test]
    public void UpdateValidator_OnlyTheChoice_IsEnough() =>
        new UpdatePatientValidator().TestValidate(new UpdatePatientCommand(Guid.NewGuid(), null, null, null, null, ReminderChannel: "None"))
            .ShouldNotHaveAnyValidationErrors();

    [Test]
    public void Export_And_Anonymise_CoverTheChoice()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.ReminderChannel = ReminderChannel.TextAndEmail;

        PatientExportMapper.ToExported(patient).ReminderChannel.Should().Be("TextAndEmail");
    }
}
