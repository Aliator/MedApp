using FluentAssertions;
using MedApp.Application.Procedures.Audit.Common;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Audit.Common;

[TestFixture]
public sealed class AuditEntryMapperTests
{
    [Test]
    public void ToResponse_Entry_ReturnsItWithTheActionByName()
    {
        var entry = new PatientProcedureAuditEntry
        {
            Id = Guid.Parse("00000011-0000-0000-0000-000000000001"),
            PatientProcedureId = Guid.Parse("00000011-0000-0000-0000-000000000002"),
            ProcedureTitle = ProcedureTitle,
            PatientId = Guid.Parse("00000011-0000-0000-0000-000000000003"),
            StageId = Guid.Parse("00000011-0000-0000-0000-000000000004"),
            StageName = ConsultationStage,
            Action = PatientProcedureAuditAction.StageFinished,
            UserId = Staff.UserId,
            Username = Staff.Username,
            AtUtc = Now,
            Details = "{\"notes\":\"Synthetic\"}",
        };

        var response = AuditEntryMapper.ToResponse(entry);

        response.Should().BeEquivalentTo(new
        {
            entry.Id,
            entry.PatientProcedureId,
            entry.ProcedureTitle,
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
    public void ToResponse_EntryForTheWholeProcedure_HasNoStage()
    {
        var response = AuditEntryMapper.ToResponse(new PatientProcedureAuditEntry { Action = PatientProcedureAuditAction.Created });

        response.StageId.Should().BeNull();
        response.StageName.Should().BeNull();
        response.Action.Should().Be("Created");
    }
}
