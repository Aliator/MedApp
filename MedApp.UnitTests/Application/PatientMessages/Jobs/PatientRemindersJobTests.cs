using FluentAssertions;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Jobs;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.Domain.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.PatientMessages.Jobs;

[TestFixture]
public sealed class PatientRemindersJobTests
{
    /// <summary>Monday 16 November 2026, 10:30 UK time (GMT): inside the sending hours, after 10:00.</summary>
    private static readonly DateTime MondayMorning = At(new DateOnly(2026, 11, 16), 10, 30);

    private Mock<IPatientMessageRepository> _repository = null!;
    private Mock<IPatientMessageTransport> _transport = null!;
    private Mock<IClosedDayRepository> _closedDays = null!;
    private PatientMessageOptions _options = null!;
    private ReminderOptions _reminders = null!;
    private List<PatientMessage> _added = null!;
    private List<PatientMessage> _pending = null!;
    private List<PatientMessage> _awaiting = null!;
    private Dictionary<Guid, Appointment> _appointments = null!;
    private int _saves;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IPatientMessageRepository>();
        _transport = new Mock<IPatientMessageTransport>();
        _closedDays = new Mock<IClosedDayRepository>();
        _options = Options();
        _reminders = new ReminderOptions();
        _added = [];
        _pending = [];
        _awaiting = [];
        _appointments = [];
        _saves = 0;

