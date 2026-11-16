using AutoFixture;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for procedure handler tests: a mocked repository that records the audit entries it's given (with
/// either kind of save), a mocked appointment repository with no appointments, a username lookup backed by
/// <see cref="ProceduresTestConstants.Usernames"/>, and a clock fixed at <see cref="ProceduresTestConstants.Now"/>.
/// </summary>
public abstract class ProcedureHandlerTestBase
{
    private readonly List<CancellationToken> _saveTokens = [];

    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IPatientProcedureRepository> Repository { get; private set; } = null!;
    protected Mock<IAppointmentRepository> Appointments { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;

    /// <summary>Audit entries passed to AddAsync, SaveAsync or DeleteAsync.</summary>
    protected List<PatientProcedureAuditEntry> SavedAudit { get; } = [];

    /// <summary>Procedure changes passed to AddAsync or SaveAsync, with the appointments and history they bring.</summary>
    protected List<ProcedureChange> SavedChanges { get; } = [];

    [SetUp]
    public void SetUpProcedureMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<IPatientProcedureRepository>();
        Appointments = new Mock<IAppointmentRepository>();
        Identity = new Mock<IIdentityReadService>();
        SavedAudit.Clear();
        SavedChanges.Clear();
        _saveTokens.Clear();

        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)ProceduresTestConstants.Usernames
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());

        Repository
            .Setup(r => r.AddAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientProcedure _, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback((ProcedureChange change, CancellationToken _) => Recorded(change))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken token) =>
            {
                SavedAudit.AddRange(audit);
                _saveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<ProcedureChange>(), It.IsAny<CancellationToken>()))
            .Callback((ProcedureChange change, CancellationToken token) =>
            {
                Recorded(change);
                _saveTokens.Add(token);
            })
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.DeleteAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientProcedure _, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);

        Appointments
            .Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Appointments
            .Setup(r => r.GetRequestedForProcedureForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Fixture.Inject(Repository.Object);
        Fixture.Inject(Appointments.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(ProceduresTestConstants.Now));
    }

    /// <summary>Makes the repository return this procedure, for reading and for updating.</summary>
    protected PatientProcedure GivenProcedure(PatientProcedure procedure)
    {
        Repository.Setup(r => r.GetForUpdateAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync(procedure);
        Repository.Setup(r => r.GetByIdAsync(procedure.Id, It.IsAny<CancellationToken>())).ReturnsAsync(procedure);
        return procedure;
    }

    /// <summary>Makes the appointment repository return these appointments, for the stages asked about.</summary>
    protected void GivenStageAppointments(params Appointment[] appointments) =>
        Appointments
            .Setup(r => r.GetForStagesForUpdateAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> stageIds, CancellationToken _) =>
                (IReadOnlyList<Appointment>)appointments.Where(a => a.StageId is { } id && stageIds.Contains(id)).ToList());

    /// <summary>Saved once (with either kind of save), with this token if one is given.</summary>
    protected void VerifySavedOnce(CancellationToken? token = null)
    {
        Assert.That(_saveTokens, Has.Count.EqualTo(1), "The procedure should have been saved once.");
        if (token is { } expected)
            Assert.That(_saveTokens[0], Is.EqualTo(expected));
    }

    protected void VerifyNotSaved() =>
        Assert.That(_saveTokens, Is.Empty, "The procedure should not have been saved.");

    private void Recorded(ProcedureChange change)
    {
        SavedAudit.AddRange(change.AuditEntries);
        SavedChanges.Add(change);
    }
}
