using MediatR;

namespace MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;

/// <summary>Removes the signed-in account's email address, and any waiting to be confirmed.</summary>
public sealed record RemoveMyEmailCommand(
    Guid UserId
) : IRequest;
