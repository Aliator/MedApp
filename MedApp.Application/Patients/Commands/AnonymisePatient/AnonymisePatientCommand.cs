using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

/// <summary>Why a record is anonymised: one of a fixed list, so the reason itself can't hold a patient's details.</summary>
public enum AnonymisationReason
{
    /// <summary>Past its retention period, reviewed and approved.</summary>
    RetentionPeriodEnded,

    /// <summary>The patient asked for erasure (Article 17), and it applies.</summary>
    ErasureRequest,

    /// <summary>The record was created in error.</summary>
    CreatedInError
}

/// <summary>
/// Anonymises (strictly, pseudonymises) a patient's record (admins only; see <c>PatientAnonymiser</c>), confirmed by
/// typing the patient's full name (<paramref name="Confirmation"/>), with a reason from <see cref="AnonymisationReason"/>
/// (by name) and an optional note, which must hold no patient details, for the activity log.
/// </summary>
public sealed record AnonymisePatientCommand(
    Guid PatientId,
    string Reason,
    string? Note,
    string Confirmation
) : IRequest<PatientResponse>;
