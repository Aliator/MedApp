namespace MedApp.Contracts.Sites.Requests;

public sealed class CreateRoomRequest
{
    /// <summary>The site it's at: one that isn't archived. A room never moves to another site.</summary>
    public Guid SiteId { get; set; }

    /// <summary>At most 100 characters, unique within the site.</summary>
    public string Name { get; set; } = string.Empty;
}
