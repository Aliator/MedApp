using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Status = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureStatus;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

[TestFixture]
public sealed class CreatePatientProcedureHandlerTests : ProcedureHandlerTestBase
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientProcedureStagesRepository> _stages = null!;
    private Patient _patient = null!;
    private PatientProcedureStageTemplate _template = null!;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _stages = new Mock<IPatientProcedureStagesRepository>();
        Fixture.Inject(_patients.Object);
        Fixture.Inject(_stages.Object);

        _patient = PatientsTestConstants.CreateValidPatient();
        var consultation = CreateDefinition(ConsultationStage);
        _template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(ProcedureStage), consultation);

        _patients.Setup(r => r.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _stages.Setup(r => r.GetStageTemplateByIdAsync(_template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
    }

    private CreatePatientProcedureCommand Command(string? title = null) =>
        new(_patient.Id, _template.Id, title, "Some notes", Today.AddDays(10), "high", Staff);

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var command = Command() with { PatientId = Guid.NewGuid() };

        var act = () => Fixture.Create<CreatePatientProcedureHandler>().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_TemplateArchived_ThrowsConflict()
    {
        _template.IsArchived = true;

        var act = () => Fixture.Create<CreatePatientProcedureHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
    }

    [Test]
    public async Task Handle_ValidTemplate_CopiesItsStagesInOrderIncludingRepeats()
    {
        PatientProcedure? added = null;
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((PatientProcedure p, IEnumerable<PatientProcedureAuditEntry> audit, CancellationToken _) =>
            {
                added = p;
                SavedAudit.AddRange(audit);
            })
            .Returns(Task.CompletedTask);

        var response = await Fixture.Create<CreatePatientProcedureHandler>().Handle(Command(), CancellationToken.None);

        added.Should().NotBeNull();
        added!.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, ProcedureStage, ConsultationStage);
        added.Stages.Select(s => s.StageOrder).Should().Equal(1, 2, 3);
        added.Stages[0].Instructions.Should().Be($"{ConsultationStage} instructions");
        added.Status.Should().Be(Status.Unassigned);
        added.Priority.Should().Be(PatientProcedurePriority.High);
        added.TemplateId.Should().Be(_template.Id);
        added.Title.Should().Be(TemplateName);
        added.DueDateUtc.Should().Be(DueDates.EndOfDayUtc(Today.AddDays(10)));
        added.CreatedAt.Should().Be(Now);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.Created && e.UserId == Staff.UserId);
        response.PatientName.Should().Be($"{_patient.FirstName} {_patient.LastName}");
        response.Stages.Should().HaveCount(3);
    }

    [Test]
    public async Task Handle_TitleGiven_UsesItTrimmed()
    {
        var response = await Fixture.Create<CreatePatientProcedureHandler>()
            .Handle(Command("  Left knee  "), CancellationToken.None);

        response.Title.Should().Be("Left knee");
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        using var cts = new CancellationTokenSource();

        await Fixture.Create<CreatePatientProcedureHandler>().Handle(Command(), cts.Token);

        Repository.Verify(
            r => r.AddAsync(It.IsAny<PatientProcedure>(), It.IsAny<IEnumerable<PatientProcedureAuditEntry>>(), cts.Token),
            Times.Once);
    }
}
