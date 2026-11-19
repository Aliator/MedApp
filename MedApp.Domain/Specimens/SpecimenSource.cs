namespace MedApp.Domain.Specimens;

/// <summary>Where a sample came from (MC14 of <c>docs/multi-clinic-plan.md</c>). Stored by number: never renumber.</summary>
public enum SpecimenSource
{
    /// <summary>Taken at a clinic's pathway stage.</summary>
    Clinic = 0,

    /// <summary>Sent by a hospital with its request (MC18).</summary>
    Hospital = 1
}
