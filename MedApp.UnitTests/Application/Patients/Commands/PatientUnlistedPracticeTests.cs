using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Letters;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Patients.Commands;

/// <summary>
/// A patient's GP practice that isn't in the directory ("Not listed here"): its name, address and phone held on the
/// patient only; a patient has it or a directory practice, never both; the export, the response and anonymisation
/// cover it, and the activity log names it, never its values. Synthetic values only.
/// </summary>
[TestFixture]
public sealed class PatientUnlistedPracticeTests
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPracticeRepository> _practices = null!;
    private RecordingActivityLog _activity = null!;
    private Practice _practice = null!;
    private ActivityEntry? _entry;
    private Patient? _saved;
    private CancellationTokenSource _cts = null!;

    private static readonly UnlistedPracticeInput Unlisted =
        new($" {UnlistedName} ", [UnlistedLine, ""], " zz99zz ", $" {UnlistedPhone} ");

    [SetUp]
    public void SetUp()
    {
        _practice = CreatePractice();
        _practices = RepositoryWith(_practice);
        _patients = new Mock<IPatientRepository>();
        _patients.Setup(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry e, CancellationToken _) => (_saved, _entry) = (p, e));
        _patients.Setup(r => r.UpdateAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((Patient p, ActivityEntry e, CancellationToken _) => (_saved, _entry) = (p, e));
        _activity = new RecordingActivityLog();
        _entry = null;
        _saved = null;
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private CreatePatientHandler Create => new(_patients.Object, _practices.Object, _activity);
    private UpdatePatientHandler Update => new(_patients.Object, _practices.Object, _activity);

    private static CreatePatientCommand NewPatient(Guid? practiceId = null, UnlistedPracticeInput? unlisted = null) =>
        new(PatientsTestConstants.ValidFirstName, PatientsTestConstants.ValidLastName, PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail, PracticeId: practiceId, UnlistedPractice: unlisted);

    private static UpdatePatientCommand Edit(Patient patient, Guid? practiceId = null, UnlistedPracticeInput? unlisted = null) =>
        new(patient.Id, null, null, null, null, PracticeId: practiceId, UnlistedPractice: unlisted);

    private Patient Given(Patient patient)
    {
        _patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        return patient;
    }

    private static Patient WithUnlisted()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.UnlistedPracticeName = UnlistedName;
        patient.UnlistedPracticeAddressLines = UnlistedLine;
        patient.UnlistedPracticePostcode = UnlistedPostcode;
        patient.UnlistedPracticePhone = UnlistedPhone;
        return patient;
    }

    [Test]
    public async Task Create_WithAnUnlistedPractice_StoresItTidied_OnThePatientOnly()
    {
        var response = await Create.Handle(NewPatient(unlisted: Unlisted), _cts.Token);

        _saved!.PracticeId.Should().BeNull();
        _saved.UnlistedPracticeName.Should().Be(UnlistedName);
        _saved.UnlistedPracticeAddressLines.Should().Be(UnlistedLine);
        _saved.UnlistedPracticePostcode.Should().Be(UnlistedPostcode);
        _saved.UnlistedPracticePhone.Should().Be(UnlistedPhone);
        response.Practice.Should().BeNull();
        response.UnlistedPractice!.Name.Should().Be(UnlistedName);
        response.UnlistedPractice.AddressLines.Should().Equal(UnlistedLine);
        response.UnlistedPractice.Postcode.Should().Be(UnlistedPostcode);
        response.UnlistedPractice.Phone.Should().Be(UnlistedPhone);
        _practices.Verify(r => r.AddAsync(It.IsAny<Practice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_AnUnlistedPractice_ReplacesTheDirectoryOne_NamedInTheLog_NeverItsValues()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());
        patient.PracticeId = _practice.Id;

        var response = await Update.Handle(Edit(patient, unlisted: Unlisted), _cts.Token);

        patient.PracticeId.Should().BeNull();
        patient.UnlistedPracticeName.Should().Be(UnlistedName);
        response!.Practice.Should().BeNull();
        response.UnlistedPractice!.Name.Should().Be(UnlistedName);
        _entry!.Details.Should().Be("""{"fields":["practice","unlistedPractice"]}""");
        foreach (var value in new[] { UnlistedName, UnlistedLine, "ZZ9", "960111" })
            _entry.Details.Should().NotContain(value);
    }

    [Test]
    public async Task Update_ADirectoryPractice_ReplacesTheUnlistedOne()
    {
        var patient = Given(WithUnlisted());

        var response = await Update.Handle(Edit(patient, practiceId: _practice.Id), _cts.Token);

        patient.PracticeId.Should().Be(_practice.Id);
        patient.UnlistedPracticeName.Should().BeNull();
        patient.UnlistedPracticeAddressLines.Should().BeNull();
        patient.UnlistedPracticePostcode.Should().BeNull();
        patient.UnlistedPracticePhone.Should().BeNull();
        response!.UnlistedPractice.Should().BeNull();
        response.Practice!.Id.Should().Be(_practice.Id);
        _entry!.Details.Should().Be("""{"fields":["practice","unlistedPractice"]}""");
    }

    [Test]
    public async Task Update_TheEmptyPracticeId_RemovesTheUnlistedOne()
    {
        var patient = Given(WithUnlisted());

        await Update.Handle(Edit(patient, practiceId: Guid.Empty), _cts.Token);

        patient.PracticeId.Should().BeNull();
        patient.UnlistedPracticeName.Should().BeNull();
        _entry!.Details.Should().Be("""{"fields":["unlistedPractice"]}""");
    }

    [Test]
    public async Task Update_TheSameUnlistedPracticeAsTyped_ChangesNothing_NullLeavesIt()
    {
        var patient = Given(WithUnlisted());

        await Update.Handle(Edit(patient, unlisted: Unlisted), _cts.Token);
        _entry!.Details.Should().Be("""{"fields":[]}""");

        await Update.Handle(new UpdatePatientCommand(patient.Id, "Changed", null, null, null), _cts.Token);
        patient.UnlistedPracticeName.Should().Be(UnlistedName);
        _entry!.Details.Should().Be("""{"fields":["firstName"]}""");
    }

    [Test]
    public async Task Update_TheEmptyIdWithAnUnlistedPractice_SetsIt_Once()
    {
        var patient = Given(WithUnlisted());

        await Update.Handle(Edit(patient, Guid.Empty, Unlisted with { Name = "Elsewhere Surgery" }), _cts.Token);

        patient.UnlistedPracticeName.Should().Be("Elsewhere Surgery");
        _entry!.Details.Should().Be("""{"fields":["unlistedPractice"]}""");
    }

    [Test]
    public void Validators_NeverBoth_AndTheUnlistedPracticeIsChecked()
    {
        var create = new CreatePatientValidator();
        create.Validate(NewPatient(unlisted: Unlisted)).IsValid.Should().BeTrue();
        create.Validate(NewPatient(_practice.Id, Unlisted)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == UnlistedPractices.BothMessage);
        create.Validate(NewPatient(unlisted: new UnlistedPracticeInput(" ", ["a", "b", "c", "d"], "ZZ", "123")))
            .Errors.Select(e => e.PropertyName.Split('.').Last()).Should().BeEquivalentTo([
                "UnlistedPracticeName", "UnlistedPracticeAddressLines", "UnlistedPracticePostcode", "UnlistedPracticePhone"
            ]);
        create.Validate(NewPatient(unlisted: Unlisted with { Name = new string('a', Practice.MaxNameLength + 1) }))
            .IsValid.Should().BeFalse();

        var update = new UpdatePatientValidator();
        var patient = PatientsTestConstants.CreateValidPatient();
        update.Validate(Edit(patient, unlisted: Unlisted)).IsValid.Should().BeTrue();
        update.Validate(Edit(patient, Guid.Empty, Unlisted)).IsValid.Should().BeTrue();
        update.Validate(Edit(patient, _practice.Id, Unlisted)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == UnlistedPractices.BothMessage);
    }

    [Test]
    public void ExportAndAnonymise_CoverIt_AndAnonymisingRemovesItsCopiesOnLetters()
    {
        var patient = WithUnlisted();

        var exported = PatientExportMapper.ToExported(patient).UnlistedPractice!;
        exported.Name.Should().Be(UnlistedName);
        exported.AddressLines.Should().Equal(UnlistedLine);
        exported.Postcode.Should().Be(UnlistedPostcode);
        exported.Phone.Should().Be(UnlistedPhone);

        var letter = LettersTestConstants.CreateLetter(PathwaysTestConstants.CreatePathway(), _practice);
        letter.Recipients.Clear();
        letter.Recipients.Add(new LetterRecipient
        {
            Id = Guid.NewGuid(), LetterId = letter.Id, Kind = LetterRecipientKind.GpPractice, Name = UnlistedName,
            AddressLines = UnlistedLine, Postcode = UnlistedPostcode
        });
        letter.Recipients.Add(new LetterRecipient
        {
            Id = Guid.NewGuid(), LetterId = letter.Id, Kind = LetterRecipientKind.GpPractice, PracticeId = _practice.Id,
            Name = PracticeName, AddressLines = AddressLine1, Postcode = Postcode
        });
        var record = PrivacyTestConstants.CreateRecord(patient) with { Letters = [letter] };

        PatientAnonymiser.Apply(record, NowUtc);

        UnlistedPractices.Has(patient).Should().BeFalse();
        patient.UnlistedPracticeAddressLines.Should().BeNull();
        patient.UnlistedPracticePostcode.Should().BeNull();
        patient.UnlistedPracticePhone.Should().BeNull();
        letter.Recipients[0].AddressLines.Should().BeNull();
        letter.Recipients[0].Name.Should().NotBe(UnlistedName);
        letter.Recipients[1].Name.Should().Be(PracticeName);
    }
}
