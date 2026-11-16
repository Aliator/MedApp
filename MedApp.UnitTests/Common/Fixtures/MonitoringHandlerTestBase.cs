using AutoFixture;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Repositories;
using MedApp.Application.Procedures.Repositories;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Shared setup for the monitoring rule handler tests: a mocked rule repository, a username lookup backed by
/// <see cref="ProceduresTestConstants.Usernames"/>, one template (<see cref="MonitoringTestConstants.TemplateId"/>) and
/// one stage (the Consultation definition), and a clock fixed at <see cref="ProceduresTestConstants.Now"/>.
/// </summary>
public abstract class MonitoringHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<IMonitoringRuleRepository> Rules { get; private set; } = null!;
    protected Mock<IIdentityReadService> Identity { get; private set; } = null!;
    protected Mock<IPatientProcedureStagesRepository> Stages { get; private set; } = null!;

    [SetUp]
    public void SetUpMonitoringMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        Rules = new Mock<IMonitoringRuleRepository>();
        Identity = new Mock<IIdentityReadService>();
        Stages = new Mock<IPatientProcedureStagesRepository>();

        Identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)Usernames.Where(kv => ids.Contains(kv.Key)).ToDictionary());

        // test.admin is an admin; the others aren't.
        Identity
            .Setup(i => i.GetRolesForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string username, CancellationToken _) =>
                username == Admin.Username ? ["Admin", "User"] : (IReadOnlyList<string>)["User"]);

        Stages
            .Setup(s => s.GetStageTemplateNamesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string> { [TemplateId] = TemplateName }
                    .Where(kv => ids.Contains(kv.Key))
                    .ToDictionary());

        var consultation = CreateDefinition(ConsultationStage, id: ConsultationDefinitionId);
        Stages
            .Setup(s => s.GetStageDefinitionByIdAsync(ConsultationDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        Stages
            .Setup(s => s.GetStageDefinitionsByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Contains(ConsultationDefinitionId) ? [consultation] : []);

        Fixture.Inject(Rules.Object);
        Fixture.Inject(Identity.Object);
        Fixture.Inject(Stages.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }
}
