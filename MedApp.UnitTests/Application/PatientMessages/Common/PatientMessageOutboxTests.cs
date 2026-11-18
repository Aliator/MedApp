using FluentAssertions;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Contracts.PatientMessages.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.PatientMessagesTestConstants;

namespace MedApp.UnitTests.Application.PatientMessages.Common;

[TestFixture]
public sealed class PatientMessageOutboxTests
{
    private Mock<IPatientMessageRepository> _repository = null!;
    private List<PatientMessage> _staged = null!;
    private Appointment _appointment = null!;
    private readonly Guid _requester = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IPatientMessageRepository>();
        _staged = [];
        _repository.Setup(r => r.Stage(It.IsAny<IEnumerable<PatientMessage>>()))
            .Callback((IEnumerable<PatientMessage> messages) => _staged.AddRange(messages));
        _repository.Setup(r => r.GetSitesForRoomsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids.ToDictionary(id => id, _ => Site));

        _appointment = CalendarTestConstants.CreateAppointment(AppointmentStatus.Cancelled, At(NextMonday, 10));
        _appointment.Patient = Patient();
    }

    private PatientMessageOutbox Outbox(PatientMessageMode mode = PatientMessageMode.Offline) =>
        new(_repository.Object, Microsoft.Extensions.Options.Options.Create(Options(mode)));

    [Test]
    public async Task StageAsync_Off_StagesNothing()
    {
        var outbox = Outbox(PatientMessageMode.Off);

        await outbox.StageAsync(_appointment, PatientMessageKind.AppointmentCancelled, _requester, Now, CancellationToken.None);

        outbox.IsOn.Should().BeFalse();
        _repository.VerifyNoOtherCalls();
    }

    [Test]
    public async Task StageAsync_WithARoom_StagesOnePerChannelNamingItsSite()
    {
        _appointment.RoomId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        await Outbox().StageAsync(_appointment, PatientMessageKind.AppointmentCancelled, _requester, Now, cts.Token);

        _staged.Should().HaveCount(2).And.AllSatisfy(m =>
        {
            m.Body.Should().Contain(Site.Name);
            m.RequestedByUserId.Should().Be(_requester);
        });
        _repository.Verify(r => r.ExistsAsync(_appointment.Id, PatientMessageKind.AppointmentCancelled, _appointment.StartUtc!.Value, _appointment.RoomId, cts.Token));
        _staged.Should().AllSatisfy(m => m.AppointmentRoomId.Should().Be(_appointment.RoomId));
    }

    [Test]
    public async Task StageAsync_AlreadyMessaged_StagesNothing()
    {
        _repository.Setup(r => r.ExistsAsync(_appointment.Id, PatientMessageKind.AppointmentCancelled, _appointment.StartUtc!.Value, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Outbox().StageAsync(_appointment, PatientMessageKind.AppointmentCancelled, _requester, Now, CancellationToken.None);

        _staged.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_EmailWithItsRequester_HasTheSubjectAndUsername()
    {
        var message = Message(_appointment, PatientMessageKind.AppointmentCancelled, PatientMessageChannel.Email, PatientMessageStatus.Delivered);
        var withRequester = new PatientMessage
        {
            Id = message.Id, PatientId = message.PatientId, AppointmentId = message.AppointmentId, Kind = message.Kind,
            Channel = message.Channel, Destination = message.Destination, Body = message.Body,
            AppointmentStartUtc = message.AppointmentStartUtc, Status = message.Status, RequestedByUserId = _requester,
            CreatedAtUtc = Now, SentAtUtc = Now, StatusAtUtc = Now,
        };

        PatientMessageResponse response = PatientMessageMapper.ToResponse(withRequester, new Dictionary<Guid, string> { [_requester] = "s.booker" });

        response.Kind.Should().Be("AppointmentCancelled");
        response.Channel.Should().Be("Email");
        response.Status.Should().Be("Delivered");
        response.Subject.Should().Be("Your appointment has been cancelled");
        response.RequestedByUsername.Should().Be("s.booker");
        response.Destination.Should().Be(Email);
    }

    [Test]
    public void ToResponse_TextFromTheJobAndOneByADeletedAccount_NoSubject()
    {
        var text = Message(_appointment);
        PatientMessageMapper.ToResponse(text, new Dictionary<Guid, string>()).Should()
            .Match<PatientMessageResponse>(r => r.Subject == null && r.RequestedByUsername == null);

        var byGone = new PatientMessage
        {
            Id = Guid.NewGuid(), AppointmentId = _appointment.Id, Kind = PatientMessageKind.AppointmentMoved,
            AppointmentStartUtc = Now, RequestedByUserId = Guid.NewGuid(), CreatedAtUtc = Now
        };
        PatientMessageMapper.ToResponse(byGone, new Dictionary<Guid, string>()).RequestedByUsername.Should().Be("Deleted user");
    }
}
