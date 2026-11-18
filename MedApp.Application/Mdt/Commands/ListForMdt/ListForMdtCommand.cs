using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.ListForMdt;

/// <summary>
/// Puts a pathway on an MDT's list (a meeting marked as a team's MDT that hasn't ended), with the question for the
/// team. Not for a discharged, cancelled or archived pathway, nor twice on one meeting, nor while the record is
/// restricted or anonymised (409s).
/// </summary>
public sealed record ListForMdtCommand(Guid PathwayId, Guid MeetingId, string Question, PathwayActor Actor)
    : IRequest<MdtItemResponse>;
