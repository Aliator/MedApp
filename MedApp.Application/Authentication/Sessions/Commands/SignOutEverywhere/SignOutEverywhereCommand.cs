using MediatR;

namespace MedApp.Application.Authentication.Sessions.Commands.SignOutEverywhere;

/// <summary>Ends every session <paramref name="Username"/> has, on every device. False if there's no such user.</summary>
public sealed record SignOutEverywhereCommand(
    string Username
) : IRequest<bool>;
