using MedApp.Application.Common.Teams;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetings;

/// <summary>
/// The MDTs (meetings marked as a team's) from 60 days ago to 180 days ahead, in one team or all, by start,
/// with their counts of the items the viewer's teams may see. Names no patient, so it records nothing.
/// </summary>
public sealed record GetMdtMeetingsQuery(Guid? TeamId, TeamScope? Scope = null) : IRequest<IReadOnlyList<MdtMeetingResponse>>
{
    /// <summary>The viewer's team walls (null only outside a request: the system's).</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
