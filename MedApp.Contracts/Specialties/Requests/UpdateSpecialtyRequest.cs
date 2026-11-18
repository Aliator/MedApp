namespace MedApp.Contracts.Specialties.Requests;

/// <summary>Renames a specialty. What has it (templates, pathways, staff) shows the new name.</summary>
public sealed class UpdateSpecialtyRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>version</c> the caller saw: if anyone has changed the specialty since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}
