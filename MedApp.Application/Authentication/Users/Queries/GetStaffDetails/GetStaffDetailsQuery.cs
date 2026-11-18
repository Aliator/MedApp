using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetStaffDetails;

/// <summary>Someone's full name and job title (the signed-in user's own, for Account Settings).</summary>
public sealed record GetStaffDetailsQuery(string Username) : IRequest<StaffDetailsResponse>;
