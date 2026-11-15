using AutoFixture;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Constants;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for procedure handler tests: a mocked repository that records the audit entries it's given, a
/// username lookup backed by <see cref="ProceduresTestConstants.Usernames"/>, and a clock fixed at
/// <see cref="ProceduresTestConstants.Now"/>.
/// </summary>
public abstract class ProcedureHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IPatientProcedureRepository> Repository { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;

    /// <summary>Audit entries passed to AddAsync, SaveAsync or DeleteAsync.</summary>
    protected List<PatientProcedureAuditEntry> SavedAudit { get; } = [];

    [SetUp]
    public void SetUpProcedureMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Repository = new Mock<IPatientProcedureRepository>();
        Identity = new Mock<IIdentityReadService>();
        SavedAudit.Clear();

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
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);
        Repository
            .Setup(r => r.DeleteAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientProcedure _, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Repository.Object);
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

    protected void VerifySavedOnce(CancellationToken? token = null) =>
        Repository.Verify(
            r => r.SaveAsync(It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), token ?? It.IsAny<CancellationToken>()),
            Times.Once);

    protected void VerifyNotSaved() =>
        Repository.Verify(
            r => r.SaveAsync(It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
}
