using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// The minimum retention periods for patient records, from the <c>Retention</c> section of the API's configuration.
/// The defaults are the NHS Records Management Code of Practice's (<c>docs/governance/retention-schedule.md</c>):
/// adults 8 years after the conclusion of treatment; children until their 25th birthday, or their 26th if they were
/// 17 at the conclusion of treatment. Nothing is deleted automatically: records past their period are listed for an
/// admin to review.
/// </summary>
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    /// <summary>Years an adult's record is kept after the conclusion of treatment.</summary>
    public int AdultYears { get; set; } = 8;

    /// <summary>The age from which a patient counts as an adult at the conclusion of treatment.</summary>
    public int AdultAge { get; set; } = 18;

    /// <summary>The birthday until which a child's record is kept.</summary>
    public int ChildUntilAge { get; set; } = 25;

    /// <summary>The birthday until which the record is kept of a child who was one year short of adult (17) at the conclusion of treatment.</summary>
    public int ChildAgedSeventeenUntilAge { get; set; } = 26;

    /// <summary>How far ahead an admin can set the next review when keeping a record past its period.</summary>
    public int MaxKeepYears { get; set; } = 2;

    /// <summary>
    /// The longer periods of the team classes a pathway template can have (<see cref="RetentionClass"/>), in years
    /// after the conclusion of treatment. Proposed defaults, for the DPO to check against the Records Management Code of
    /// Practice before use.
    /// </summary>
    public RetentionClassYears Classes { get; set; } = new();

    /// <summary>Whether every class's period is at least a year (checked at startup).</summary>
    public bool IsValid => Classes.Oncology >= 1 && Classes.Maternity >= 1 && Classes.MentalHealth >= 1;

    public const string InvalidPeriods = "Each of Retention:Classes (Oncology, Maternity, MentalHealth) must be at least 1 year.";
}

/// <summary>The years kept after the conclusion of treatment for each team class (<c>Retention:Classes</c>).</summary>
public sealed class RetentionClassYears
{
    public int Oncology { get; set; } = 30;

    public int Maternity { get; set; } = 25;

    public int MentalHealth { get; set; } = 20;

    /// <summary>
    /// The classes whose period is shorter than <paramref name="retentionClass"/>'s (Standard, which has no years of its
    /// own, counting as shortest): the existing pathways a template's change to it may raise. Empty for Standard, so a
    /// class is never lowered on existing pathways.
    /// </summary>
    public IReadOnlyList<RetentionClass> ShorterThan(RetentionClass retentionClass) =>
        For(retentionClass) is not { } years
            ? []
            : Enum.GetValues<RetentionClass>().Where(c => (For(c) ?? 0) < years).ToList();

    /// <summary>A class's years; null for Standard, which has none of its own.</summary>
    public int? For(RetentionClass retentionClass) => retentionClass switch
    {
        RetentionClass.Oncology => Oncology,
        RetentionClass.Maternity => Maternity,
        RetentionClass.MentalHealth => MentalHealth,
        _ => null
    };
}
