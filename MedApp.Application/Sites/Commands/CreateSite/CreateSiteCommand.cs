using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.CreateSite;

/// <summary>Adds a site: a name (unique) and, optionally, its address, postcode and phone number.</summary>
public sealed record CreateSiteCommand(string Name, IReadOnlyList<string?>? AddressLines, string? Postcode, string? Phone)
    : IRequest<SiteResponse>;
