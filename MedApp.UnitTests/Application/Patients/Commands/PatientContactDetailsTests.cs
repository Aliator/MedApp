using FluentAssertions;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Patients.Commands;

/// <summary>
/// A patient's contact details (address, postcode, mobile) and registered GP practice: adding, editing, validating,
/// reading, exporting and anonymising them. Synthetic values only (ZZ postcodes, Ofcom's drama range).
/// </summary>
[TestFixture]
public sealed class PatientContactDetailsTests
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPracticeRepository> _practices = null!;
    private RecordingActivityLog _activity = null!;
    private Practice _practice = null!;
    private Practice _archived = null!;
    private ActivityEntry? _entry;
    private Patient? _saved;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _practice = CreatePractice();
        _archived = CreatePractice(OtherPracticeName, OtherOdsCode, isArchived: true);
        _practices = RepositoryWith(_practice, _archived);
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

    private static CreatePatientCommand NewPatient(
        IReadOnlyList<string>? lines = null, string? postcode = null, string? mobile = null, Guid? practiceId = null) =>
        new(PatientsTestConstants.ValidFirstName, PatientsTestConstants.ValidLastName, PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail, null, lines, postcode, mobile, practiceId);

    private Patient Given(Patient patient)
    {
        _patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        return patient;
    }

    private static Patient WithContactDetails(Guid? practiceId = null)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AddressLines = AddressLine1;
        patient.Postcode = Postcode;
        patient.MobilePhone = Mobile;
        patient.PracticeId = practiceId;
        return patient;
    }

    private static UpdatePatientCommand Edit(
        Patient patient, IReadOnlyList<string>? lines = null, string? postcode = null, string? mobile = null, Guid? practiceId = null) =>
        new(patient.Id, null, null, null, null, null, lines, postcode, mobile, practiceId);

    [Test]
    public async Task Create_WithContactDetails_StoresThemNormalised_AndReturnsThePractice()
    {
        var response = await Create.Handle(
            NewPatient([$" {AddressLine1} ", "", AddressLine2], PostcodeAsTyped, MobileAsTyped, _practice.Id), _cts.Token);

        _saved!.AddressLines.Should().Be($"{AddressLine1}\n{AddressLine2}");
        _saved.Postcode.Should().Be(Postcode);
        _saved.MobilePhone.Should().Be(Mobile);
        _saved.PracticeId.Should().Be(_practice.Id);
        _saved.ReminderChannel.Should().Be(ReminderChannel.None);
        response.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        response.MobilePhone.Should().Be(Mobile);
        response.PracticeId.Should().Be(_practice.Id);
        response.Practice!.Name.Should().Be(PracticeName);
        _entry!.Action.Should().Be(ActivityAction.PatientCreated);
        _practices.Verify(r => r.GetAsync(_practice.Id, _cts.Token));
    }

    [Test]
    public async Task Create_WithoutContactDetails_HasNone()
    {
        var response = await Create.Handle(NewPatient(), _cts.Token);

        _saved!.AddressLines.Should().BeNull();
        _saved.Postcode.Should().BeNull();
        _saved.MobilePhone.Should().BeNull();
        _saved.PracticeId.Should().BeNull();
        response.AddressLines.Should().BeEmpty();
        response.Practice.Should().BeNull();
    }

    [Test]
    public async Task Create_ArchivedPractice_IsAConflict_UnknownIsNotFound()
    {
        var archived = () => Create.Handle(NewPatient(practiceId: _archived.Id), _cts.Token);
        await archived.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ArchivedMessage(_archived));

        var unknown = () => Create.Handle(NewPatient(practiceId: Guid.NewGuid()), _cts.Token);
        await unknown.Should().ThrowAsync<NotFoundException>();

        _patients.Verify(r => r.AddAsync(It.IsAny<Patient>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_ChangedContactDetails_AreSavedAndNamedInTheLog_NeverTheirValues()
    {
        var patient = Given(PatientsTestConstants.CreateValidPatient());

        var response = await Update.Handle(
            Edit(patient, [AddressLine1, AddressLine2], PostcodeAsTyped, MobileAsTyped, _practice.Id), _cts.Token);

        patient.AddressLines.Should().Be($"{AddressLine1}\n{AddressLine2}");
        patient.Postcode.Should().Be(Postcode);
        patient.MobilePhone.Should().Be(Mobile);
        patient.PracticeId.Should().Be(_practice.Id);
        response!.Practice!.Id.Should().Be(_practice.Id);
        _entry!.Details.Should().Be("""{"fields":["addressLines","postcode","mobilePhone","practice"]}""");
        foreach (var value in new[] { AddressLine1, "ZZ1", "7700", PracticeName })
            _entry.Details.Should().NotContain(value);
    }

    [Test]
    public async Task Update_NullLeavesThemAsTheyAre_AndTheResponseStillHasThePractice()
    {
        var patient = Given(WithContactDetails(_practice.Id));

        var response = await Update.Handle(
            new UpdatePatientCommand(patient.Id, "Changed", null, null, null), _cts.Token);

        patient.AddressLines.Should().Be(AddressLine1);
        patient.Postcode.Should().Be(Postcode);
        patient.MobilePhone.Should().Be(Mobile);
        patient.PracticeId.Should().Be(_practice.Id);
        response!.Practice!.Name.Should().Be(PracticeName);
        _entry!.Details.Should().Be("""{"fields":["firstName"]}""");
    }

    [Test]
    public async Task Update_EmptyRemovesThem()
    {
        var patient = Given(WithContactDetails(_practice.Id));

        var response = await Update.Handle(Edit(patient, [], " ", "", Guid.Empty), _cts.Token);

        patient.AddressLines.Should().BeNull();
        patient.Postcode.Should().BeNull();
        patient.MobilePhone.Should().BeNull();
        patient.PracticeId.Should().BeNull();
        response!.Practice.Should().BeNull();
        _entry!.Details.Should().Be("""{"fields":["addressLines","postcode","mobilePhone","practice"]}""");
    }

    [Test]
    public async Task Update_SameValuesAsTyped_ChangeNothing()
    {
        var patient = Given(WithContactDetails(_practice.Id));

        await Update.Handle(Edit(patient, [$" {AddressLine1}"], "zz1 1zz", MobileAsTyped, _practice.Id), _cts.Token);

        _entry!.Details.Should().Be("""{"fields":[]}""");
    }

    [Test]
    public async Task Update_KeepingAnArchivedPractice_IsFine_ButChoosingOneIsAConflict()
    {
        var keeps = Given(WithContactDetails(_archived.Id));
        var response = await Update.Handle(Edit(keeps, practiceId: _archived.Id), _cts.Token);
        response!.Practice!.IsArchived.Should().BeTrue();

        var chooses = Given(WithContactDetails(_practice.Id));
        var act = () => Update.Handle(Edit(chooses, practiceId: _archived.Id), _cts.Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ArchivedMessage(_archived));
        chooses.PracticeId.Should().Be(_practice.Id);
    }

    [Test]
    public async Task Update_Restricted_IsRefusedBeforeAnythingChanges()
    {
        var patient = Given(WithContactDetails());
        patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;

        var act = () => Update.Handle(Edit(patient, postcode: "ZZ9 9ZZ"), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        patient.Postcode.Should().Be(Postcode);
    }

    [Test]
    public async Task GetById_GivesThePracticeTheyAreRegisteredWith()
    {
        var patient = Given(WithContactDetails(_practice.Id));

        var response = await new GetPatientByIdHandler(_patients.Object, _practices.Object, _activity)
            .Handle(new GetPatientByIdQuery(patient.Id), _cts.Token);

        response!.Practice!.OdsCode.Should().Be(OdsCode);
        response.AddressLines.Should().Equal(AddressLine1);
        _practices.Verify(r => r.GetAsync(_practice.Id, _cts.Token));
    }

    [Test]
    public void CreateValidator_ChecksTheContactDetails()
    {
        var validator = new CreatePatientValidator();

        validator.Validate(NewPatient([AddressLine1, AddressLine2], PostcodeAsTyped, MobileAsTyped, _practice.Id))
            .IsValid.Should().BeTrue();
        validator.Validate(NewPatient(["a", "b", "c", "d"], "ZZ", "01632 960000", Guid.Empty))
            .Errors.Select(e => (e.PropertyName, e.ErrorMessage)).Should().BeEquivalentTo(new[]
            {
                ("AddressLines", ContactRules.TooManyLines),
                ("Postcode", ContactRules.NotAPostcode),
                ("MobilePhone", ContactRules.NotAMobile),
                ("PracticeId", "Practice id must not be empty: leave it out for none.")
            });
    }

    [Test]
    public void UpdateValidator_ChecksTheContactDetails_AndAnyOneCountsAsAField()
    {
        var validator = new UpdatePatientValidator();
        var id = Guid.NewGuid();

        validator.Validate(new UpdatePatientCommand(id, null, null, null, null, PracticeId: Guid.Empty)).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePatientCommand(id, null, null, null, null, AddressLines: [])).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePatientCommand(id, null, null, null, null, Postcode: "")).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePatientCommand(id, null, null, null, null, MobilePhone: "")).IsValid.Should().BeTrue();
        validator.Validate(new UpdatePatientCommand(id, null, null, null, null, AddressLines: [new string('a', 101)], Postcode: "nope", MobilePhone: "nope"))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["AddressLines", "Postcode", "MobilePhone"]);
    }

    [Test]
    public void Mapper_AList_GivesThePracticeByIdOnly_AndAnotherPracticeIsLeftOut()
    {
        var patient = WithContactDetails(_practice.Id);

        PatientMapper.ToResponse(patient).Practice.Should().BeNull();
        PatientMapper.ToResponse(patient).PracticeId.Should().Be(_practice.Id);
        PatientMapper.ToResponse(patient, _archived).Practice.Should().BeNull();
        PatientMapper.ToResponse(patient, _practice).Practice!.Name.Should().Be(PracticeName);
    }

    [Test]
    public void Export_GivesTheContactDetailsAndThePracticeByName()
    {
        var patient = WithContactDetails(_practice.Id);

        var exported = PatientExportMapper.ToExported(patient, _practice);

        exported.AddressLines.Should().Equal(AddressLine1);
        exported.Postcode.Should().Be(Postcode);
        exported.MobilePhone.Should().Be(Mobile);
        exported.PracticeName.Should().Be(PracticeName);
        exported.PracticeOdsCode.Should().Be(OdsCode);
        PatientExportMapper.ToExported(patient).PracticeName.Should().BeNull();
    }

    [Test]
    public void Anonymise_RemovesTheContactDetailsAndTheLinkToThePractice_ButNotThePractice()
    {
        var patient = WithContactDetails(_practice.Id);
        patient.ReminderChannel = ReminderChannel.TextAndEmail;
        var referral = ReferralsTestConstants.CreateReferral(patient);
        referral.PracticeId = _practice.Id;
        var record = PrivacyTestConstants.CreateRecord(patient) with { Practice = _practice, Referrals = [referral] };

        PatientAnonymiser.Apply(record, PracticesTestConstants.NowUtc);

        patient.AddressLines.Should().BeNull();
        patient.Postcode.Should().BeNull();
        patient.MobilePhone.Should().BeNull();
        patient.PracticeId.Should().BeNull();
        patient.ReminderChannel.Should().Be(ReminderChannel.None);
        referral.PracticeId.Should().BeNull();
        _practice.Name.Should().Be(PracticeName);
    }
}
