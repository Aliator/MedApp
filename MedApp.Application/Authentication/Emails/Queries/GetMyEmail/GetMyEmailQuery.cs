using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Queries.GetMyEmail;

/// <summary>The signed-in account's email address, and any waiting to be confirmed.</summary>
public sealed record GetMyEmailQuery(
    Guid UserId
) : IRequest<EmailResponse>;
