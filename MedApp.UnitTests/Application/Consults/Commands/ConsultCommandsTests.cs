using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Consults.Commands.CloseConsult;
using MedApp.Application.Consults.Commands.ReplyToConsult;
using MedApp.Application.Consults.Commands.RequestConsult;
using MedApp.Application.Consults.Commands.WithdrawConsult;
using MedApp.Application.Consults.Common;
using MedApp.Application.Consults.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ConsultsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consults.Commands;

/// <summary>Shared mocks: the repository keeps what's saved; the walls read the pathway's grants.</summary>
public abstract class ConsultCommandTestBase
{
    protected Mock<IConsultRepository> Repository { get; private set; } = null!;
    protected Mock<IConsultNotifications> Notifications { get; private set; } = null!;
    protected List<ConsultChange> Saved { get; } = [];
    protected FixedTimeProvider Clock { get; } = new(Now);
    protected CancellationToken Token { get; } = new CancellationTokenSource().Token;
    protected TeamWallsTestBoundary Walls { get; private set; } = null!;
    protected PatientPathway Pathway { get; private set; } = null!;

    [SetUp]
    public void SetUpConsultMocks()
    {
        Saved.Clear();
        Walls = new TeamWallsTestBoundary();
        Walls.TeamNames[Dermatology.Id] = Dermatology.Name;
        Walls.TeamNames[Lab.Id] = Lab.Name;
        Pathway = CreateDermatologyPathway();
        Walls.Grants.AddRange(Grants(Pathway));
        Repository = new Mock<IConsultRepository>();
        Repository.Setup(r => r.SaveAsync(It.IsAny<ConsultChange>(), It.IsAny<CancellationToken>()))
            .Callback((ConsultChange change, CancellationToken _) => Saved.Add(change))
            .Returns(Task.CompletedTask);
        Notifications = new Mock<IConsultNotifications>();
    }

    protected Consult Given(ConsultStatus status)
    {
        var consult = CreateConsult(Pathway, status);
        Repository.Setup(r => r.GetForUpdateAsync(consult.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consult);
        return consult;
    }

    protected PatientPathwayAuditEntry SavedEntry =>
        Saved.Should().ContainSingle().Subject.AuditEntries.Should().ContainSingle().Subject;
}

[TestFixture]
public sealed class RequestConsultHandlerTests : ConsultCommandTestBase
{
    private Mock<ITeamRepository> _teams = null!;
    private ConsultChange? _added;

