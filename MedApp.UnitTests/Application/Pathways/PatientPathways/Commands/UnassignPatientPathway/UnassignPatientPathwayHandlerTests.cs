using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;

[TestFixture]
public sealed class UnassignPatientPathwayHandlerTests : PathwayHandlerTestBase
{
    private UnassignPatientPathwayHandler Handler => Fixture.Create<UnassignPatientPathwayHandler>();

    [Test]
    public async Task Handle_UserNotAssigned_ThrowsNotFound()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var act = () => Handler.Handle(new UnassignPatientPathwayCommand(pathway.Id, OtherUserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(Status.Completed, "discharged")]
    [TestCase(Status.Cancelled, "cancelled")]
    [TestCase(Status.Archived, "archived")]
    public async Task Handle_PathwayClosed_ThrowsConflictAndKeepsTheAssignment(Status status, string described)
    {
        var pathway = GivenPathway(CreatePathway(status));

        var act = () => Handler.Handle(new UnassignPatientPathwayCommand(pathway.Id, Staff.UserId, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"Staff can't be unassigned from a pathway that's {described}.");
        pathway.Assignments.Should().ContainSingle(a => a.UserId == Staff.UserId);
        VerifyNotSaved();
    }

    [Test]
    public async Task Handle_LastAssignee_UnassignsThemAndMovesToUnassigned()
    {
        var pathway = GivenPathway(CreatePathway(Status.Assigned));

        var response = await Handler.Handle(new UnassignPatientPathwayCommand(pathway.Id, Staff.UserId, Admin), CancellationToken.None);

        response.Assignments.Should().BeEmpty();
        response.Status.Should().Be("Unassigned");
        SavedAudit.First().Details.Should().Contain(Staff.Username);
        VerifySavedOnce();
    }

    [Test]
    public async Task Handle_AssigneeAccountDeleted_StillUnassigns()
    {
        var pathway = CreatePathway(Status.Assigned);
        var deletedUserId = Guid.NewGuid();
        pathway.Assignments[0].UserId = deletedUserId;
        GivenPathway(pathway);

        await Handler.Handle(new UnassignPatientPathwayCommand(pathway.Id, deletedUserId, Admin), CancellationToken.None);

        pathway.Assignments.Should().BeEmpty();
    }
}
