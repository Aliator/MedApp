namespace MedApp.Domain.Mdt;

/// <summary>A member of staff present when an MDT item was discussed. No foreign key to the users table, as with other staff IDs.</summary>
public sealed class MdtItemAttendee
{
    public Guid MdtItemId { get; set; }

    public Guid StaffUserId { get; set; }

    /// <summary>The username at the time, kept if the account is deleted.</summary>
    public string Username { get; set; } = string.Empty;
}
