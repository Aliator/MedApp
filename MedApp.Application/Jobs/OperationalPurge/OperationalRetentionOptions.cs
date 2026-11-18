namespace MedApp.Application.Jobs.OperationalPurge;

/// <summary>
/// How long operational data is kept before the operational purge deletes it, from the <c>Retention:Operational</c>
/// settings. The defaults are the periods in <c>docs/governance/retention-schedule.md</c> (section 3), proposed for the
/// DPO to confirm; notepads use <c>Notepad:KeepDays</c>. <b>It's a dry run until <see cref="DryRun"/> is turned off:</b>
/// it counts what it would delete, and deletes nothing.
/// </summary>
public sealed class OperationalRetentionOptions
{
    public const string Section = "Retention:Operational";

    /// <summary>Whether the purge only counts (true, the default) or deletes.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Months a session (address, browser, times) is kept after it was created.</summary>
    public int SessionMonths { get; set; } = 12;

    /// <summary>Months a sign-in, sign-out or security event in the activity log is kept.</summary>
    public int SignInEventMonths { get; set; } = 12;

    /// <summary>Months a notification is kept after it was made, read or not.</summary>
    public int NotificationMonths { get; set; } = 6;

    /// <summary>Days a sign-in challenge or emailed code is kept after it expired (it can't be used once expired).</summary>
    public int ExpiredCodeDays { get; set; } = 1;

    /// <summary>Said when a period is less than 1, which stops the API at startup.</summary>
    public const string InvalidPeriods =
        "Retention:Operational:SessionMonths, SignInEventMonths, NotificationMonths and ExpiredCodeDays must each be at least 1.";

    /// <summary>Whether the settings can be used: every period at least 1, so nothing is deleted as it's made.</summary>
    public bool IsValid => SessionMonths >= 1 && SignInEventMonths >= 1 && NotificationMonths >= 1 && ExpiredCodeDays >= 1;
}
