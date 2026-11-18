using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetings;

/// <summary>
/// The MDTs (meetings marked as a team's) from 60 days ago to 180 days ahead, in one team or all, by start,
/// with their item counts. Names no patient, so it records nothing.
/// </summary>
public sealed record GetMdtMeetingsQuery(Guid? TeamId) : IRequest<IReadOnlyList<MdtMeetingResponse>>;
