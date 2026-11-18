using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetings;

/// <summary>
/// The MDTs (meetings marked as a specialty's) from 60 days ago to 180 days ahead, in one specialty or all, by start,
/// with their item counts. Names no patient, so it records nothing.
/// </summary>
public sealed record GetMdtMeetingsQuery(Guid? SpecialtyId) : IRequest<IReadOnlyList<MdtMeetingResponse>>;
