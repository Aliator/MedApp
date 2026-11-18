using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;

[TestFixture]
public sealed class GetPatientPathwayByIdHandlerTests : PathwayHandlerTestBase
{
    [Test]
    public async Task Handle_PathwayDoesNotExist_ThrowsNotFound()
    {
        var act = () => Fixture.Create<GetPatientPathwayByIdHandler>()
            .Handle(new GetPatientPathwayByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_PathwayExists_ReturnsItWithUsernames()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var response = await Fixture.Create<GetPatientPathwayByIdHandler>()
            .Handle(new GetPatientPathwayByIdQuery(pathway.Id), CancellationToken.None);

        response.Id.Should().Be(pathway.Id);
        response.Assignments.Single().Username.Should().Be(Staff.Username);
        response.Stages.Should().HaveCount(3);
    }

    [Test]
    public async Task Handle_ViewerPermissions_DecideWhatTheResponseOffers()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));
        var handler = Fixture.Create<GetPatientPathwayByIdHandler>();

        var manager = await handler.Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, OtherUserId, StandardRoles.Manager.Permissions), CancellationToken.None);
        var nurse = await handler.Handle(
            new GetPatientPathwayByIdQuery(pathway.Id, OtherUserId, StandardRoles.Nurse.Permissions), CancellationToken.None);

        manager.Should().BeEquivalentTo(new { CanWorkOnStages = false, CanAddNotes = false, CanChangeAssignments = true });
        manager.AllowedStatusChanges.Should().BeEmpty();
        nurse.Should().BeEquivalentTo(new { CanWorkOnStages = true, CanAddNotes = true, CanChangeAssignments = false });
        nurse.AllowedStatusChanges.Should().NotBeEmpty();
    }
}
