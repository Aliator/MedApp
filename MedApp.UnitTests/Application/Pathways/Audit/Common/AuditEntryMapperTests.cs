using FluentAssertions;
using MedApp.Application.Pathways.Audit.Common;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.Audit.Common;

[TestFixture]
public sealed class AuditEntryMapperTests
{
    [Test]
    public void ToResponse_Entry_ReturnsItWithTheActionByName()
    {
        var entry = new PatientPathwayAuditEntry
        {
            Id = Guid.Parse("00000011-0000-0000-0000-000000000001"),
            PatientPathwayId = Guid.Parse("00000011-0000-0000-0000-000000000002"),
            PathwayTitle = PathwayTitle,
            PatientId = Guid.Parse("00000011-0000-0000-0000-000000000003"),
            StageId = Guid.Parse("00000011-0000-0000-0000-000000000004"),
            StageName = ConsultationStage,
            Action = PatientPathwayAuditAction.StageFinished,
            UserId = Staff.UserId,
            Username = Staff.Username,
            AtUtc = Now,
            Details = "{\"notes\":\"Synthetic\"}",
        };

        var response = AuditEntryMapper.ToResponse(entry);

        response.Should().BeEquivalentTo(new
        {
            entry.Id,
            entry.PatientPathwayId,
            entry.PathwayTitle,
            entry.PatientId,
            entry.StageId,
            entry.StageName,
            Action = "StageFinished",
            entry.UserId,
            entry.Username,
            entry.AtUtc,
            entry.Details,
        });
    }

    [Test]
    public void ToResponse_EntryForTheWholePathway_HasNoStage()
    {
        var response = AuditEntryMapper.ToResponse(new PatientPathwayAuditEntry { Action = PatientPathwayAuditAction.Created });

        response.StageId.Should().BeNull();
        response.StageName.Should().BeNull();
        response.Action.Should().Be("Created");
    }
}