        _repository.Setup(r => r.GetDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetDueMissedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(r => r.GetSitesForRoomsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, MessageSite>());
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<PatientMessage>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<PatientMessage> messages, CancellationToken _) => _added.AddRange(messages))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => _pending);
        _repository.Setup(r => r.GetAppointmentsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _appointments);
        _repository.Setup(r => r.GetAwaitingDeliveryAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _awaiting);
        _repository.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Callback(() => _saves++).Returns(Task.CompletedTask);
        _closedDays.Setup(c => c.GetAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transport.Setup(t => t.SendAsync(It.IsAny<PatientMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientMessage m, CancellationToken _) => PatientMessageSendResult.Accepted("ref-" + m.Id));
    }

    private PatientRemindersJob Job() => new(
        _repository.Object,
        _transport.Object,
        _closedDays.Object,
        Microsoft.Extensions.Options.Options.Create(_options),
        Microsoft.Extensions.Options.Options.Create(_reminders),
        NullLogger<PatientRemindersJob>.Instance);

    private Appointment Booked(DateTime startUtc)
    {
        var appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Booked, startUtc);
        var patient = Patient();
        appointment.Patient = patient;
        appointment.PatientId = patient.Id;
        _appointments[appointment.Id] = appointment;
        return appointment;
    }

    [Test]
    public async Task RunAsync_Off_DoesNothing()
    {
        _options.Mode = PatientMessageMode.Off;

        var count = await Job().RunAsync(MondayMorning, CancellationToken.None);

        count.Should().Be(0);
        _repository.VerifyNoOtherCalls();
        _transport.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RunAsync_InTheDay_MakesRemindersForTheNextTwoDaysFromTwoHoursAway()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        _repository.Setup(r => r.GetDueRemindersAsync(MondayMorning.AddHours(2), MondayMorning.AddDays(2), PatientRemindersJob.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([appointment]);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        _added.Should().HaveCount(2).And.AllSatisfy(m =>
        {
            m.Kind.Should().Be(PatientMessageKind.AppointmentReminder);
            m.AppointmentId.Should().Be(appointment.Id);
            m.RequestedByUserId.Should().BeNull();
        });
    }

    [Test]
    public async Task RunAsync_AtNight_MakesNoRemindersAndSendsNoReminder_ButSendsACancellation()
    {
        var night = At(new DateOnly(2026, 11, 16), 21);
        var appointment = Booked(night.AddDays(1));
        var reminder = Message(appointment);
        appointment.Status = AppointmentStatus.Booked;
        var other = Booked(night.AddDays(1).AddHours(1));
        other.Status = AppointmentStatus.Cancelled;
        var cancellation = Message(other, PatientMessageKind.AppointmentCancelled);
        _pending.AddRange([reminder, cancellation]);

        await Job().RunAsync(night, CancellationToken.None);

        _repository.Verify(r => r.GetDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        reminder.Status.Should().Be(PatientMessageStatus.Pending);
        cancellation.Status.Should().Be(PatientMessageStatus.Sent);
        cancellation.ProviderReference.Should().Be("ref-" + cancellation.Id);
        cancellation.SentAtUtc.Should().Be(night);
        _transport.Verify(t => t.SendAsync(reminder, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_PendingThatNoLongerApplies_IsCancelledNotSent()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var message = Message(appointment, startUtc: appointment.StartUtc!.Value.AddHours(-1));
        _pending.Add(message);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Status.Should().Be(PatientMessageStatus.Cancelled);
        message.StatusReason.Should().Be(PatientMessageReasons.NoLongerApplies);
        _transport.Verify(t => t.SendAsync(It.IsAny<PatientMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _saves.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RunAsync_PendingForAnAppointmentThatsGone_IsCancelled()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var message = Message(appointment);
        _appointments.Clear();
        _pending.Add(message);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Status.Should().Be(PatientMessageStatus.Cancelled);
    }

    [Test]
    public async Task RunAsync_Sending_GoesToTheMobileOnTheRecordNow()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var message = Message(appointment);
        appointment.Patient.MobilePhone = "+447700900456";
        _pending.Add(message);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Destination.Should().Be("+447700900456");
        message.Status.Should().Be(PatientMessageStatus.Sent);
    }

    [Test]
    public async Task RunAsync_ProviderRefuses_IsFailedWithItsCode()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var message = Message(appointment);
        _pending.Add(message);
        _transport.Setup(t => t.SendAsync(message, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PatientMessageSendResult.Rejected("ValidationError"));

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Status.Should().Be(PatientMessageStatus.Failed);
        message.StatusReason.Should().Be("ValidationError");
    }

    [Test]
    public async Task RunAsync_ProviderAway_CountsAnAttempt_AndGivesUpAtTheLimit()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var message = Message(appointment);
        _pending.Add(message);
        _transport.Setup(t => t.SendAsync(message, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("away"));

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Status.Should().Be(PatientMessageStatus.Pending);
        message.SendAttempts.Should().Be(1);

        message.SendAttempts = PatientMessageRules.MaxSendAttempts - 1;
        await Job().RunAsync(MondayMorning, CancellationToken.None);

        message.Status.Should().Be(PatientMessageStatus.Failed);
        message.StatusReason.Should().Be(PatientMessageReasons.TooManyAttempts);
    }

    [Test]
    public async Task RunAsync_ProviderAsksToSlowDown_StopsSendingWithoutCountingAnAttempt()
    {
        var first = Message(Booked(MondayMorning.AddDays(1)));
        var second = Message(Booked(MondayMorning.AddDays(1).AddHours(1)));
        _pending.AddRange([first, second]);
        _transport.Setup(t => t.SendAsync(first, It.IsAny<CancellationToken>())).ReturnsAsync(PatientMessageSendResult.TryLater());

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        first.Status.Should().Be(PatientMessageStatus.Pending);
        first.SendAttempts.Should().Be(0);
        _transport.Verify(t => t.SendAsync(second, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_DeliveryKnown_UpdatesIt_AndStillOnItsWayOnlyStampsTheCheck()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var delivered = Message(appointment, status: PatientMessageStatus.Sent);
        delivered.ProviderReference = "a";
        var failed = Message(appointment, channel: PatientMessageChannel.Email, status: PatientMessageStatus.Sent);
        failed.ProviderReference = "b";
        var waiting = Message(appointment, PatientMessageKind.AppointmentMoved, status: PatientMessageStatus.Sent);
        waiting.ProviderReference = "c";
        _awaiting.AddRange([delivered, failed, waiting]);
        _transport.Setup(t => t.GetDeliveryAsync("a", It.IsAny<CancellationToken>())).ReturnsAsync(PatientMessageDelivery.Delivered);
        _transport.Setup(t => t.GetDeliveryAsync("b", It.IsAny<CancellationToken>())).ReturnsAsync(PatientMessageDelivery.Failed("permanent-failure"));
        _transport.Setup(t => t.GetDeliveryAsync("c", It.IsAny<CancellationToken>())).ReturnsAsync(PatientMessageDelivery.OnItsWay);

        var count = await Job().RunAsync(MondayMorning, CancellationToken.None);

        delivered.Status.Should().Be(PatientMessageStatus.Delivered);
        failed.Status.Should().Be(PatientMessageStatus.Failed);
        failed.StatusReason.Should().Be("permanent-failure");
        waiting.Status.Should().Be(PatientMessageStatus.Sent);
        waiting.StatusAtUtc.Should().Be(MondayMorning);
        count.Should().Be(2);
        _repository.Verify(r => r.GetAwaitingDeliveryAsync(MondayMorning.AddDays(-3), PatientRemindersJob.BatchSize, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task RunAsync_DeliveryCheckFails_StopsCheckingThisRun()
    {
        var appointment = Booked(MondayMorning.AddDays(1));
        var first = Message(appointment, status: PatientMessageStatus.Sent);
        first.ProviderReference = "a";
        var second = Message(appointment, channel: PatientMessageChannel.Email, status: PatientMessageStatus.Sent);
        second.ProviderReference = "b";
        _awaiting.AddRange([first, second]);
        _transport.Setup(t => t.GetDeliveryAsync("a", It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("away"));

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        _transport.Verify(t => t.GetDeliveryAsync("b", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_MissedMessagesOff_MakesNone()
    {
        await Job().RunAsync(MondayMorning, CancellationToken.None);

        _repository.Verify(r => r.GetDueMissedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_MissedMessagesOnMonday_AreForFridayToSunday()
    {
        _reminders.MissedMessage = true;
        var missed = Booked(At(new DateOnly(2026, 11, 13), 15));
        missed.Status = AppointmentStatus.DidNotAttend;
        _repository.Setup(r => r.GetDueMissedAsync(
                At(new DateOnly(2026, 11, 13), 0), At(new DateOnly(2026, 11, 16), 0), PatientRemindersJob.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([missed]);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        _added.Should().HaveCount(2).And.AllSatisfy(m => m.Kind.Should().Be(PatientMessageKind.MissedAppointment));
    }

    [Test]
    public async Task RunAsync_MissedMessagesBeforeTen_MakesNone()
    {
        _reminders.MissedMessage = true;

        await Job().RunAsync(At(new DateOnly(2026, 11, 16), 9, 45), CancellationToken.None);

        _repository.Verify(r => r.GetDueMissedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_MissedMessagesOnAClosedDay_WaitForTheNextWorkingDay()
    {
        _reminders.MissedMessage = true;
        _closedDays.Setup(c => c.GetAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClosedDay { Id = Guid.NewGuid(), Date = new DateOnly(2026, 11, 16), Name = "Closed" }]);

        await Job().RunAsync(MondayMorning, CancellationToken.None);

        _repository.Verify(r => r.GetDueMissedAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunAsync_TuesdayAfterAClosedMonday_CoversFridayToMonday()
    {
        _reminders.MissedMessage = true;
        _closedDays.Setup(c => c.GetAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClosedDay { Id = Guid.NewGuid(), Date = new DateOnly(2026, 11, 16), Name = "Closed" }]);

        await Job().RunAsync(At(new DateOnly(2026, 11, 17), 11), CancellationToken.None);

        _repository.Verify(r => r.GetDueMissedAsync(
            At(new DateOnly(2026, 11, 13), 0), At(new DateOnly(2026, 11, 17), 0), PatientRemindersJob.BatchSize, It.IsAny<CancellationToken>()));
    }

    [Test]
    public void Description_SaysWhereMessagesGo()
    {
        Job().Description.Should().Contain("offline").And.Contain("never to patients");
        _options.Mode = PatientMessageMode.Live;
        Job().Description.Should().Contain("GOV.UK Notify");
        _options.Mode = PatientMessageMode.Off;
        Job().Description.Should().StartWith("Off");
        Job().Name.Should().Be(PatientRemindersJob.JobName);
        Job().Schedule.Interval.Should().Be(TimeSpan.FromMinutes(15));
    }
}
