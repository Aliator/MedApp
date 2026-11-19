using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetingList;

/// <summary>
/// One MDT's list, in the order items were listed, each with the patient as the banner shows them. Opening (or
/// printing) it records <c>PatientListed</c> for each patient on it (decision 143). A 404 for a meeting that isn't an MDT.
/// Only the items the viewer's teams may see (their team's MDT's, or on a pathway they see), and counts of those.
/// </summary>
public sealed record GetMdtMeetingListQuery(Guid MeetingId, PathwayActor Viewer) : IRequest<MdtMeetingListResponse>;
