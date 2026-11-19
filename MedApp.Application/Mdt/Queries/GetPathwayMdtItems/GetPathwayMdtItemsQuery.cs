using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetPathwayMdtItems;

/// <summary>
/// A pathway's MDT items, newest listed first, for its page's MDT card. Records <c>PathwayViewed</c> (deduplicated with
/// the page's own, with why the viewer sees it: <c>access</c>). A 404 for an unknown pathway, and for one behind the
/// viewer's team wall (recorded).
/// </summary>
public sealed record GetPathwayMdtItemsQuery(Guid PathwayId, PathwayActor Viewer) : IRequest<IReadOnlyList<MdtItemResponse>>;
