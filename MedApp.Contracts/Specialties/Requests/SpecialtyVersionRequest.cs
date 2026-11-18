namespace MedApp.Contracts.Specialties.Requests;

/// <summary>Archiving or restoring a specialty, from the version the caller saw.</summary>
public sealed class SpecialtyVersionRequest
{
    /// <summary>The <c>version</c> the caller saw: if anyone has changed the specialty since, it's a 409.</summary>
    public int ExpectedVersion { get; set; }
}