    [SetUp]
    public void SetUp()
    {
        _added = null;
        _teams = new Mock<ITeamRepository>();
        _teams.Setup(t => t.GetAsync(Dermatology.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Dermatology);
        _teams.Setup(t => t.GetAsync(Lab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Lab);
        Repository.Setup(r => r.GetPathwayAsync(Pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Pathway);
        Repository.Setup(r => r.AddAsync(It.IsAny<ConsultChange>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((ConsultChange change, string _, CancellationToken _) =>
            {
                _added = change;
                var consult = change.Consult;
                consult.Patient = Pathway.Patient;
                consult.Pathway = Pathway;
                consult.AskingTeam = Dermatology;
                consult.AskedTeam = Lab;
                Repository.Setup(r => r.GetAsync(consult.Id, It.IsAny<CancellationToken>())).ReturnsAsync(consult);
            })
            .Returns(Task.CompletedTask);
    }

    private RequestConsultHandler Handler() => new(Repository.Object, _teams.Object, Walls.Boundary, Notifications.Object, Clock);

    private RequestConsultCommand Command(Guid? askedTeamId = null) => new(Pathway.Id, askedTeamId ?? Lab.Id, $"  {Question}  ", Asker);

    [Test]
    public async Task Handle_PathwaysOwnTeamAsksTheLab_SendsItWithTheGrantAndAuditsWithoutTheQuestion()
    {
        var response = await Handler().Handle(Command(), Token);

        var consult = _added!.Consult;
        consult.Status.Should().Be(ConsultStatus.Open);
        consult.Version.Should().Be(1);
        consult.Question.Should().Be(Question);
        consult.AskingTeamId.Should().Be(Dermatology.Id);
        consult.AskedTeamId.Should().Be(Lab.Id);
        consult.RequestedByUsername.Should().Be(Asker.Username);
        consult.RequestedAtUtc.Should().Be(Now);
        _added.NewGrant.Should().BeEquivalentTo(new
        {
            PathwayId = Pathway.Id,
            PatientId = Pathway.PatientId,
            TeamId = (Guid?)Lab.Id,
            Reason = PathwayAccessReason.Consult,
            StartedAtUtc = Now,
            EndedAtUtc = (DateTime?)null,
        });
        var entry = _added.AuditEntries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.ConsultRequested);
        entry.TeamId.Should().Be(Dermatology.Id);
        entry.Details.Should().NotContain("biopsy");
        JsonDocument.Parse(entry.Details).RootElement.GetProperty("askedTeam").GetString().Should().Be("Lab");
        response.Status.Should().Be("Open");
        response.CanWithdraw.Should().BeTrue();
        response.CanClose.Should().BeFalse();
        Notifications.Verify(n => n.RequestedAsync(_added, Dermatology.Name, Token));
        Repository.Verify(r => r.AddAsync(_added, Lab.Name, Token));
    }

    [Test]
    public async Task Handle_TheLabAsksAboutDermatologysPathway_Refuses()
    {
        await FluentActions.Awaiting(() => Handler().Handle(Command() with { Actor = Pathologist }, Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.NotPathwaysTeam(Dermatology.Name));
        _added.Should().BeNull();
    }

    [Test]
    public async Task Handle_PathwayBehindTheWall_IsARecordedNotFound()
    {
        Walls.Grants.Clear();

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token)).Should().ThrowAsync<NotFoundException>();
        Walls.ShouldHaveRefused("Pathway", Pathway.Id);
    }

    [TestCase(PatientPathwayStatus.Completed)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Archived)]
    public async Task Handle_ClosedPathway_Refuses(PatientPathwayStatus status)
    {
        Pathway.Status = status;

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.ClosedPathwayMessage);
    }

    [Test]
    public async Task Handle_RestrictedRecord_Refuses()
    {
        Pathway.Patient.RestrictedAtUtc = Now;

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Handle_OwnTeam_IsABadRequest()
    {
        await FluentActions.Awaiting(() => Handler().Handle(Command(Dermatology.Id), Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{ConsultRules.OwnTeamMessage}*");
    }

    [Test]
    public async Task Handle_ArchivedOrMissingTeam_IsABadRequest()
    {
        var archived = new Team { Id = Guid.NewGuid(), Name = "Old clinic", IsArchived = true };
        _teams.Setup(t => t.GetAsync(archived.Id, It.IsAny<CancellationToken>())).ReturnsAsync(archived);

        await FluentActions.Awaiting(() => Handler().Handle(Command(archived.Id), Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{ConsultRules.TeamNotInUseMessage}*");
        await FluentActions.Awaiting(() => Handler().Handle(Command(Guid.NewGuid()), Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{ConsultRules.TeamNotInUseMessage}*");
    }

    [Test]
    public async Task Handle_AlreadyOpenWithThatTeam_Refuses()
    {
        Repository.Setup(r => r.HasOpenAsync(Pathway.Id, Lab.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.AlreadyOpen(Lab.Name));
        _added.Should().BeNull();
    }
}

[TestFixture]
public sealed class ReplyToConsultHandlerTests : ConsultCommandTestBase
{
    private ReplyToConsultHandler Handler() => new(Repository.Object, Walls.Boundary, Notifications.Object, Clock);

    [Test]
    public async Task Handle_AskedTeamReplies_RecordsItTellsAndKeepsTheGrant()
    {
        var consult = Given(ConsultStatus.Open);

        var response = await Handler().Handle(new ReplyToConsultCommand(consult.Id, $" {Reply} ", 1, Pathologist), Token);

        consult.Status.Should().Be(ConsultStatus.Replied);
        consult.Reply.Should().Be(Reply);
        consult.RepliedByUserId.Should().Be(Pathologist.UserId);
        consult.RepliedAtUtc.Should().Be(Now);
        consult.Version.Should().Be(2);
        SavedEntry.Action.Should().Be(PatientPathwayAuditAction.ConsultReplied);
        SavedEntry.Details.Should().NotContain("punch");
        Saved[0].EndsGrant.Should().BeFalse();
        response.CanReply.Should().BeFalse();
        Notifications.Verify(n => n.RepliedAsync(Saved[0], Lab.Name, Token));
    }

    [Test]
    public async Task Handle_TheAskingTeam_CantReply()
    {
        var consult = Given(ConsultStatus.Open);

        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(consult.Id, Reply, 1, Asker), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.NotAskedTeam(Lab.Name));
    }

    [Test]
    public async Task Handle_StaleVersion_Refuses()
    {
        var consult = Given(ConsultStatus.Open);

        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(consult.Id, Reply, 2, Pathologist), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Handle_AlreadyReplied_Refuses()
    {
        var consult = Given(ConsultStatus.Replied);

        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(consult.Id, Reply, 2, Pathologist), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.AlreadyRepliedMessage);
    }

    [Test]
    public async Task Handle_RestrictedRecord_Refuses()
    {
        var consult = Given(ConsultStatus.Open);
        Pathway.Patient.RestrictedAtUtc = Now;

        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(consult.Id, Reply, 1, Pathologist), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ClosedConsultsGrantEnded_IsARecordedNotFound()
    {
        var consult = Given(ConsultStatus.Closed);
        Walls.Grants.RemoveAll(g => g.Reason == PathwayAccessReason.Consult);

        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(consult.Id, Reply, 3, Pathologist), Token))
            .Should().ThrowAsync<NotFoundException>().WithMessage(ConsultRules.NotFoundMessage(consult.Id));
        Walls.ShouldHaveRefused(ConsultRules.TeamWallTarget, consult.Id);
    }

    [Test]
    public async Task Handle_NoSuchConsult_IsNotFound()
    {
        await FluentActions.Awaiting(() => Handler().Handle(new ReplyToConsultCommand(Guid.NewGuid(), Reply, 1, Pathologist), Token))
            .Should().ThrowAsync<NotFoundException>();
        Walls.Activity.Once.Should().BeEmpty();
    }
}

[TestFixture]
public sealed class CloseConsultHandlerTests : ConsultCommandTestBase
{
    private CloseConsultHandler Handler() => new(Repository.Object, Walls.Boundary, Clock);

    [Test]
    public async Task Handle_AskingTeamClosesAReply_ClosesAndEndsTheGrant()
    {
        var consult = Given(ConsultStatus.Replied);

        var response = await Handler().Handle(new CloseConsultCommand(consult.Id, 2, Asker), Token);

        consult.Status.Should().Be(ConsultStatus.Closed);
        consult.ClosedByUsername.Should().Be(Asker.Username);
        consult.ClosedAtUtc.Should().Be(Now);
        consult.Version.Should().Be(3);
        Saved[0].EndsGrant.Should().BeTrue();
        SavedEntry.Action.Should().Be(PatientPathwayAuditAction.ConsultClosed);
        response.CanClose.Should().BeFalse();
        Repository.Verify(r => r.SaveAsync(Saved[0], Token));
    }

    [Test]
    public async Task Handle_RestrictedRecord_StillCloses()
    {
        var consult = Given(ConsultStatus.Replied);
        Pathway.Patient.RestrictedAtUtc = Now;

        await Handler().Handle(new CloseConsultCommand(consult.Id, 2, Asker), Token);

        consult.Status.Should().Be(ConsultStatus.Closed);
    }

    [Test]
    public async Task Handle_NoReplyYet_Refuses()
    {
        var consult = Given(ConsultStatus.Open);

        await FluentActions.Awaiting(() => Handler().Handle(new CloseConsultCommand(consult.Id, 1, Asker), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.NoReplyYetMessage);
    }

    [Test]
    public async Task Handle_TheAskedTeam_CantClose()
    {
        var consult = Given(ConsultStatus.Replied);

        await FluentActions.Awaiting(() => Handler().Handle(new CloseConsultCommand(consult.Id, 2, Pathologist), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.NotAskingTeam(Dermatology.Name));
    }

    [Test]
    public async Task Handle_StaleVersion_Refuses()
    {
        var consult = Given(ConsultStatus.Replied);

        await FluentActions.Awaiting(() => Handler().Handle(new CloseConsultCommand(consult.Id, 1, Asker), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.ChangedMeanwhileMessage);
    }
}

[TestFixture]
public sealed class WithdrawConsultHandlerTests : ConsultCommandTestBase
{
    private WithdrawConsultHandler Handler() => new(Repository.Object, Walls.Boundary, Clock);

    [Test]
    public async Task Handle_AskingTeamWithdrawsBeforeAReply_WithdrawsAndEndsTheGrant_EvenWhileRestricted()
    {
        var consult = Given(ConsultStatus.Open);
        Pathway.Patient.RestrictedAtUtc = Now;

        await Handler().Handle(new WithdrawConsultCommand(consult.Id, 1, Asker), Token);

        consult.Status.Should().Be(ConsultStatus.Withdrawn);
        consult.ClosedByUserId.Should().Be(Asker.UserId);
        Saved[0].EndsGrant.Should().BeTrue();
        SavedEntry.Action.Should().Be(PatientPathwayAuditAction.ConsultWithdrawn);
    }

    [TestCase(ConsultStatus.Replied, ConsultRules.HasReplyMessage)]
    [TestCase(ConsultStatus.Closed, ConsultRules.AlreadyClosedMessage)]
    [TestCase(ConsultStatus.Withdrawn, ConsultRules.AlreadyClosedMessage)]
    public async Task Handle_NotOpen_Refuses(ConsultStatus status, string message)
    {
        var consult = Given(status);

        await FluentActions.Awaiting(() => Handler().Handle(new WithdrawConsultCommand(consult.Id, consult.Version, Asker), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(message);
    }

    [Test]
    public async Task Handle_AnAdmin_CantWithdraw()
    {
        var consult = Given(ConsultStatus.Open);

        await FluentActions.Awaiting(() => Handler().Handle(new WithdrawConsultCommand(consult.Id, 1, AdminViewer), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsultRules.NotAskingTeam(Dermatology.Name));
    }
}

[TestFixture]
public sealed class ConsultValidatorTests
{
    [Test]
    public void RequestConsult_MissingEverything_SaysWhatsNeeded()
    {
        var result = new RequestConsultValidator().Validate(new RequestConsultCommand(Guid.Empty, Guid.Empty, " ", Asker));

        result.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo(
            "Choose the pathway.", "Choose the team to ask.", "Write the question for the team.");
    }

    [Test]
    public void RequestConsult_QuestionTooLong_IsRefused()
    {
        var result = new RequestConsultValidator().Validate(
            new RequestConsultCommand(Guid.NewGuid(), Guid.NewGuid(), new string('x', Consult.MaxQuestionLength + 1), Asker));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The question must not exceed 2000 characters.");
    }

    [Test]
    public void RequestConsult_Valid_Passes() =>
        new RequestConsultValidator().Validate(new RequestConsultCommand(Guid.NewGuid(), Guid.NewGuid(), Question, Asker))
            .IsValid.Should().BeTrue();

    [TestCase(" ")]
    [TestCase(null)]
    public void ReplyToConsult_Empty_IsRefused(string? reply) =>
        new ReplyToConsultValidator().Validate(new ReplyToConsultCommand(Guid.NewGuid(), reply!, 1, Pathologist))
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Write the reply.");

    [Test]
    public void ReplyToConsult_TooLong_IsRefused() =>
        new ReplyToConsultValidator().Validate(
                new ReplyToConsultCommand(Guid.NewGuid(), new string('x', Consult.MaxReplyLength + 1), 1, Pathologist))
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The reply must not exceed 4000 characters.");
}
