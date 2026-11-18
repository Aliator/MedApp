using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetPathwayMdtItems;

/// <summary>
/// A pathway's MDT items, newest listed first, for its page's MDT card. Records <c>PathwayViewed</c> (deduplicated with
/// the page's own). A 404 for an unknown pathway.
/// </summary>
public sealed record GetPathwayMdtItemsQuery(Guid PathwayId, PathwayActor Viewer) : IRequest<IReadOnlyList<MdtItemResponse>>;
