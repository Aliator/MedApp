using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// What can be done to a patient's record. A restricted record (Article 18) can't be changed until an admin lifts the
/// restriction, and an anonymised one can't be changed at all; that covers the patient and their procedures.
/// </summary>
public static class PatientRules
{
    public const string RestrictedMessage =
        "This patient's record is restricted, so it can't be changed until an admin lifts the restriction.";

    public const string AnonymisedMessage = "This patient's record has been anonymised, so it can't be changed.";

    public static bool IsRestricted(Patient? patient) => patient?.RestrictedAtUtc is not null;

    public static bool IsAnonymised(Patient? patient) => patient?.AnonymisedAtUtc is not null;

    /// <summary>Whether the patient's record, and their procedures, can be changed.</summary>
    public static bool CanChange(Patient? patient) => WhyCantChange(patient) is null;

    /// <summary>Why the record can't be changed, or null if it can.</summary>
    public static string? WhyCantChange(Patient? patient) =>
        IsAnonymised(patient) ? AnonymisedMessage
        : IsRestricted(patient) ? RestrictedMessage
        : null;

    /// <summary>Throws a 409 (<see cref="ConflictException"/>) if the record can't be changed.</summary>
    public static void EnsureCanChange(Patient? patient)
    {
        if (WhyCantChange(patient) is { } reason)
            throw new ConflictException(reason);
    }
}
