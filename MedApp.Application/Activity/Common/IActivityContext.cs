namespace MedApp.Application.Activity.Common;

/// <summary>
/// The request being handled, as the activity log records it: the signed-in user and their address. The API provides
/// it from the HTTP request; with no request (or nobody signed in), the actor has no user ID.
/// </summary>
public interface IActivityContext
{
    ActivityActor Actor { get; }
}
