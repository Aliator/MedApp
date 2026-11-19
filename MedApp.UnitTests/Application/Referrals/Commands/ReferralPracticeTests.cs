using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Referrals.Commands.EditReferral;
using MedApp.Application.Referrals.Commands.RegisterReferral;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Commands;

/// <summary>Choosing the referring practice from the directory, beside the practice typed from the letter.</summary>
[TestFixture]
public sealed class ReferralPracticeTests : ReferralCommandTestBase
{
    private RegisterReferralHandler Register => new(Repository.Object, Patients.Object, Teams.Object, Practices.Object, Clock);
    private EditReferralHandler Edit => new(Repository.Object, Teams.Object, Practices.Object, Boundary, Clock);

    private RegisterReferralCommand Registering(Guid? practiceId, string practice = "", string? ods = null) => new(
        Patient.Id, Team.Id, ReceivedOn, practice, null, ods, "High", Reason, false, Registrar, practiceId);

    private EditReferralCommand Editing(Referral referral, Guid? practiceId) => new(
        referral.Id, Team.Id, referral.ReceivedOn, referral.ReferrerPractice, referral.ReferrerClinician,
        referral.ReferrerOdsCode, "High", referral.Reason, referral.Version, Registrar, practiceId);

    private MedApp.Domain.Practices.Practice Archived()
    {
        var archived = PracticesTestConstants.CreatePractice(PracticesTestConstants.OtherPracticeName, isArchived: true);
        Practices.Setup(r => r.GetAsync(archived.Id, It.IsAny<CancellationToken>())).ReturnsAsync(archived);
        return archived;
    }

    [Test]
    public async Task Register_WithAPracticeAndNothingTyped_TakesItsNameAndCode()
    {
        var response = await Register.Handle(Registering(DirectoryPractice.Id), Cts.Token);

        var referral = Added.Single();
        referral.PracticeId.Should().Be(DirectoryPractice.Id);
        referral.ReferrerPractice.Should().Be(PracticesTestConstants.PracticeName);
        referral.ReferrerOdsCode.Should().Be(PracticesTestConstants.OdsCode);
        response.PracticeId.Should().Be(DirectoryPractice.Id);
        Practices.Verify(r => r.GetAsync(DirectoryPractice.Id, Cts.Token));
    }

    [Test]
    public async Task Register_WithAPracticeAndWhatTheLetterSays_KeepsTheLettersWords()
    {
        await Register.Handle(Registering(DirectoryPractice.Id, Practice, OdsCode), Cts.Token);

        var referral = Added.Single();
        referral.ReferrerPractice.Should().Be(Practice);
        referral.ReferrerOdsCode.Should().Be(OdsCode);
        referral.PracticeId.Should().Be(DirectoryPractice.Id);
    }

    [Test]
    public async Task Register_WithoutAPractice_HasNone()
    {
        await Register.Handle(Registering(null, Practice), Cts.Token);

        Added.Single().PracticeId.Should().BeNull();
        Practices.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Register_ArchivedPractice_IsAConflict()
    {
        var archived = Archived();

        var act = () => Register.Handle(Registering(archived.Id), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ArchivedMessage(archived));
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Edit_ChoosingAPractice_RecordsTheFieldWithIds()
    {
        var referral = GivenReferral();

        await Edit.Handle(Editing(referral, DirectoryPractice.Id), Cts.Token);

        referral.PracticeId.Should().Be(DirectoryPractice.Id);
        var entry = Saved.Single();
        entry.Action.Should().Be(ReferralEventAction.DetailsEdited);
        entry.Details.Should().Contain($"\"{ReferralChange.DirectoryPracticeField}\"");
        entry.Details.Should().Contain(DirectoryPractice.Id.ToString());
        entry.Details.Should().NotContain(PracticesTestConstants.PracticeName);
    }

    [Test]
    public async Task Edit_KeepingAnArchivedPractice_IsFine_AndRemovingItIsRecorded()
    {
        var archived = Archived();
        var referral = GivenReferral();
        referral.PracticeId = archived.Id;

        await Edit.Handle(Editing(referral, archived.Id), Cts.Token);
        Saved.Should().BeEmpty();

        await Edit.Handle(Editing(referral, null), Cts.Token);
        referral.PracticeId.Should().BeNull();
        Saved.Single().Details.Should().Contain(ReferralChange.DirectoryPracticeField);
    }

    [Test]
    public async Task Edit_ChoosingAnArchivedPractice_IsAConflict()
    {
        var archived = Archived();
        var referral = GivenReferral();

        var act = () => Edit.Handle(Editing(referral, archived.Id), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        referral.PracticeId.Should().BeNull();
    }

    [Test]
    public void Validators_AllowAnEmptyPracticeWithOneFromTheDirectory_ButNotAnEmptyId()
    {
        var clock = new FixedTimeProvider(NowUtc);
        var register = new RegisterReferralValidator(clock);
        var edit = new EditReferralValidator(clock);

        register.Validate(Registering(Guid.NewGuid())).IsValid.Should().BeTrue();
        register.Validate(Registering(null)).Errors.Should().ContainSingle(e => e.PropertyName == "ReferrerPractice");
        register.Validate(Registering(Guid.Empty)).Errors.Should().ContainSingle(e => e.PropertyName == "PracticeId");
        register.Validate(Registering(Guid.NewGuid(), new string('a', Referral.MaxPracticeLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "ReferrerPractice");

        var referral = CreateReferral();
        referral.ReferrerPractice = "";
        edit.Validate(Editing(referral, Guid.NewGuid())).IsValid.Should().BeTrue();
        edit.Validate(Editing(referral, null)).Errors.Should().ContainSingle(e => e.PropertyName == "ReferrerPractice");
    }

    [Test]
    public void ReferralPractices_FillOnlyWhatsMissing()
    {
        var practice = PracticesTestConstants.CreatePractice(odsCode: null);

        ReferralPractices.TypedOrDirectoryName(" ", practice).Should().Be(PracticesTestConstants.PracticeName);
        ReferralPractices.TypedOrDirectoryName(Practice, practice).Should().Be(Practice);
        ReferralPractices.TypedOrDirectoryName(null, null).Should().BeEmpty();
        ReferralPractices.TypedOrDirectoryOdsCode(null, practice).Should().BeNull();
        ReferralPractices.TypedOrDirectoryOdsCode(OdsCode, practice).Should().Be(OdsCode);
        ReferralPractices.TypedOrDirectoryOdsCode("", null).Should().Be("");
    }
}
