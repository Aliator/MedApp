namespace MedApp.Contracts.Practices.Responses;

/// <summary>A GP practice in the directory, with how many patients and referrals have it.</summary>
public sealed class PracticeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Its ODS code in upper case, or null.</summary>
    public string? OdsCode { get; set; }

    /// <summary>The address's lines (none when it isn't known).</summary>
    public IReadOnlyList<string> AddressLines { get; set; } = [];

    /// <summary>The postcode, upper case with one space, or null.</summary>
    public string? Postcode { get; set; }

    public string? Phone { get; set; }
    public string? SecureEmail { get; set; }

    /// <summary>Archived: it can't be chosen for a patient or referral; those that have it keep it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Pass back as <c>expectedVersion</c> with a change.</summary>
    public int Version { get; set; }

    /// <summary>How many patients are registered with it.</summary>
    public int PatientCount { get; set; }

    /// <summary>How many referrals came from it.</summary>
    public int ReferralCount { get; set; }

    /// <summary>Only while no patient or referral has it; otherwise archive it.</summary>
    public bool CanBeDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>A practice as a patient's or referral's page shows it: its name, code and address.</summary>
public sealed class PracticeSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OdsCode { get; set; }
    public IReadOnlyList<string> AddressLines { get; set; } = [];
    public string? Postcode { get; set; }
    public string? Phone { get; set; }
    public bool IsArchived { get; set; }
}
