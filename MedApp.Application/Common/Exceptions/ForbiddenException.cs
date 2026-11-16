namespace MedApp.Application.Common.Exceptions;

/// <summary>
/// The signed-in user may use the endpoint, but not on this record (e.g. someone else's calendar). The API answers it
/// with a 403.
/// </summary>
public sealed class ForbiddenException(string message) : Exception(message);
