using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Mdt.Commands.ListForMdt;
using MedApp.Application.Mdt.Commands.RecordMdtOutcome;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Mdt;
using MedApp.Domain.Notifications;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt.Common;

[TestFixture]
public sealed class MdtMapperTests
{
    [Test]
    public void ToResponse_OpenItemOnAStartedMdt_OffersEachActionByPermission()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-5)));

        var clinician = MdtMapper.ToResponse(item, Clinician, Now);
        var nurse = MdtMapper.ToResponse(item, Nurse, Now);

        clinician.CanRecordOutcome.Should().BeTrue();
        clinician.CanWithdraw.Should().BeTrue();
        clinician.CanDefer.Should().BeTrue();
        nurse.CanRecordOutcome.Should().BeFalse();
        nurse.CanWithdraw.Should().BeTrue();
        clinician.TeamName.Should().Be(Team.Name);
        clinician.Patient!.Id.Should().Be(item.PatientId);
    }

    [Test]
    public void ToResponse_MeetingNotStarted_NoOutcomeYet()
    {
        MdtMapper.ToResponse(CreateItem(CreateMdt(Now.AddHours(1))), Clinician, Now).CanRecordOutcome.Should().BeFalse();
    }

    [Test]
    public void ToResponse_Restricted_OnlyWithdrawAndOutcome()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-5)));
        item.Patient.RestrictedAtUtc = Now;

        var response = MdtMapper.ToResponse(item, Clinician, Now);

        response.CanDefer.Should().BeFalse();
        response.CanWithdraw.Should().BeTrue();
        response.CanRecordOutcome.Should().BeTrue();
        response.ReadOnlyReason.Should().Be(PatientRules.RestrictedMessage);
    }

    [Test]
    public void ToResponse_Discussed_NothingToDo_AndShowsTheOutcomeAndWhoWasPresent()
    {
        var item = CreateItem(CreateMdt(Now.AddHours(-2)), MdtItemStatus.Discussed);
        item.Outcome = Outcome;
        item.Attendees = [new MdtItemAttendee { StaffUserId = OtherUserId, Username = "b.synthetic" }, new MdtItemAttendee { StaffUserId = Staff.UserId, Username = "a.synthetic" }];

        var response = MdtMapper.ToResponse(item, Clinician, Now);

        response.CanWithdraw.Should().BeFalse();
        response.CanDefer.Should().BeFalse();
        response.CanRecordOutcome.Should().BeFalse();
        response.Outcome.Should().Be(Outcome);
        response.Present.Should().Equal("a.synthetic", "b.synthetic");
    }

    [Test]
    public void ToResponse_MeetingCancelled_SaysWhen()
    {
        var item = CreateItem(null);
        item.MeetingCancelledStartUtc = Now.AddDays(-1);

        MdtMapper.ToResponse(item, Clinician, Now).MeetingCancelledStartUtc.Should().Be(Now.AddDays(-1));
    }

    [Test]
    public void OutcomeRecorded_TellsEachAssigneeButTheRecorder_WithAFixedTitle()
    {
        var item = CreateItem(CreateMdt(Now));

        MdtNotifications.OutcomeRecorded(item, Clinician, Now).Should().BeEmpty("the only assignee recorded it");
        var told = MdtNotifications.OutcomeRecorded(item, Nurse, Now).Should().ContainSingle().Subject;
        told.Kind.Should().Be(NotificationKind.MdtOutcomeRecorded);
        told.PathwayId.Should().Be(item.PathwayId);
        told.Message.Should().Be($"{Nurse.Username} recorded the Dermatology MDT's outcome on a pathway you're assigned to.");
        Notification.HasFixedTitle(told.Kind).Should().BeTrue();
    }

    [Test]
    public void ListValidator_QuestionRequiredAndAtMost1000()
    {
        var validator = new ListForMdtValidator();
        validator.TestValidate(new ListForMdtCommand(Guid.NewGuid(), Guid.NewGuid(), " ", Nurse)).ShouldHaveValidationErrorFor(x => x.Question);
        validator.TestValidate(new ListForMdtCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 1001), Nurse)).ShouldHaveValidationErrorFor(x => x.Question);
        validator.TestValidate(new ListForMdtCommand(Guid.NewGuid(), Guid.Empty, Question, Nurse)).ShouldHaveValidationErrorFor(x => x.MeetingId);
        validator.TestValidate(new ListForMdtCommand(Guid.NewGuid(), Guid.NewGuid(), Question, Nurse)).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void OutcomeValidator_OutcomeAndSomeonePresentRequired()
    {
        var validator = new RecordMdtOutcomeValidator();
        validator.TestValidate(new RecordMdtOutcomeCommand(Guid.NewGuid(), "", [OtherUserId], 1, Clinician)).ShouldHaveValidationErrorFor(x => x.Outcome);
        validator.TestValidate(new RecordMdtOutcomeCommand(Guid.NewGuid(), new string('a', 2001), [OtherUserId], 1, Clinician)).ShouldHaveValidationErrorFor(x => x.Outcome);
        validator.TestValidate(new RecordMdtOutcomeCommand(Guid.NewGuid(), Outcome, [], 1, Clinician))
            .ShouldHaveValidationErrorFor(x => x.PresentUserIds).WithErrorMessage(RecordMdtOutcomeValidator.NobodyPresent);
        validator.TestValidate(new RecordMdtOutcomeCommand(Guid.NewGuid(), Outcome, [OtherUserId], 0, Clinician)).ShouldHaveValidationErrorFor(x => x.ExpectedVersion);
        validator.TestValidate(new RecordMdtOutcomeCommand(Guid.NewGuid(), Outcome, [OtherUserId], 1, Clinician)).ShouldNotHaveAnyValidationErrors();
    }
}
