using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.SetPatientFlag;

/// <summary>A flag an admin sets on a patient's record, with a reason, for the patient's rights under UK GDPR.</summary>
public enum PatientFlag
{
    /// <summary>Article 18: the record can't be changed until the flag is cleared.</summary>
    Restricted,

    /// <summary>Article 16: the patient disputes something in the record; staff see it on a banner.</summary>
    Disputed
}

/// <summary>
/// Sets (<paramref name="Set"/>) or clears a patient's flag (admins only). Setting one needs a reason. Restricting a
/// record with pathways under way or open appointments also needs <paramref name="ConfirmOpenPathways"/>: their
/// work stops, but for blocking and cancelling pathways and cancelling appointments.
/// </summary>
public sealed record SetPatientFlagCommand(
    Guid PatientId,
    PatientFlag Flag,
    bool Set,
    string? Reason,
    bool ConfirmOpenPathways = false
) : IRequest<PatientResponse>;
