using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using MediatR;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

/// <summary>Every pathway command tells the monitor about its change, after saving it, with the request's token.</summary>
[TestFixture]
public sealed class PathwayCommandsMonitoringTests : PathwayHandlerTestBase
{
    private Mock<IPathwayMonitor> _monitor = null!;
    private List<string> _order = null!;

    [SetUp]
    public void SetUpMonitor()
    {
        _monitor = new Mock<IPathwayMonitor>();
        _order = [];
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        // A change that also asks for or tidies up stage visits is saved with them.
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.DeleteAsync(It.IsAny<PatientPathway>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        _monitor
            .Setup(m => m.NotifyAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("monitored"))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_monitor.Object);

        // Everyone holds every permission asked about (an assignee must work on pathways).
        var holders = new Mock<IPermissionHolders>();
        holders
            .Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Permission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Permission _, CancellationToken _) => (IReadOnlySet<Guid>)ids.ToHashSet());
        Fixture.Inject(holders.Object);
    }

    [Test]
    public void EveryPathwayCommandHandler_TakesTheMonitor()
    {
        var handlers = PathwayCommandHandlers();

        handlers.Should().NotBeEmpty();
        handlers.Where(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPatientPathwayRepository)))
            .Should().OnlyContain(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPathwayMonitor)),
                "a pathway command that saves a change must tell the monitor, or monitoring rules miss it");
    }

    [Test]
    public void EveryPathwayCommandHandler_IsRunBelow()
    {
        PathwayCommandHandlers().Select(h => h.Name).Should().BeEquivalentTo(Commands().Select(c => c.TestName));
    }

    /// <summary>Each command, run once on a pathway it can change.</summary>
    private static IEnumerable<TestCaseData> Commands()
    {
        yield return Case<AssignPatientPathwayHandler>(Status.Unassigned, 0, (t, p) =>
            t.Fixture.Create<AssignPatientPathwayHandler>().Handle(new AssignPatientPathwayCommand(p.Id, OtherUserId, Admin), t.Token));
        yield return Case<ChangePatientPathwayStatusHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<ChangePatientPathwayStatusHandler>().Handle(new ChangePatientPathwayStatusCommand(p.Id, "Blocked", Reason, Staff), t.Token));
        yield return Case<CreatePatientPathwayHandler>(Status.Unassigned, 0, (t, _) => t.CreateAsync());
        yield return Case<DeletePatientPathwayHandler>(Status.Cancelled, 0, (t, p) =>
            t.Fixture.Create<DeletePatientPathwayHandler>().Handle(new DeletePatientPathwayCommand(p.Id, Admin), t.Token));
        yield return Case<FinishPathwayStageHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<FinishPathwayStageHandler>().Handle(new FinishPathwayStageCommand(p.Id, p.StageAt(1).Id, Staff), t.Token));
        yield return Case<StartPathwayStageHandler>(Status.Assigned, 0, (t, p) =>
            t.Fixture.Create<StartPathwayStageHandler>().Handle(new StartPathwayStageCommand(p.Id, p.StageAt(1).Id, Staff), t.Token));
        yield return Case<UnassignPatientPathwayHandler>(Status.Assigned, 0, (t, p) =>
            t.Fixture.Create<UnassignPatientPathwayHandler>().Handle(new UnassignPatientPathwayCommand(p.Id, Staff.UserId, Admin), t.Token));
        yield return Case<UnlockPathwayStageHandler>(Status.InProgress, 1, (t, p) =>
            t.Fixture.Create<UnlockPathwayStageHandler>().Handle(new UnlockPathwayStageCommand(p.Id, p.StageAt(1).Id, Admin), t.Token));
        yield return Case<UpdatePatientPathwayHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<UpdatePatientPathwayHandler>().Handle(
                new UpdatePatientPathwayCommand(p.Id, null, Today.AddDays(30), null, Staff), t.Token));
        yield return Case<AddPathwayNoteHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<AddPathwayNoteHandler>().Handle(new AddPathwayNoteCommand(p.Id, NoteText, Staff), t.Token));
        yield return Case<EditPathwayNoteHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<EditPathwayNoteHandler>().Handle(
                new EditPathwayNoteCommand(p.Id, p.AddNote().Id, "Synthetic note, edited.", 1, Staff), t.Token));
        yield return Case<DeletePathwayNoteHandler>(Status.Cancelled, 0, (t, p) =>
            t.Fixture.Create<DeletePathwayNoteHandler>().Handle(
                new DeletePathwayNoteCommand(p.Id, p.AddNote().Id, "WrongPatient", null, 1, Admin), t.Token));
        yield return Case<DecideNoteDeletionRequestHandler>(Status.InProgress, 0, (t, p) => t.DecideAsync(p));
        yield return Case<UpdatePathwayStageNotesHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<UpdatePathwayStageNotesHandler>().Handle(
                new UpdatePathwayStageNotesCommand(p.Id, p.StageAt(1).Id, "Synthetic notes.", Staff), t.Token));
    }

    [TestCaseSource(nameof(Commands))]
    public async Task Handle_EachCommand_TellsTheMonitorAfterItsSave(Status status, int finishedStages, Func<PathwayCommandsMonitoringTests, PatientPathway, Task> run)
    {
        var pathway = GivenPathway(CreatePathway(status, finishedStages));
        using var cts = new CancellationTokenSource();
        Token = cts.Token;

        await run(this, pathway);

        _order.Should().Equal("saved", "monitored");
        _monitor.Verify(m => m.NotifyAsync(It.Is<PathwayChange>(c => c.AuditEntries.Count > 0), cts.Token), Times.Once);
    }

    [Test]
    public async Task FinishStage_TellsTheMonitorAboutTheSavedChange()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        _monitor.Verify(m => m.NotifyAsync(
            It.Is<PathwayChange>(c => c.Pathway == pathway && c.Actor == Staff
                                        && c.AuditEntries.Any(e => e.Action == PatientPathwayAuditAction.StageFinished)),
            CancellationToken.None), Times.Once);
    }

    private CancellationToken Token { get; set; }

    /// <summary>An admin deleting a note from a member of staff's request.</summary>
    private Task DecideAsync(PatientPathway pathway)
    {
        var request = pathway.RequestDeletion(pathway.AddNote());
        var requests = new Mock<INoteDeletionRequestRepository>();
        requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        Fixture.Inject(requests.Object);

        return Fixture.Create<DecideNoteDeletionRequestHandler>()
            .Handle(new DecideNoteDeletionRequestCommand(request.Id, true, 1, null, null, Admin), Token);
    }

    /// <summary>
    /// Commands that take the pathway repository but never save a <see cref="PathwayChange"/>, so nothing a rule
    /// could watch: asking for a note's deletion only points the note at the request (no audit entry).
    /// </summary>
    private static readonly Type[] SaveNoPathwayChange = [typeof(RequestNoteDeletionHandler)];

    private async Task CreateAsync()
    {
        var patients = new Mock<IPatientRepository>();
        var stages = new Mock<IPatientPathwayStagesRepository>();
        var patient = PatientsTestConstants.CreateValidPatient();
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        stages.Setup(r => r.GetPathwayTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        Fixture.Inject(patients.Object);
        Fixture.Inject(stages.Object);

        await Fixture.Create<CreatePatientPathwayHandler>()
            .Handle(new CreatePatientPathwayCommand(patient.Id, template.Id, null, string.Empty, Today.AddDays(7), "Medium", Staff), Token);
    }

    /// <summary>
    /// Every command handler in the Pathways area that takes the pathway repository (a pathway's own commands,
    /// and others that change a pathway, such as deciding a request to delete a note), less those that save no change.
    /// </summary>
    private static List<Type> PathwayCommandHandlers() =>
        typeof(PathwayChange).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("MedApp.Application.Pathways.") == true && t.Namespace.Contains(".Commands."))
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() is var d
                                                   && (d == typeof(IRequestHandler<>) || d == typeof(IRequestHandler<,>))))
            .Where(t => t.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPatientPathwayRepository)))
            .Except(SaveNoPathwayChange)
            .ToList();

    private static TestCaseData Case<THandler>(Status status, int finishedStages, Func<PathwayCommandsMonitoringTests, PatientPathway, Task> run) =>
        new TestCaseData(status, finishedStages, run).SetName(typeof(THandler).Name);
}
