using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;
using MedApp.Infrastructure.Data;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

/// <summary>
/// The clinic picker on the patient search (MC6): patients stay centre-wide, but a team picked narrows the list to
/// patients with a pathway, referral or appointment in it. Anyone but an admin picks only one of their own teams.
/// </summary>
[TestFixture]
public sealed class PatientSearchTeamPickerTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Urology = Guid.Parse("0000005a-0000-0000-0000-0000000000d3");
    private static readonly Guid Viewer = Guid.Parse("0000005a-0000-0000-0000-0000000000f1");

    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _repository.Setup(r => r.SearchAsync(It.IsAny<SearchPatientsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Patient>([PatientsTestConstants.CreateValidPatient()], 1));
        _activity = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public void InTeam_KeepsPatientsWithAPathwayReferralOrAppointmentInTheTeam_AndEveryoneWithoutOne()
    {
        var byPathway = PatientsTestConstants.CreateValidPatient();
        var byReferral = PatientsTestConstants.CreateValidPatient();
        var byAppointment = PatientsTestConstants.CreateValidPatient();
        var elsewhere = PatientsTestConstants.CreateValidPatient();
        var pathways = new[] { new PatientPathway { PatientId = byPathway.Id, TeamId = Dermatology }, new PatientPathway { PatientId = elsewhere.Id, TeamId = Urology } }.AsQueryable();
        var referrals = new[] { new Referral { PatientId = byReferral.Id, TeamId = Dermatology } }.AsQueryable();
        var appointments = new[] { new Appointment { PatientId = byAppointment.Id, TeamId = Dermatology } }.AsQueryable();
        var patients = new[] { byPathway, byReferral, byAppointment, elsewhere }.AsQueryable();

        patients.InTeam(Dermatology, pathways, referrals, appointments).Should().Equal(byPathway, byReferral, byAppointment);
        patients.InTeam(null, pathways, referrals, appointments).Should().HaveCount(4, "patients are centre-wide");
    }

    [Test]
    public void InTeam_TranslatesToSql()
    {
        using var context = new MedAppDbContext(new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer("Server=picker.invalid;Database=None")
            .Options);

        var sql = context.Patients.InTeam(Dermatology, context.PatientPathways, context.Referrals, context.Appointments).ToQueryString();

        sql.Should().Contain("EXISTS").And.Contain("[PatientPathways]").And.Contain("[Referrals]").And.Contain("[Appointments]");
    }

    [Test]
    public async Task Handle_PickingATeamThatIsntTheirs_IsA400OnTeamId_AndSearchesNothing()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            TeamId = Urology, Scope = GrantsTeamBoundary.MemberOf(Viewer, Dermatology)
        };

        var act = () => _fixture.Create<SearchPatientsHandler>().Handle(query, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Single().PropertyName.Should().Be("TeamId");
        _repository.Verify(r => r.SearchAsync(It.IsAny<SearchPatientsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TheirOwnTeam_Searches_AndRecordsNoSearchForThePickerAlone()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            TeamId = Dermatology, Scope = GrantsTeamBoundary.MemberOf(Viewer, Dermatology)
        };

        await _fixture.Create<SearchPatientsHandler>().Handle(query, CancellationToken.None);

        _activity.Recorded.Should().BeEmpty("picking a clinic is a view, not a search for a patient");
        _activity.ViewDetails.Single().Should().Be("{}", "a member's list adds no details");
    }

    [Test]
    public async Task Handle_AnAdminsList_RecordsAccessAndTheTeamFilter()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            TeamId = Urology, Scope = GrantsTeamBoundary.AdminScope(Viewer)
        };

        await _fixture.Create<SearchPatientsHandler>().Handle(query, CancellationToken.None);

        _activity.ViewDetails.Single().Should().Be($"{{\"access\":\"Admin\",\"teamFilter\":\"{Urology}\"}}");
    }
}
