using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Sessions.Queries.GetSession;

/// <summary>The signed-in session's expiry (the session in the request's cookie).</summary>
public sealed record GetSessionQuery(Guid SessionId) : IRequest<SessionResponse>;
