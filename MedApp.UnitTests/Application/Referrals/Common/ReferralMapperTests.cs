using FluentAssertions;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Queries.SearchReferrals;
using MedApp.Domain.Referrals;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Common;

[TestFixture]
public sealed class ReferralMapperTests
{
    [Test]
    public void ToResponse_GivesTheBannerDetailsHistoryAndWhatTheRegistrarMayDo()
    {
        var referral = CreateReferral(version: 2);
        var change = new ReferralChange(referral, Registrar, NowUtc);
        change.Register(referral.Specialty);

        var response = ReferralMapper.ToResponse(referral, change.Events, letterCount: 1, Registrar);

        response.Patient!.Id.Should().Be(referral.PatientId);
        response.PatientFlags.Should().NotBeNull();
        response.SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
        response.Reason.Should().Be(Reason);
        response.ReferrerPractice.Should().Be(Practice);
        response.RequestedPriority.Should().Be("High");
        response.Status.Should().Be("AwaitingTriage");
        response.IsOpen.Should().BeTrue();
        response.LetterCount.Should().Be(1);
        response.CanEdit.Should().BeTrue();
        response.CanClose.Should().BeTrue();
        response.CanAttachFiles.Should().BeTrue();
        response.ReadOnlyReason.Should().BeNull();
        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().Be(ReferralWorkflow.NoTriagePermissionMessage);
        response.History.Single().Action.Should().Be("Registered");
        response.History.Single().SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
        response.History.Single().RequestedPriority.Should().Be("High");
    }

    [Test]
    public void ToResponse_AViewerWithoutRegisterReferrals_CanDoNothing()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Viewer);

        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeFalse();
        response.CanAttachFiles.Should().BeFalse();
    }

    [Test]
    public void ToResponse_RestrictedRecord_CanBeWithdrawnButNotEditedOrGivenFiles()
    {
        var referral = CreateReferral();
        referral.Patient.RestrictedAtUtc = NowUtc;

        var response = ReferralMapper.ToResponse(referral, [], 0, Registrar);

        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeTrue();
        response.CanAttachFiles.Should().BeFalse();
        response.ReadOnlyReason.Should().NotBeNull();
        response.PatientFlags!.IsRestricted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_Closed_SaysWhyAndOffersNoTriage()
    {
        var referral = CreateReferral(status: ReferralStatus.Withdrawn);
        referral.DecisionDetail = Detail;
        referral.DecidedByUsername = Registrar.Username;

        var response = ReferralMapper.ToResponse(referral, [], 0, Registrar);

        response.IsOpen.Should().BeFalse();
        response.CanEdit.Should().BeFalse();
        response.CanClose.Should().BeFalse();
        response.ReadOnlyReason.Should().Contain("withdrawn");
        response.WhyCantTriage.Should().BeNull();
        response.DecisionDetail.Should().Be(Detail);
        response.DecidedByUsername.Should().Be(Registrar.Username);
    }

    [Test]
    public void ToResponse_ATriagerInItsSpecialty_CanTriage()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Triager, viewerIsMember: true);

        response.CanTriage.Should().BeTrue();
        response.WhyCantTriage.Should().BeNull();
    }

    [Test]
    public void ToResponse_ATriagerOutsideItsSpecialty_IsToldWhoCan()
    {
        var response = ReferralMapper.ToResponse(CreateReferral(), [], 0, Triager, viewerIsMember: false);

        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().Be(ReferralWorkflow.NotInSpecialtyMessage(SpecialtiesTestConstants.SpecialtyName));
    }

    [Test]
    public void ToResponse_ATriagerOnARestrictedRecord_CantTriage()
    {
        var referral = CreateReferral();
        referral.Patient.RestrictedAtUtc = NowUtc;

        var response = ReferralMapper.ToResponse(referral, [], 0, Triager, viewerIsMember: true);

        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().NotBeNull();
    }

    [Test]
    public void ToResponse_Triaged_GivesTheDecisionAndHistoryWithoutFreeTextInTheHistory()
    {
        var referral = CreateReferral();
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.Reject(ReferralRejectionReason.Duplicate, Canary);

        var response = ReferralMapper.ToResponse(referral, change.Events, 0, Triager, viewerIsMember: true);

        response.Status.Should().Be("Rejected");
        response.RejectionReason.Should().Be("Duplicate");
        response.DecisionDetail.Should().Be(Canary);
        response.DecidedByUsername.Should().Be(Triager.Username);
        response.CanTriage.Should().BeFalse();
        response.WhyCantTriage.Should().BeNull();
        var entry = response.History.Single();
        entry.Action.Should().Be("Rejected");
        entry.RejectionReason.Should().Be("Duplicate");
        entry.ToStatus.Should().Be("Rejected");
        change.Events.Single().Details.Should().NotContain(Canary);
    }

    [Test]
    public void ToResponse_RedirectedHere_GivesTheRedirectDetail()
    {
        var referral = CreateReferral();
        referral.RedirectDetail = Detail;

        ReferralMapper.ToResponse(referral, [], 0, Viewer).RedirectDetail.Should().Be(Detail);
    }

    [Test]
    public void ToPathwayReferral_GivesItsIdDateAndSpecialty()
    {
        var referral = CreateReferral();

        var response = ReferralMapper.ToPathwayReferral(referral);

        response.Id.Should().Be(referral.Id);
        response.ReceivedOn.Should().Be(ReceivedOn);
        response.SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
    }

    [Test]
    public void EventDetails_Accepted_GivesThePriorityTemplateAndPathway()
    {
        var referral = CreateReferral();
        var pathwayId = Guid.NewGuid();
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.Accept(pathwayId, "Synthetic template", MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority.Urgent);

        var entry = ReferralMapper.ToResponse(change.Events.Single());

        entry.Action.Should().Be("Accepted");
        entry.Priority.Should().Be("Urgent");
        entry.TemplateName.Should().Be("Synthetic template");
        entry.PathwayId.Should().Be(pathwayId);
        entry.SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
    }

    [Test]
    public void EventDetails_SpecialtyChanged_GivesBothSpecialties()
    {
        var referral = CreateReferral();
        var urology = SpecialtiesTestConstants.CreateSpecialty(SpecialtiesTestConstants.OtherSpecialtyName);
        var change = new ReferralChange(referral, Triager, NowUtc);
        change.RedirectToSpecialty(urology, Canary);

        var entry = ReferralMapper.ToResponse(change.Events.Single());

        entry.Action.Should().Be("SpecialtyChanged");
        entry.SpecialtyName.Should().Be(SpecialtiesTestConstants.OtherSpecialtyName);
        entry.PreviousSpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
        change.Events.Single().Details.Should().NotContain(Canary);
    }

    [Test]
    public void ToSummary_CountsDaysToTodayWhileOpenAndToTheDecisionOnceClosed()
    {
        var open = CreateReferral();
        ReferralMapper.ToSummary(new ReferralRow(open, true), Today).DaysSinceReceived.Should().Be(8);

        var closed = CreateReferral(status: ReferralStatus.Withdrawn);
        closed.DecidedAtUtc = new DateTime(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
        var summary = ReferralMapper.ToSummary(new ReferralRow(closed, false), Today);

        summary.DaysSinceReceived.Should().Be(2);
        summary.HasLetter.Should().BeFalse();
        summary.PatientName.Should().Be($"{closed.Patient.FirstName} {closed.Patient.LastName}");
        summary.SpecialtyName.Should().Be(SpecialtiesTestConstants.SpecialtyName);
        summary.ReferrerPractice.Should().Be(Practice);
        summary.Status.Should().Be("Withdrawn");
    }

    [Test]
    public void EventDetails_NotJson_IsEmpty()
    {
        var parsed = ReferralEventDetails.Parse("not json");

        parsed.ChangedFields.Should().BeEmpty();
        parsed.ToStatus.Should().BeNull();
    }
}

