using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// What can be done to a patient's record. A restricted record (Article 18) can't be changed until an admin lifts the
/// restriction, and an anonymised one can't be changed at all; that covers the patient and their procedures. So that
/// care under way can be paused safely, a restricted record's procedures can still be blocked or cancelled, with a
/// reason (<see cref="PausesWhileRestricted"/>).
/// </summary>
public static class PatientRules
{
    public const string RestrictedMessage =
        "This patient's record is restricted, so it can't be changed until an admin lifts the restriction. Its procedures can still be blocked or cancelled, with a reason.";

    /// <summary>The status changes still allowed on a restricted record's procedures: pausing or stopping care, with a reason.</summary>
    public static readonly IReadOnlyList<PatientProcedureStatus> PausesWhileRestricted =
        [PatientProcedureStatus.Blocked, PatientProcedureStatus.Cancelled];

    public const string AnonymisedMessage = "This patient's record has been anonymised, so it can't be changed.";

    /// <summary>A save refused because someone else saved the record first (its concurrency token changed).</summary>
    public const string ChangedMeanwhileMessage =
        "Someone else changed this patient's record while you were working on it. Reload it to see their change, then try again.";

    public static bool IsRestricted(Patient? patient) => patient?.RestrictedAtUtc is not null;

    public static bool IsAnonymised(Patient? patient) => patient?.AnonymisedAtUtc is not null;

    /// <summary>Whether the patient's record, and their procedures, can be changed.</summary>
    public static bool CanChange(Patient? patient) => WhyCantChange(patient) is null;

    /// <summary>Why the record can't be changed, or null if it can.</summary>
    public static string? WhyCantChange(Patient? patient) =>
        IsAnonymised(patient) ? AnonymisedMessage
        : IsRestricted(patient) ? RestrictedMessage
        : null;

    /// <summary>
    /// Throws a 409 unless the record's procedures can be paused (blocked or cancelled): anything but an anonymised
    /// record. Restricted records can.
    /// </summary>
    public static void EnsureCanPause(Patient? patient)
    {
        if (IsAnonymised(patient))
            throw new ConflictException(AnonymisedMessage);
    }

    /// <summary>Throws a 409 (<see cref="ConflictException"/>) if the record can't be changed.</summary>
    public static void EnsureCanChange(Patient? patient)
    {
        if (WhyCantChange(patient) is { } reason)
            throw new ConflictException(reason);
    }
}
