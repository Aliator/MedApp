using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.UpdateSite;

/// <summary>Replaces a site's details (an address, postcode or phone left out is removed). Its rooms are unchanged.</summary>
public sealed record UpdateSiteCommand(
    Guid SiteId,
    string Name,
    IReadOnlyList<string?>? AddressLines,
    string? Postcode,
    string? Phone) : IRequest<SiteResponse>;
