namespace MedApp.Application.Common.Exceptions;

/// <summary>The signed-in user may not do this to this record (403), e.g. delete someone else's file.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
