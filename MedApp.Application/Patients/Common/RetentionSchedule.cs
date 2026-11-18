using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// What set a record's minimum period: the adult or child rule, or one of its pathways' team classes
/// (<see cref="RetentionClass"/>) when that's longer. Not stored.
/// </summary>
public enum RetentionBasis
{
    Adult,
    Child,
    Oncology,
    Maternity,
    MentalHealth
}

/// <summary>When a record's minimum retention period ends, and what set it.</summary>
public sealed record RetentionDue(RetentionBasis Class, DateOnly ConclusionOfTreatment, DateOnly DueOn);

/// <summary>
/// The retention schedule's rules (<see cref="RetentionOptions"/>), worked out for one patient. The conclusion of
/// treatment is the latest of the last change to the patient's record, to any of their pathways (all closed by then)
/// and to any of their referrals (none awaiting triage by then), as a UK date.
/// </summary>
public static class RetentionSchedule
{
    /// <summary>
    /// Pathway audit actions that record a read, not a change to the record, so they don't move the conclusion of
    /// treatment: downloading a file. Add any other read the audit log records.
    /// </summary>
    public static readonly IReadOnlySet<PatientPathwayAuditAction> ReadOnlyAuditActions =
        new HashSet<PatientPathwayAuditAction> { PatientPathwayAuditAction.AttachmentDownloaded };

    /// <summary>When the patient's record is due for review.</summary>
    public static RetentionDue Due(RetentionCandidate patient, RetentionOptions options)
    {
        // The latest of the record, its pathways, their audit trail (which outlives a pathway deleted since), and its
        // referrals' history.
        var lastChange = new[]
            {
                patient.LastUpdatedUtc, patient.LastPathwayChangedUtc, patient.LastAuditUtc, patient.LastReferralChangeUtc
            }
            .OfType<DateTime>()
            .Max();
        return Due(patient.DateOfBirth, DueDates.ToUkDate(lastChange), options, patient.RetentionClasses ?? []);
    }

    /// <summary>
    /// When the record of a patient born on <paramref name="dateOfBirth"/>, treated until <paramref name="conclusion"/>,
    /// is due for review: the latest of the adult or child rule and each of <paramref name="classes"/> (their
    /// pathways' classes), counted from the conclusion of treatment. On a tie the adult or child rule names it.
    /// </summary>
    public static RetentionDue Due(
        DateOnly dateOfBirth,
        DateOnly conclusion,
        RetentionOptions options,
        IEnumerable<RetentionClass>? classes = null)
    {
        var due = StandardDue(dateOfBirth, conclusion, options);
        foreach (var retentionClass in (classes ?? []).Distinct().Order())
        {
            if (options.Classes.For(retentionClass) is not { } years)
                continue;

            var dueOn = AddYears(conclusion, years);
            if (dueOn > due.DueOn)
                due = new RetentionDue(BasisOf(retentionClass), conclusion, dueOn);
        }

        return due;
    }

    private static RetentionDue StandardDue(DateOnly dateOfBirth, DateOnly conclusion, RetentionOptions options)
    {
        var age = AgeOn(dateOfBirth, conclusion);
        if (age >= options.AdultAge)
            return new RetentionDue(RetentionBasis.Adult, conclusion, AddYears(conclusion, options.AdultYears));

        var until = age == options.AdultAge - 1 ? options.ChildAgedSeventeenUntilAge : options.ChildUntilAge;
        return new RetentionDue(RetentionBasis.Child, conclusion, AddYears(dateOfBirth, until));
    }

    private static RetentionBasis BasisOf(RetentionClass retentionClass) => retentionClass switch
    {
        RetentionClass.Oncology => RetentionBasis.Oncology,
        RetentionClass.Maternity => RetentionBasis.Maternity,
        _ => RetentionBasis.MentalHealth
    };

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
    /// is at least that old need checking. A child's period is shortest just before an age boundary. A team class
    /// only ever lengthens a record's period, so it doesn't change this.
    /// </summary>
    public static int ShortestYears(RetentionOptions options) =>
        Math.Max(0, Math.Min(
            options.AdultYears,
            Math.Min(
                options.ChildUntilAge - (options.AdultAge - 1),
                options.ChildAgedSeventeenUntilAge - options.AdultAge)));
}
