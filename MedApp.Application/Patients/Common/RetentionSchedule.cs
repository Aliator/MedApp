using MedApp.Application.Procedures.Common;

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
    /// <summary>When the patient's record is due for review.</summary>
    public static RetentionDue Due(RetentionCandidate patient, RetentionOptions options)
    {
        var lastChange = patient.LastProcedureChangedUtc is { } procedureChange && procedureChange > patient.LastUpdatedUtc
            ? procedureChange
            : patient.LastUpdatedUtc;
        return Due(patient.DateOfBirth, DueDates.ToUkDate(lastChange), options);
    }

    /// <summary>When the record of a patient born on <paramref name="dateOfBirth"/>, treated until <paramref name="conclusion"/>, is due for review.</summary>
    public static RetentionDue Due(DateOnly dateOfBirth, DateOnly conclusion, RetentionOptions options)
    {
        var age = AgeOn(dateOfBirth, conclusion);
        if (age >= options.AdultAge)
            return new RetentionDue(RetentionClass.Adult, conclusion, conclusion.AddYears(options.AdultYears));

        var until = age == options.AdultAge - 1 ? options.ChildAgedSeventeenUntilAge : options.ChildUntilAge;
        return new RetentionDue(RetentionClass.Child, conclusion, dateOfBirth.AddYears(until));
    }

    /// <summary>Whole years of age on a date.</summary>
    public static int AgeOn(DateOnly dateOfBirth, DateOnly on)
    {
        var age = on.Year - dateOfBirth.Year;
        if (on < dateOfBirth.AddYears(age))
            age--;
        return age;
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
