using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetStaffDetails;

/// <summary>
/// Replaces someone's full name and job title (empty or null removes one): their own from Account Settings, or anyone's
/// by a holder of ManageUsers. Returns them as they are now.
/// </summary>
public sealed record SetStaffDetailsCommand(string Username, string? FullName, string? JobTitle)
    : IRequest<StaffDetailsResponse>;
