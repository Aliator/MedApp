using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Specimens.Responses;
using MediatR;

namespace MedApp.Application.Specimens.Commands.RecordSpecimen;

/// <summary>
/// Records a specimen taken at a stage of a pathway: its type, lab, the clinician who asked for it, when it was taken
/// (now if null), and optionally where on the patient and notes for the lab. It gets its number from the sequence.
/// </summary>
public sealed record RecordSpecimenCommand(
    Guid PathwayId,
    Guid StageId,
    Guid SpecimenTypeId,
    Guid LaboratoryId,
    DateTime? TakenAtUtc,
    string? Site,
    string? RequestNotes,
    Guid RequestingUserId,
    PathwayActor Actor)
    : IRequest<SpecimenResponse>;
