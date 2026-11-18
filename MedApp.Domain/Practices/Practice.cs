namespace MedApp.Domain.Practices;

/// <summary>
/// A GP practice in the centre's directory: the practice a patient is registered with (<c>Patient.PracticeId</c>), and
/// the one a referral came from (<c>Referral.PracticeId</c>). Letters will be addressed to it. Staff who can edit
/// patients keep the directory; one that a patient or referral has is archived, never deleted. It's an organisation's
/// details, not anyone's personal data.
/// </summary>
public sealed class Practice
{
    public const int MaxNameLength = 200;

    /// <summary>The longest an ODS code can be (3 to 10 letters and digits, stored in upper case).</summary>
    public const int MaxOdsCodeLength = 10;

    public const int MaxSecureEmailLength = 256;

    public Guid Id { get; init; }

    /// <summary>The practice's name, as on its letters. Not unique: two practices in different towns can share one.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Its Organisation Data Service code, if known (a GP practice's is a letter and five digits), in upper case: unique
    /// when present. Only the format is checked; MedApp has no link to the ODS.
    /// </summary>
    public string? OdsCode { get; set; }

    /// <summary>The address's lines, stored as one value (<c>Contacts.PostalAddress</c>), or null.</summary>
    public string? AddressLines { get; set; }

    /// <summary>The postcode, normalised (<c>Contacts.UkPostcodes</c>), or null.</summary>
    public string? Postcode { get; set; }

    /// <summary>Its phone number, as typed (tidied), or null.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// A secure email address for letters, or null: its domain must be one the deployment allows
    /// (<c>Letters:SecureEmailDomains</c>, <c>nhs.net</c> by default).
    /// </summary>
    public string? SecureEmail { get; set; }

    /// <summary>Archived practices can't be chosen for a patient or referral; those that have one keep it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// One more for each change. A change names the version it started from, so a page loaded before someone else's
    /// change is refused (409); it's also a concurrency token, so two saves at once can't both win.
    /// </summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}
