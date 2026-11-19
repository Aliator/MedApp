using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Specimens.Common;

[TestFixture]
public sealed class SpecimenNumbersTests
{
    [Test]
    public void Format_PrefixUkYearAndSixDigits()
    {
        SpecimenNumbers.Format("MA", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc), 123).Should().Be("MA26-000123");
        SpecimenNumbers.Format("XYZ", new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1234567).Should().Be("XYZ27-1234567");
    }

    [Test]
    public void Format_NewYearsEveAfterMidnightUk_IsTheNewYear()
    {
        // 31 December 2026 at 23:30 UTC is still 2026 in the UK (GMT): only the UK date counts.
        SpecimenNumbers.Format("MA", new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc), 1).Should().Be("MA26-000001");
    }

    [TestCase(" ma26-000123 ", "MA26-000123")]
    [TestCase("MA26 - 000123", "MA26-000123")]
    [TestCase("", null)]
    [TestCase("  ", null)]
    [TestCase(null, null)]
    [TestCase("MA26-00000000000000", null)]
    public void Normalise_RemovesSpacesAndUpperCases(string? typed, string? expected)
    {
        SpecimenNumbers.Normalise(typed).Should().Be(expected);
    }

    [TestCase("MA", true)]
    [TestCase("ABCD", true)]
    [TestCase("ABCDE", false)]
    [TestCase("ma", false)]
    [TestCase("M1", false)]
    [TestCase("", false)]
    public void Options_PrefixMustBeOneToFourCapitals(string prefix, bool valid)
    {
        new SpecimenOptions { NumberPrefix = prefix }.IsValid.Should().Be(valid);
    }
}

[TestFixture]
public sealed class SpecimenWorkflowTests
{
    [TestCase(SpecimenStatus.Taken, SpecimenStatus.Sent, true)]
    [TestCase(SpecimenStatus.Sent, SpecimenStatus.Received, true)]
    [TestCase(SpecimenStatus.Received, SpecimenStatus.Reported, true)]
    [TestCase(SpecimenStatus.Sent, SpecimenStatus.RejectedByLab, true)]
    [TestCase(SpecimenStatus.Received, SpecimenStatus.RejectedByLab, true)]
    [TestCase(SpecimenStatus.Taken, SpecimenStatus.RejectedByLab, false)]
    [TestCase(SpecimenStatus.Taken, SpecimenStatus.Lost, true)]
    [TestCase(SpecimenStatus.Received, SpecimenStatus.EnteredInError, true)]
    [TestCase(SpecimenStatus.Taken, SpecimenStatus.Received, false)]
    [TestCase(SpecimenStatus.Sent, SpecimenStatus.Reported, false)]
    [TestCase(SpecimenStatus.Reported, SpecimenStatus.Lost, false)]
    [TestCase(SpecimenStatus.Lost, SpecimenStatus.EnteredInError, false)]
    [TestCase(SpecimenStatus.RejectedByLab, SpecimenStatus.Received, false)]
    public void WhyCantMoveTo_FollowsTheStatuses(SpecimenStatus from, SpecimenStatus to, bool allowed)
    {
        var specimen = CreateSpecimen(status: from);

        (SpecimenWorkflow.WhyCantMoveTo(specimen, to) is null).Should().Be(allowed);
    }

