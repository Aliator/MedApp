using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.Domain.Appointments;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;

[TestFixture]
public sealed class CreatePatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientPathwayStagesRepository> _stages = null!;
    private Patient _patient = null!;
    private PathwayTemplate _template = null!;

    [SetUp]
    public void SetUp()
    {
        _patients = new Mock<IPatientRepository>();
        _stages = new Mock<IPatientPathwayStagesRepository>();
        Fixture.Inject(_patients.Object);
        Fixture.Inject(_stages.Object);

        _patient = PatientsTestConstants.CreateValidPatient();
        var consultation = CreateDefinition(ConsultationStage);
        _template = CreateTemplate(TemplateName, false, consultation, CreateDefinition(PathwayStage), consultation);

        _patients.Setup(r => r.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _stages.Setup(r => r.GetPathwayTemplateByIdAsync(_template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
    }

    private CreatePatientPathwayCommand Command(string? title = null) =>
        new(_patient.Id, _template.Id, title, "Some notes", Today.AddDays(10), "high", Staff);

    [Test]
    public async Task Handle_PatientDoesNotExist_ThrowsNotFound()
    {
        var command = Command() with { PatientId = Guid.NewGuid() };

        var act = () => Fixture.Create<CreatePatientPathwayHandler>().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_TemplateDoesNotExist_ThrowsNotFoundWithoutAdding()
    {
        var command = Command() with { TemplateId = Guid.NewGuid() };

        var act = () => Fixture.Create<CreatePatientPathwayHandler>().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Template '{command.TemplateId}' was not found.");
        Repository.Verify(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateHasNoStages_ThrowsConflictWithoutAdding()
    {
        _template.Maps.Clear();

        var act = () => Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"The '{TemplateName}' template has no stages.");
        Repository.Verify(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TemplateArchived_ThrowsConflict()
    {
        _template.IsArchived = true;

        var act = () => Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*archived*");
    }

    [Test]
    public async Task Handle_DefinitionWithASnomedCode_CopiesTheCodeAndTermOntoTheStage()
    {
        var coded = _template.Maps.OrderBy(m => m.StageOrder).First().StageDefinition;
        coded.SnomedCode = NhsTestConstants.SnomedCode;
        coded.SnomedTerm = NhsTestConstants.SnomedTerm;
        PatientPathway? added = null;
        Repository
            .Setup(r => r.AddAsync(It.IsAny<PathwayChange>(), It.IsAny<CancellationToken>()))
            .Callback((PathwayChange c, CancellationToken _) => added = c.Pathway)
            .Returns(Task.CompletedTask);

        var response = await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        // Consultation is first and third; the pathway stage between has no code.
        added!.Stages.Select(s => s.SnomedCode).Should().Equal(NhsTestConstants.SnomedCode, null, NhsTestConstants.SnomedCode);
        added.Stages[0].SnomedTerm.Should().Be(NhsTestConstants.SnomedTerm);
        response.Stages[0].SnomedCode.Should().Be(NhsTestConstants.SnomedCode);
        response.Stages[0].SnomedTerm.Should().Be(NhsTestConstants.SnomedTerm);
        // The new pathway's response names its patient for the banner, though the patient isn't loaded with it.
        response.Patient!.Id.Should().Be(_patient.Id);
    }

    [Test]
    public async Task Handle_ValidTemplate_CopiesItsStagesInOrderIncludingRepeats()
    {
        var response = await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        var added = SavedChanges.Should().ContainSingle().Which.Pathway;
        added.Stages.Select(s => s.Name).Should().Equal(ConsultationStage, PathwayStage, ConsultationStage);
        added.Stages.Select(s => s.StageOrder).Should().Equal(1, 2, 3);
        added.Stages[0].Instructions.Should().Be($"{ConsultationStage} instructions");
        added.Status.Should().Be(Status.Unassigned);
        added.Priority.Should().Be(PatientPathwayPriority.High);
        added.TemplateId.Should().Be(_template.Id);
        added.Title.Should().Be(TemplateName);
        added.DueDateUtc.Should().Be(DueDates.EndOfDayUtc(Today.AddDays(10)));
        added.CreatedAt.Should().Be(Now);
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.Created && e.UserId == Staff.UserId);
        response.PatientName.Should().Be($"{_patient.FirstName} {_patient.LastName}");
        response.Stages.Should().HaveCount(3);
    }

    [Test]
    public async Task Handle_TitleGiven_UsesItTrimmed()
    {
        var response = await Fixture.Create<CreatePatientPathwayHandler>()
            .Handle(Command("  Left knee  "), CancellationToken.None);

        response.Title.Should().Be("Left knee");
    }

    [Test]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        using var cts = new CancellationTokenSource();

        await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), cts.Token);

        Repository.Verify(r => r.AddAsync(It.IsAny<PathwayChange>(), cts.Token), Times.Once);
    }

    /// <summary>A template of Consultation (with the given visit) then Pathway (Suggested).</summary>
    private void GivenTemplateWithVisits(VisitRequirement first)
    {
        _template = CreateTemplate(
            TemplateName,
            false,
            CreateDefinition(ConsultationStage).WithVisit(first),
            CreateDefinition(PathwayStage).WithVisit(VisitRequirement.Suggested));
        _stages.Setup(r => r.GetPathwayTemplateByIdAsync(_template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_template);
    }

    [Test]
    public async Task Handle_TemplateWithVisits_CopiesEachStagesVisitRequirement()
    {
        GivenTemplateWithVisits(VisitRequirement.Required);

        var response = await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        SavedChanges.Single().Pathway.Stages.Select(s => s.VisitRequirement)
            .Should().Equal(VisitRequirement.Required, VisitRequirement.Suggested);
        response.Stages.Select(s => s.VisitRequirement).Should().Equal("Required", "Suggested");
    }

    [TestCase(VisitRequirement.Required)]
    [TestCase(VisitRequirement.Suggested)]
    public async Task Handle_FirstStageInvolvesAVisit_AsksForItWithThePathway(VisitRequirement requirement)
    {
        GivenTemplateWithVisits(requirement);

        await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        var change = SavedChanges.Single();
        var visit = change.NewAppointments.Should().ContainSingle().Subject;
        visit.StageId.Should().Be(change.Pathway.StageAt(1).Id);
        visit.PathwayId.Should().Be(change.Pathway.Id);
        visit.PatientId.Should().Be(_patient.Id);
        visit.Status.Should().Be(AppointmentStatus.Requested);
        visit.IsAutomatic.Should().BeTrue();
        visit.CreatedByUserId.Should().Be(Staff.UserId);
        change.AppointmentEvents.Should().ContainSingle(e => e.Action == AppointmentEventAction.Requested && e.AppointmentId == visit.Id);
    }

    [Test]
    public async Task Handle_FirstStageWithoutAVisit_AsksForNoneEvenIfALaterStageHasOne()
    {
        GivenTemplateWithVisits(VisitRequirement.None);

        await Fixture.Create<CreatePatientPathwayHandler>().Handle(Command(), CancellationToken.None);

        SavedChanges.Single().NewAppointments.Should().BeEmpty();
        SavedChanges.Single().AppointmentEvents.Should().BeEmpty();
    }
}
