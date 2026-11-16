using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.ExtendSession;

/// <summary>"Stay signed in": extends the signed-in session (the session in the request's cookie).</summary>
public sealed record ExtendSessionCommand(Guid SessionId) : IRequest<SessionResponse>;