    [Test]
    public void WhyCantTake_CurrentOrUnlockedStageOfAnOpenPathway()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);

        SpecimenWorkflow.WhyCantTake(pathway, pathway.StageAt(2)).Should().BeNull();
        SpecimenWorkflow.WhyCantTake(pathway, pathway.StageAt(1)).Should().Contain("finished and locked");
        pathway.StageAt(1).IsLocked = false;
        SpecimenWorkflow.WhyCantTake(pathway, pathway.StageAt(1)).Should().BeNull("an admin unlocked it");
    }

    [Test]
    public void ExpectedByAndOverdue_AreDateArithmetic()
    {
        var specimen = CreateSpecimen(status: SpecimenStatus.Sent, takenAtUtc: Now.AddDays(-BiopsyDays));

        SpecimenWorkflow.ExpectedBy(specimen).Should().Be(Now);
        SpecimenWorkflow.IsOverdueAtLab(specimen, Now).Should().BeFalse();
        SpecimenWorkflow.IsOverdueAtLab(specimen, Now.AddMinutes(1)).Should().BeTrue();
        specimen.Status = SpecimenStatus.Reported;
        SpecimenWorkflow.IsOverdueAtLab(specimen, Now.AddDays(5)).Should().BeFalse("a reported one is back");
    }

    [Test]
    public void WhyTimeInvalid_NotFutureNotOverAWeekNotBeforeTheStepBefore()
    {
        SpecimenWorkflow.WhyTimeInvalid(Now, null, Now, "sent").Should().BeNull();
        SpecimenWorkflow.WhyTimeInvalid(Now.AddDays(-7), null, Now, "sent").Should().BeNull();
        SpecimenWorkflow.WhyTimeInvalid(Now.AddSeconds(1), null, Now, "sent").Should().Contain("future");
        SpecimenWorkflow.WhyTimeInvalid(Now.AddDays(-7).AddSeconds(-1), null, Now, "sent").Should().Contain("7 days");
        SpecimenWorkflow.WhyTimeInvalid(Now.AddHours(-2), Now.AddHours(-1), Now, "sent").Should().Contain("before");
    }

    [Test]
    public void MayMarkEnteredInError_RecorderFor24HoursOrCorrectRecords()
    {
        var specimen = CreateSpecimen(recordedAtUtc: Now.AddHours(-1));

        SpecimenWorkflow.MayMarkEnteredInError(specimen, Nurse, Now).Should().BeTrue();
        SpecimenWorkflow.MayMarkEnteredInError(specimen, Nurse, Now.AddHours(23)).Should().BeFalse();
        SpecimenWorkflow.MayMarkEnteredInError(specimen, LabStaff, Now).Should().BeFalse();
        SpecimenWorkflow.MayMarkEnteredInError(specimen, Corrector, Now.AddDays(100)).Should().BeTrue();
        SpecimenWorkflow.MayMarkEnteredInError(specimen, Nurse with { Permissions = new HashSet<Permission>() }, Now)
            .Should().BeFalse("they no longer record specimens");
    }

    [Test]
    public void Describe_EveryStatusAndReasonInWords()
    {
        Enum.GetValues<SpecimenStatus>().Select(SpecimenWorkflow.Describe).Should().OnlyHaveUniqueItems();
        SpecimenWorkflow.Describe(SpecimenStatus.RejectedByLab).Should().Be("rejected by the lab");
        SpecimenWorkflow.Describe(SpecimenRejectionReason.InsufficientSample).Should().Be("Insufficient sample");
        Enum.GetValues<SpecimenRejectionReason>().Select(SpecimenWorkflow.Describe).Should().OnlyHaveUniqueItems();
    }
}

[TestFixture]
public sealed class SpecimenMapperTests
{
    private static readonly QrCodeImage Label = new("M0 0h1v1H0z", 29);

    [Test]
    public void ToResponse_NamesEveryoneAndGivesTheLabel()
    {
        var specimen = CreateSpecimen(status: SpecimenStatus.Received);

        var response = SpecimenMapper.ToResponse(specimen, Usernames, [], Label, LabStaff, Now);

        response.Number.Should().Be(Number);
        response.StageName.Should().Be(ConsultationStage);
        response.PathwayTitle.Should().Be(specimen.Pathway!.Title);
        response.SpecimenTypeName.Should().Be(BiopsyName);
        response.ExpectedTurnaroundDays.Should().Be(BiopsyDays);
        response.TakenByUsername.Should().Be(Staff.Username);
        response.ReceivedByUsername.Should().Be(OtherUsername);
        response.ReportedByUsername.Should().BeNull();
        response.Label.Should().BeEquivalentTo(new { QrPath = Label.Path, Size = 29 });
        response.Patient!.NhsNumber.Should().Be(specimen.Patient.NhsNumber);
        response.IsOpen.Should().BeTrue();
    }

    [Test]
    public void ToResponse_AnAccountGone_IsADeletedUser()
    {
        var specimen = CreateSpecimen();
        specimen.RequestingUserId = Guid.NewGuid();

        SpecimenMapper.ToResponse(specimen, Usernames, [], Label, Nurse, Now).RequestingUsername.Should().Be(PathwayMapper.DeletedUser);
    }

    [Test]
    public void ToResponse_Flags_FollowTheViewersPermissionsAndTheStatus()
    {
        var received = CreateSpecimen(status: SpecimenStatus.Received);

        var lab = SpecimenMapper.ToResponse(received, Usernames, [], Label, LabStaff, Now);
        var nurse = SpecimenMapper.ToResponse(received, Usernames, [], Label, Nurse, Now);

        lab.Should().BeEquivalentTo(new { CanReport = true, CanReject = true, CanMarkLost = true, CanMarkReceived = false, CanEdit = false, CanMarkSent = false });
        nurse.Should().BeEquivalentTo(new { CanReport = false, CanReject = false, CanMarkLost = true, CanEdit = false });
    }

    [Test]
    public void ToResponse_Restricted_CanStillGoOnButNotBeEdited()
    {
        var taken = CreateSpecimen();
        taken.Patient.RestrictedAtUtc = Now;

        var response = SpecimenMapper.ToResponse(taken, Usernames, [], Label, Nurse, Now);

        response.CanEdit.Should().BeFalse();
        response.CanMarkSent.Should().BeTrue();
        response.ReadOnlyReason.Should().Contain("restricted");
    }

