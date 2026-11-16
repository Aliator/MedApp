namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

/// <summary>
/// The patient a procedure is for, as the patient banner shows them on the procedure's pages (the control for hazards
/// H-01 and H-02): who they are, and whether their record is restricted, disputed or anonymised.
/// </summary>
public sealed class ProcedurePatientResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }

    /// <summary>The NHS number, as its ten digits, or null when it isn't known.</summary>
    public string? NhsNumber { get; set; }

    public bool IsRestricted { get; set; }
    public bool IsDisputed { get; set; }
    public bool IsAnonymised { get; set; }
}
