using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Mdt;

using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt.Common;

/// <summary>The export and anonymisation cover MDT items; MDTs keep their specialty while items are on them.</summary>
[TestFixture]
public sealed class MdtPrivacyAndMeetingsTests
{
    private static PatientRecord RecordWith(MdtItem item) =>
        PrivacyTestConstants.CreateRecord(item.Patient, item.Pathway) with { MdtItems = [item] };

    [Test]
    public void Export_GivesTheQuestionOutcomeAndWhoWasPresent_ByUsername()
    {
        var item = CreateItem(CreateMdt(Now.AddHours(-2)), MdtItemStatus.Discussed);
        item.Outcome = Outcome;
        item.OutcomeByUsername = Clinician.Username;
        item.Attendees = [new MdtItemAttendee { StaffUserId = OtherUserId, Username = OtherUsername }];

        var exported = PatientExportMapper.ToResponse(RecordWith(item), new Dictionary<Guid, string>(), Now, "admin").MdtItems;

        var only = exported.Should().ContainSingle().Subject;
        only.Question.Should().Be(Question);
        only.Outcome.Should().Be(Outcome);
        only.Specialty.Should().Be(Specialty.Name);
        only.Present.Should().Equal(OtherUsername);
        only.ListedBy.Should().Be(Nurse.Username);
    }

    [Test]
    public void Anonymise_ReplacesTheQuestionAndOutcome_AndWithdrawsOpenItems()
    {
        var discussed = CreateItem(CreateMdt(Now.AddHours(-2)), MdtItemStatus.Discussed);
        discussed.Outcome = Outcome;
        var open = CreateItem(CreateMdt(Now.AddDays(2)), pathway: discussed.Pathway);
        var record = PrivacyTestConstants.CreateRecord(discussed.Patient, discussed.Pathway) with { MdtItems = [discussed, open] };

        PatientAnonymiser.Apply(record, Now);

        discussed.Question.Should().NotContain(Question);
        discussed.Outcome.Should().NotContain(Outcome);
        discussed.Status.Should().Be(MdtItemStatus.Discussed);
        open.Question.Should().NotContain(Question);
        open.Status.Should().Be(MdtItemStatus.Withdrawn);
        open.WithdrawnAtUtc.Should().Be(Now);
    }

    [Test]
    public async Task ChooseSpecialty_LeftOut_KeepsTheMeetingAsItIs()
    {
        var command = new FakeMeetingCommand(null, null);

        (await MdtMeetings.ChooseSpecialtyAsync(command, Specialty.Id, Mock.Of<ISpecialtyRepository>(), CancellationToken.None))
            .Should().Be(Specialty.Id);
    }

    [Test]
    public async Task ChooseSpecialty_NewlyChosenArchived_Refuses()
    {
        var archived = new MedApp.Domain.Specialties.Specialty { Id = Guid.NewGuid(), Name = "Old", IsArchived = true };
        var specialties = new Mock<ISpecialtyRepository>();
        specialties.Setup(s => s.GetAsync(archived.Id, It.IsAny<CancellationToken>())).ReturnsAsync(archived);

        await FluentActions.Awaiting(() => MdtMeetings.ChooseSpecialtyAsync(new FakeMeetingCommand(true, archived.Id), null, specialties.Object, CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtMeetings.ArchivedSpecialtyMessage);
    }

    [Test]
    public async Task ChooseSpecialty_NotAnMdt_IsNone()
    {
        (await MdtMeetings.ChooseSpecialtyAsync(new FakeMeetingCommand(false, Specialty.Id), Specialty.Id, Mock.Of<ISpecialtyRepository>(), CancellationToken.None))
            .Should().BeNull();
    }

    [Test]
    public async Task EnsureCanChange_ItemsOnIt_RefusesAnotherSpecialtyOrAnOrdinaryMeeting()
    {
        var meeting = CreateMdt(Now.AddDays(1));
        var items = new Mock<IMdtItemRepository>();
        items.Setup(i => i.CountOnMeetingAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync((1, 0));

        await FluentActions.Awaiting(() => MdtMeetings.EnsureCanChangeAsync(meeting, null, items.Object, CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.HasItemsMessage);
        await MdtMeetings.EnsureCanChangeAsync(meeting, meeting.SpecialtyId, items.Object, CancellationToken.None);
    }

    [Test]
    public async Task EnsureCanCancel_OutcomesRecorded_Refuses()
    {
        var meeting = CreateMdt(Now.AddDays(1));
        var items = new Mock<IMdtItemRepository>();
        items.Setup(i => i.CountOnMeetingAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync((2, 1));

        await FluentActions.Awaiting(() => MdtMeetings.EnsureCanCancelAsync(meeting, items.Object, CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.HasOutcomesMessage);
    }

    private sealed record FakeMeetingCommand(bool? IsMdt, Guid? SpecialtyId) : IMeetingCommand
    {
        public string Title => "Skin MDT";
        public string? Notes => null;
        public DateTime StartUtc => Now;
        public DateTime EndUtc => Now.AddHours(1);
        public IReadOnlyList<Guid> AttendeeUserIds => [OtherUserId];
    }
}
