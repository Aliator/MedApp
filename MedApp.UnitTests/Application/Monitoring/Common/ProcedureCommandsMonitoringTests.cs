using AutoFixture;
using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Procedures.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using MediatR;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

/// <summary>Every procedure command tells the monitor about its change, after saving it, with the request's token.</summary>
[TestFixture]
public sealed class ProcedureCommandsMonitoringTests : ProcedureHandlerTestBase
{
    private Mock<IProcedureMonitor> _monitor = null!;
    private List<string> _order = null!;

    [SetUp]
    public void SetUpMonitor()
    {
        _monitor = new Mock<IProcedureMonitor>();
        _order = [];
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        // A change that also asks for or tidies up stage visits is saved with them.
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.DeleteAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("saved"))
            .Returns(Task.CompletedTask);
        _monitor
            .Setup(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("monitored"))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_monitor.Object);
    }

    [Test]
    public void EveryProcedureCommandHandler_TakesTheMonitor()
    {
        var handlers = ProcedureCommandHandlers();

        handlers.Should().NotBeEmpty();
        handlers.Where(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPatientProcedureRepository)))
            .Should().OnlyContain(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IProcedureMonitor)),
                "a procedure command that saves a change must tell the monitor, or monitoring rules miss it");
    }

    [Test]
    public void EveryProcedureCommandHandler_IsRunBelow()
    {
        ProcedureCommandHandlers().Select(h => h.Name).Should().BeEquivalentTo(Commands().Select(c => c.TestName));
    }

    /// <summary>Each command, run once on a procedure it can change.</summary>
    private static IEnumerable<TestCaseData> Commands()
    {
        yield return Case<AssignPatientProcedureHandler>(Status.Unassigned, 0, (t, p) =>
            t.Fixture.Create<AssignPatientProcedureHandler>().Handle(new AssignPatientProcedureCommand(p.Id, OtherUserId, Admin), t.Token));
        yield return Case<ChangePatientProcedureStatusHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<ChangePatientProcedureStatusHandler>().Handle(new ChangePatientProcedureStatusCommand(p.Id, "Blocked", Reason, Staff), t.Token));
        yield return Case<CreatePatientProcedureHandler>(Status.Unassigned, 0, (t, _) => t.CreateAsync());
        yield return Case<DeletePatientProcedureHandler>(Status.Cancelled, 0, (t, p) =>
            t.Fixture.Create<DeletePatientProcedureHandler>().Handle(new DeletePatientProcedureCommand(p.Id, Admin), t.Token));
        yield return Case<FinishProcedureStageHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<FinishProcedureStageHandler>().Handle(new FinishProcedureStageCommand(p.Id, p.StageAt(1).Id, Staff), t.Token));
        yield return Case<StartProcedureStageHandler>(Status.Assigned, 0, (t, p) =>
            t.Fixture.Create<StartProcedureStageHandler>().Handle(new StartProcedureStageCommand(p.Id, p.StageAt(1).Id, Staff), t.Token));
        yield return Case<UnassignPatientProcedureHandler>(Status.Assigned, 0, (t, p) =>
            t.Fixture.Create<UnassignPatientProcedureHandler>().Handle(new UnassignPatientProcedureCommand(p.Id, Staff.UserId, Admin), t.Token));
        yield return Case<UnlockProcedureStageHandler>(Status.InProgress, 1, (t, p) =>
            t.Fixture.Create<UnlockProcedureStageHandler>().Handle(new UnlockProcedureStageCommand(p.Id, p.StageAt(1).Id, Admin), t.Token));
        yield return Case<UpdatePatientProcedureHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<UpdatePatientProcedureHandler>().Handle(
                new UpdatePatientProcedureCommand(p.Id, null, Today.AddDays(30), null, Staff), t.Token));
        yield return Case<AddProcedureNoteHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<AddProcedureNoteHandler>().Handle(new AddProcedureNoteCommand(p.Id, NoteText, Staff), t.Token));
        yield return Case<EditProcedureNoteHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<EditProcedureNoteHandler>().Handle(
                new EditProcedureNoteCommand(p.Id, p.AddNote().Id, "Synthetic note, edited.", 1, Staff), t.Token));
        yield return Case<DeleteProcedureNoteHandler>(Status.Cancelled, 0, (t, p) =>
            t.Fixture.Create<DeleteProcedureNoteHandler>().Handle(
                new DeleteProcedureNoteCommand(p.Id, p.AddNote().Id, "WrongPatient", null, 1, Admin), t.Token));
        yield return Case<DecideNoteDeletionRequestHandler>(Status.InProgress, 0, (t, p) => t.DecideAsync(p));
        yield return Case<UpdateProcedureStageNotesHandler>(Status.InProgress, 0, (t, p) =>
            t.Fixture.Create<UpdateProcedureStageNotesHandler>().Handle(
                new UpdateProcedureStageNotesCommand(p.Id, p.StageAt(1).Id, "Synthetic notes.", Staff), t.Token));
    }

    [TestCaseSource(nameof(Commands))]
    public async Task Handle_EachCommand_TellsTheMonitorAfterItsSave(Status status, int finishedStages, Func<ProcedureCommandsMonitoringTests, PatientProcedure, Task> run)
    {
        var procedure = GivenProcedure(CreateProcedure(status, finishedStages));
        using var cts = new CancellationTokenSource();
        Token = cts.Token;

        await run(this, procedure);

        _order.Should().Equal("saved", "monitored");
        _monitor.Verify(m => m.NotifyAsync(It.Is<ProcedureChange>(c => c.AuditEntries.Count > 0), cts.Token), Times.Once);
    }

    [Test]
    public async Task FinishStage_TellsTheMonitorAboutTheSavedChange()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        await Fixture.Create<FinishProcedureStageHandler>()
            .Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), CancellationToken.None);

        _monitor.Verify(m => m.NotifyAsync(
            It.Is<ProcedureChange>(c => c.Procedure == procedure && c.Actor == Staff
                                        && c.AuditEntries.Any(e => e.Action == PatientProcedureAuditAction.StageFinished)),
            CancellationToken.None), Times.Once);
    }

    private CancellationToken Token { get; set; }

    /// <summary>An admin deleting a note from a member of staff's request.</summary>
    private Task DecideAsync(PatientProcedure procedure)
    {
        var request = procedure.RequestDeletion(procedure.AddNote());
        var requests = new Mock<INoteDeletionRequestRepository>();
        requests.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        Fixture.Inject(requests.Object);

        return Fixture.Create<DecideNoteDeletionRequestHandler>()
            .Handle(new DecideNoteDeletionRequestCommand(request.Id, true, 1, null, null, Admin), Token);
    }

    /// <summary>
    /// Commands that take the procedure repository but never save a <see cref="ProcedureChange"/>, so nothing a rule
    /// could watch: asking for a note's deletion only points the note at the request (no audit entry).
    /// </summary>
    private static readonly Type[] SaveNoProcedureChange = [typeof(RequestNoteDeletionHandler)];

    private async Task CreateAsync()
    {
        var patients = new Mock<IPatientRepository>();
        var stages = new Mock<IPatientProcedureStagesRepository>();
        var patient = PatientsTestConstants.CreateValidPatient();
        var consultation = CreateDefinition(ConsultationStage);
        var template = CreateTemplate(TemplateName, false, consultation);
        patients.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        stages.Setup(r => r.GetStageTemplateByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        Fixture.Inject(patients.Object);
        Fixture.Inject(stages.Object);

        await Fixture.Create<CreatePatientProcedureHandler>()
            .Handle(new CreatePatientProcedureCommand(patient.Id, template.Id, null, string.Empty, Today.AddDays(7), "Medium", Staff), Token);
    }

    /// <summary>
    /// Every command handler in the Procedures area that takes the procedure repository (a procedure's own commands,
    /// and others that change a procedure, such as deciding a request to delete a note), less those that save no change.
    /// </summary>
    private static List<Type> ProcedureCommandHandlers() =>
        typeof(ProcedureChange).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("MedApp.Application.Procedures.") == true && t.Namespace.Contains(".Commands."))
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() is var d
                                                   && (d == typeof(IRequestHandler<>) || d == typeof(IRequestHandler<,>))))
            .Where(t => t.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPatientProcedureRepository)))
            .Except(SaveNoProcedureChange)
            .ToList();

    private static TestCaseData Case<THandler>(Status status, int finishedStages, Func<ProcedureCommandsMonitoringTests, PatientProcedure, Task> run) =>
        new TestCaseData(status, finishedStages, run).SetName(typeof(THandler).Name);
}
