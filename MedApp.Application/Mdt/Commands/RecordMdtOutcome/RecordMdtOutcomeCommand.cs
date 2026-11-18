using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.RecordMdtOutcome;

/// <summary>
/// Records what the team decided about an open item, as the person recording it writes it, and who was present (from
/// the meeting's attendees), once its meeting has started. It becomes Discussed, final. The pathway's assignees are
/// told; nothing about the pathway changes (any change that follows is a person's own). Allowed while the record is
/// restricted (what the team decided must never be stuck), not once it's anonymised.
/// </summary>
public sealed record RecordMdtOutcomeCommand(
    Guid ItemId,
    string Outcome,
    IReadOnlyList<Guid> PresentUserIds,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<MdtItemResponse>;
