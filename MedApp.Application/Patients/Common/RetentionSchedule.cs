using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Patients.Common;

public enum RetentionClass
{
    Adult,
    Child
}

/// <summary>When a record's minimum retention period ends, and which class set it.</summary>
public sealed record RetentionDue(RetentionClass Class, DateOnly ConclusionOfTreatment, DateOnly DueOn);

/// <summary>
/// The retention schedule's rules (<see cref="RetentionOptions"/>), worked out for one patient. The conclusion of
/// treatment is the later of the last change to the patient's record and the last change to any of their procedures
/// (all closed by then), as a UK date.
/// </summary>
public static class RetentionSchedule
{
    /// <summary>
    /// Procedure audit actions that record a read, not a change to the record, so they don't move the conclusion of
    /// treatment: downloading a file. Add any other read the audit log records.
    /// </summary>
    public static readonly IReadOnlySet<PatientProcedureAuditAction> ReadOnlyAuditActions =
        new HashSet<PatientProcedureAuditAction> { PatientProcedureAuditAction.AttachmentDownloaded };

    /// <summary>When the patient's record is due for review.</summary>
    public static RetentionDue Due(RetentionCandidate patient, RetentionOptions options)
    {
        // The latest of the record, its procedures, and their audit trail, which outlives a procedure deleted since.
        var lastChange = new[] { patient.LastUpdatedUtc, patient.LastProcedureChangedUtc, patient.LastAuditUtc }
            .OfType<DateTime>()
            .Max();
        return Due(patient.DateOfBirth, DueDates.ToUkDate(lastChange), options);
    }

    /// <summary>When the record of a patient born on <paramref name="dateOfBirth"/>, treated until <paramref name="conclusion"/>, is due for review.</summary>
    public static RetentionDue Due(DateOnly dateOfBirth, DateOnly conclusion, RetentionOptions options)
    {
        var age = AgeOn(dateOfBirth, conclusion);
        if (age >= options.AdultAge)
            return new RetentionDue(RetentionClass.Adult, conclusion, AddYears(conclusion, options.AdultYears));

        var until = age == options.AdultAge - 1 ? options.ChildAgedSeventeenUntilAge : options.ChildUntilAge;
        return new RetentionDue(RetentionClass.Child, conclusion, AddYears(dateOfBirth, until));
    }

    /// <summary>Whole years of age on a date (a 29 February birthday falls on 1 March in other years).</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on < AddYears(dateOfBirth, age))
            age--;
        return age;
    }

    /// <summary>
    /// A date so many years on. In England, someone born on 29 February has their birthday on 1 March in other years,
    /// so that's the day it falls on then (<see cref="DateOnly.AddYears"/> would give 28 February, a day early).
    /// </summary>
    public static DateOnly AddYears(DateOnly date, int years)
    {
        var moved = date.AddYears(years);
        return date is { Month: 2, Day: 29 } && moved.Day == 28 ? moved.AddDays(1) : moved;
    }

    /// <summary>
    /// The shortest any record is kept after the conclusion of treatment, in years, so only patients whose last change
    /// is at least that old need checking. A child's period is shortest just before an age boundary.
    /// </summary>
    public static int ShortestYears(RetentionOptions options) =>
        Math.Max(0, Math.Min(
            options.AdultYears,
            Math.Min(
                options.ChildUntilAge - (options.AdultAge - 1),
                options.ChildAgedSeventeenUntilAge - options.AdultAge)));
}
