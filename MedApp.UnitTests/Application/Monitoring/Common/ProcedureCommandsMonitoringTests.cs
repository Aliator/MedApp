using AutoFixture;
using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using MediatR;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;

namespace MedApp.UnitTests.Application.Monitoring.Common;

/// <summary>Every procedure command tells the monitor about its change, after saving it.</summary>
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
        _monitor
            .Setup(m => m.NotifyAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback(() => _order.Add("monitored"))
            .Returns(Task.CompletedTask);
        Fixture.Inject(_monitor.Object);
    }

    [Test]
    public void EveryProcedureCommandHandler_TakesTheMonitor()
    {
        var handlers = typeof(ProcedureChange).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("MedApp.Application.Procedures.PatientProcedures.Commands") == true)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() is var d
                                                   && (d == typeof(IRequestHandler<>) || d == typeof(IRequestHandler<,>))))
            .ToList();

        handlers.Should().NotBeEmpty();
        handlers.Where(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IPatientProcedureRepository)))
            .Should().OnlyContain(h => h.GetConstructors().Single().GetParameters().Any(p => p.ParameterType == typeof(IProcedureMonitor)),
                "a procedure command that saves a change must tell the monitor, or monitoring rules miss it");
    }

    [Test]
    public async Task FinishStage_TellsTheMonitorAboutTheSavedChange()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));
        using var cts = new CancellationTokenSource();

        await Fixture.Create<FinishProcedureStageHandler>()
            .Handle(new FinishProcedureStageCommand(procedure.Id, procedure.StageAt(1).Id, Staff), cts.Token);

        _order.Should().Equal("saved", "monitored");
        _monitor.Verify(m => m.NotifyAsync(
            It.Is<ProcedureChange>(c => c.Procedure == procedure && c.Actor == Staff
                                        && c.AuditEntries.Any(e => e.Action == PatientProcedureAuditAction.StageFinished)),
            cts.Token), Times.Once);
    }

    [Test]
    public async Task ChangeStatus_TellsTheMonitorAboutTheSavedChange()
    {
        var procedure = GivenProcedure(CreateProcedure(Status.InProgress));

        await Fixture.Create<ChangePatientProcedureStatusHandler>()
            .Handle(new ChangePatientProcedureStatusCommand(procedure.Id, "Blocked", Reason, Staff), CancellationToken.None);

        _order.Should().Equal("saved", "monitored");
    }
}