[TestFixture]
public sealed class ReferralSearchTests
{
    private static SearchReferralsQuery Query(
        IReadOnlyList<string>? statuses = null, Guid? patientId = null, string? search = null, string? sort = null, bool ascending = true) =>
        new(1, 10, sort, ascending, statuses, null, false, patientId, search, Registrar.UserId);

    [Test]
    public void Filter_ByStatusPatientSpecialtyAndName()
    {
        var awaiting = CreateReferral();
        var withdrawn = CreateReferral(status: ReferralStatus.Withdrawn);
        var other = CreateReferral(specialty: SpecialtiesTestConstants.CreateSpecialty(SpecialtiesTestConstants.OtherSpecialtyName));
        other.Patient.FirstName = "Zelda";
        var all = new[] { awaiting, withdrawn, other }.AsQueryable();

        all.Filter(Query(["AwaitingTriage"]), null).Should().BeEquivalentTo([awaiting, other]);
        all.Filter(Query(patientId: withdrawn.PatientId), null).Should().Equal(withdrawn);
        all.Filter(Query(), [other.SpecialtyId]).Should().Equal(other);
        all.Filter(Query(search: "zeld"), null).Should().Equal(other);
    }

    [Test]
    public void Sort_OldestReceivedFirstByDefault_ThenById()
    {
        var newer = CreateReferral();
        var older = CreateReferral();
        older.ReceivedOn = ReceivedOn.AddDays(-5);
        var all = new[] { newer, older }.AsQueryable();

        all.Sort(null, true).Should().Equal(older, newer);
        all.Sort("receivedOn", false).Should().Equal(newer, older);
        all.Sort("priority", true).Should().HaveCount(2);
    }

    [TestCase("receivedOn", true)]
    [TestCase("PATIENT", true)]
    [TestCase("specialty", true)]
    [TestCase("priority", true)]
    [TestCase("registered", true)]
    [TestCase("daysWaited", false)]
    public void IsSortColumn(string sort, bool valid) => ReferralSearch.IsSortColumn(sort).Should().Be(valid);
}
