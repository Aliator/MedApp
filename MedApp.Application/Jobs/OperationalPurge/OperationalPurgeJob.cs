using System.Globalization;
using MedApp.Application.Common.Security;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Notepads.Common;
using MedApp.Domain.Activity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Jobs.OperationalPurge;

/// <summary>
/// The operational purge (<c>docs/governance/retention-schedule.md</c>, section 3): deletes sessions, the activity
/// log's sign-in and security events, notifications, notepads left unchanged, and expired sign-in challenges and
/// emailed codes, at the periods in <see cref="OperationalRetentionOptions"/>. <b>Patient records are never deleted
/// here,</b> nor anything the schedule doesn't name for it: the activity log's entries about patients and its account
/// and role changes, calendars, meetings and deleted files' contents all stay. It logs counts only. A dry run (the
/// default) counts, and deletes nothing.
/// </summary>
public sealed class OperationalPurgeJob(
    IOperationalDataRepository repository,
    IOptions<OperationalRetentionOptions> options,
    IOptions<NotepadOptions> notepadOptions,
    ILogger<OperationalPurgeJob> logger) : IScheduledJob
{
    public const string JobName = "operational-purge";

    /// <summary>
    /// The activity log's sign-in and security events: signing in (each step, and refusals), signing out (and signing
    /// someone out everywhere), and every security event (<see cref="ISecurityEventLog"/>: two-factor, password reset
    /// links, email addresses), but those that are account and role changes (an admin given by the recovery command is
    /// recorded as <see cref="ActivityAction.RolesChanged"/>). Account and role changes, and every entry about a patient,
    /// aren't among them: no period is set for them.
    /// </summary>
    public static readonly IReadOnlyList<ActivityAction> SignInAndSecurityActions =
    [
        ActivityAction.SignedIn,
        ActivityAction.SignInFailed,
        ActivityAction.TwoFactorChallenged,
        ActivityAction.SignedOut,
        ActivityAction.SignedOutEverywhere,
        .. Enum.GetValues<SecurityEventKind>().Select(ActivitySecurityEventLog.ActionFor).Except(AccountAndRoleChanges).Distinct(),
    ];

    /// <summary>The activity log's account and role changes, kept whatever logged them.</summary>
    private static ActivityAction[] AccountAndRoleChanges =>
    [
        ActivityAction.AccountCreated, ActivityAction.AccountDeleted, ActivityAction.PasswordReset,
        ActivityAction.PasswordChanged, ActivityAction.RolesChanged, ActivityAction.RoleCreated, ActivityAction.RoleDeleted,
        ActivityAction.RoleChanged,
    ];

    public string Name => JobName;

    public string Title => "Operational data purge";

    public string Description
    {
        get
        {
            var settings = options.Value;
            var what = string.Create(
                CultureInfo.InvariantCulture,
                $"Deletes sessions after {settings.SessionMonths} months, sign-in and security events after {settings.SignInEventMonths} months, notifications after {settings.NotificationMonths} months, notepads unchanged for {notepadOptions.Value.KeepDays} days, and expired sign-in codes. Never patient records.");
            return settings.DryRun
                ? "Dry run (Retention:Operational:DryRun): counts what it would delete, and deletes nothing. " + what
                : what;
        }
    }

    /// <summary>Early in the morning, UK time, after the clock changes' hours.</summary>
    public JobSchedule Schedule { get; } = JobSchedule.DailyAt(new TimeOnly(4, 30));

    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var dryRun = settings.DryRun;
        var codesCutoff = nowUtc.AddDays(-settings.ExpiredCodeDays);

        var sessions = await repository.PurgeSessionsAsync(nowUtc.AddMonths(-settings.SessionMonths), dryRun, cancellationToken);
        var signInEvents = await repository.PurgeActivityAsync(
            SignInAndSecurityActions, nowUtc.AddMonths(-settings.SignInEventMonths), dryRun, cancellationToken);
        var notifications = await repository.PurgeNotificationsAsync(
            nowUtc.AddMonths(-settings.NotificationMonths), dryRun, cancellationToken);
        var notepads = await repository.PurgeNotepadsAsync(
            nowUtc.AddDays(-notepadOptions.Value.KeepDays), dryRun, cancellationToken);
        var challenges = await repository.PurgeSignInChallengesAsync(codesCutoff, dryRun, cancellationToken);
        var codes = await repository.PurgeEmailCodesAsync(codesCutoff, dryRun, cancellationToken);

        logger.LogInformation(
            "Operational purge ({Mode}): {Sessions} sessions, {SignInEvents} sign-in and security events, {Notifications} notifications, {Notepads} notepads, {Challenges} sign-in challenges, {Codes} emailed codes.",
            dryRun ? "dry run: would delete, deleted nothing" : "deleted",
            sessions, signInEvents, notifications, notepads, challenges, codes);

        return sessions + signInEvents + notifications + notepads + challenges + codes;
    }
}
