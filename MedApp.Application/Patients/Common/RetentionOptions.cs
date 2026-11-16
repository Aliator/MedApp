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
}