    [Test]
    public void ToResponse_Closed_SaysSoAndAllowsNothing()
    {
        var lost = CreateSpecimen(status: SpecimenStatus.Lost);

        var response = SpecimenMapper.ToResponse(lost, Usernames, [], Label, Corrector, Now);

        response.ReadOnlyReason.Should().Be(SpecimenWorkflow.ClosedMessage(SpecimenStatus.Lost));
        response.Should().BeEquivalentTo(new { CanMarkLost = false, CanMarkEnteredInError = false, IsOpen = false });
    }

    [Test]
    public void ToResponse_OtherSpecimensFromTheStage_LeaveItselfOut_OldestFirst()
    {
        var specimen = CreateSpecimen();
        var retake = CreateSpecimen(takenAtUtc: Now.AddHours(-1));
        var first = CreateSpecimen(takenAtUtc: Now.AddDays(-3));

        var response = SpecimenMapper.ToResponse(specimen, Usernames, [retake, specimen, first], Label, Nurse, Now);

        response.OtherSpecimensFromStage.Select(o => o.Id).Should().Equal(first.Id, retake.Id);
    }

    [Test]
    public void ToSummary_DatesNamesAndStatusesOnly()
    {
        var specimen = CreateSpecimen(status: SpecimenStatus.Sent, takenAtUtc: Now.AddDays(-20));

        var summary = SpecimenMapper.ToSummary(specimen, Now);

        summary.PatientName.Should().Be($"{specimen.Patient.FirstName} {specimen.Patient.LastName}");
        summary.IsOverdueAtLab.Should().BeTrue();
        summary.ExpectedByUtc.Should().Be(specimen.TakenAtUtc.AddDays(BiopsyDays));
    }

    [Test]
    public void ToResponse_Types_CanBeDeletedOnlyWhenUnused_AndCarryTheirSnomedCode()
    {
        SpecimenMapper.ToResponse(Biopsy(), inUse: true).CanBeDeleted.Should().BeFalse();
        var type = Biopsy();
        type.SnomedCode = "309066003";
        SpecimenMapper.ToResponse(type, inUse: false).Should().BeEquivalentTo(new { CanBeDeleted = true, SnomedCode = "309066003", Name = BiopsyName });
    }
}

[TestFixture]
public sealed class SpecimenNotificationsTests
{
    [Test]
    public void Rejected_ToRequesterAndTaker_NotTheActor_FixedTitleNoPatient()
    {
        var specimen = CreateSpecimen();
        specimen.RequestingUserId = Admin.UserId;

        var notifications = SpecimenNotifications.Rejected(specimen, LabStaff, Now);

        notifications.Select(n => n.UserId).Should().BeEquivalentTo([Admin.UserId, Staff.UserId]);
        notifications.Should().AllSatisfy(n =>
        {
            n.Title.Should().Be(SpecimenNotifications.RejectedTitle);
            n.Message.Should().NotContain(specimen.Patient.FirstName).And.NotContain(specimen.Site!);
            n.SubjectId.Should().Be(specimen.Id);
        });
    }

    [Test]
    public void Lost_AVowelType_SaysAn()
    {
        var specimen = CreateSpecimen();
        specimen.SpecimenType.Name = "Endometrial biopsy";

        SpecimenNotifications.Lost(specimen, LabStaff, Now).Single().Message
            .Should().Be($"{OtherUsername} marked a sample lost (Endometrial biopsy).");
    }
}

[TestFixture]
public sealed class StageGatesTests
{
    [Test]
    public void WhyCantFinish_LastStageWithAnOpenSpecimen_NamesIt()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 2);
        var open = CreateSpecimen(pathway, SpecimenStatus.Sent);

        StageGates.WhyCantFinish(pathway, pathway.StageAt(3), [open])
            .Should().Contain(Number).And.Contain(BiopsyName).And.Contain("sent");
    }

    [Test]
    public void WhyCantFinish_NotTheLastStage_OrNoneOpen_IsNull()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var open = CreateSpecimen(pathway, SpecimenStatus.Taken);

        StageGates.WhyCantFinish(pathway, pathway.StageAt(2), [open]).Should().BeNull();
        var last = CreatePathway(Status.InProgress, finishedStages: 2);
        StageGates.WhyCantFinish(last, last.StageAt(3), []).Should().BeNull();
        StageGates.WhyCantFinish(last, last.StageAt(3), [CreateSpecimen(last, SpecimenStatus.Reported)]).Should().BeNull();
    }

    [Test]
    public void IsLastToFinish_AnUnlockedEarlierStageWhenTheRestAreDone()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 3);
        pathway.StageAt(1).IsLocked = false;

        StageGates.IsLastToFinish(pathway, pathway.StageAt(1)).Should().BeTrue();
    }
}
