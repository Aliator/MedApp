using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtItemsAwaitingMeeting;

/// <summary>
/// Open items whose MDT was cancelled ("Meeting cancelled: list again"), in one team or all, oldest listed first.
/// Only the items the viewer's teams may see (their team's MDT's, or on a pathway they see). Records
/// <c>PatientListed</c> for each patient shown.
/// </summary>
public sealed record GetMdtItemsAwaitingMeetingQuery(Guid? TeamId, PathwayActor Viewer) : IRequest<IReadOnlyList<MdtItemResponse>>;
