namespace MedApp.Contracts.Sites.Requests;

public sealed class UpdateRoomRequest
{
    /// <summary>At most 100 characters, unique within the room's site.</summary>
    public string Name { get; set; } = string.Empty;
}
