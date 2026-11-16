namespace MedApp.Contracts.Patients.Requests;

/// <summary>
/// Body of <c>PUT api/patients/{id}/restriction</c> and <c>PUT api/patients/{id}/dispute</c> (admins only): sets or
/// clears the flag, with a reason, which is required to set it.
/// </summary>
public sealed class SetPatientFlagRequest
{
    /// <summary>True to set the flag, false to clear it.</summary>
    public bool Set { get; set; }

    /// <summary>Why; shown on the patient's banner while the flag is set, and kept in the activity log.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// For restricting a record with procedures under way or open (Requested or Booked) appointments: the admin
    /// confirms that their work stops while it's restricted (procedures can still be blocked or cancelled, and
    /// appointments cancelled). Without it, restricting such a record is a 400.
    /// </summary>
    public bool ConfirmOpenProcedures { get; set; }
}
