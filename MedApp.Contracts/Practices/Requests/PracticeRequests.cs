namespace MedApp.Contracts.Practices.Requests;

/// <summary>A GP practice to add to the directory.</summary>
public sealed class CreatePracticeRequest
{
    /// <summary>The practice's name: required, at most 200 characters. Not unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its ODS code, if known: 3 to 10 letters and digits (spaces ignored). Unique when given.</summary>
    public string? OdsCode { get; set; }

    /// <summary>The address's lines: at most three, each at most 100 characters. Empty lines are dropped.</summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode, if known (any case, spaces anywhere).</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK phone number, if known.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// A secure email address for letters, if any: its domain must be one the deployment allows
    /// (<c>Letters:SecureEmailDomains</c>, <c>nhs.net</c> by default).
    /// </summary>
    public string? SecureEmail { get; set; }
}

/// <summary>A practice's new details, from the version the caller saw.</summary>
public sealed class UpdatePracticeRequest
{
    /// <summary>The practice's name: required, at most 200 characters. Not unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its ODS code, if known: 3 to 10 letters and digits (spaces ignored). Unique when given.</summary>
    public string? OdsCode { get; set; }

    /// <summary>The address's lines: at most three, each at most 100 characters. Empty lines are dropped.</summary>
    public List<string>? AddressLines { get; set; }

    /// <summary>A UK postcode, if known (any case, spaces anywhere).</summary>
    public string? Postcode { get; set; }

    /// <summary>A UK phone number, if known.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// A secure email address for letters, if any: its domain must be one the deployment allows
    /// (<c>Letters:SecureEmailDomains</c>, <c>nhs.net</c> by default).
    /// </summary>
    public string? SecureEmail { get; set; }

    /// <summary>The <c>version</c> the caller saw: if anyone has changed the practice since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>Archiving or restoring a practice, from the version the caller saw.</summary>
public sealed class PracticeVersionRequest
{
    /// <summary>The <c>version</c> the caller saw: if anyone has changed the practice since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}
