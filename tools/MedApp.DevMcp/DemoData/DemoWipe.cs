using MedApp.Domain.Sites;
using MedApp.Domain.Specialties;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Configurations.Calendar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>Empties the app's data, keeping only the kept accounts and what migrations seed.</summary>
public static class DemoWipe
{
    /// <summary>The "Definition missing" stage, seeded with HasData in its EF configuration and relied on by the app; never deleted.</summary>
    public static readonly Guid MissingDefinitionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Deletes every patient, pathway (with its notes and their versions), appointment, meeting, calendar setup (but the seeded activity types), closed day, attached file's
    /// record (not its bytes), audit and activity entry, session, notification, rule and choice, the admins' sign-in
    /// rules (back to the defaults: none), and every account but the kept ones (with their settings and sign-in
    /// challenges). Returns the usernames removed.
    /// </summary>
    public static async Task<List<string>> RunAsync(SeedServices services, IReadOnlySet<string> keep, CancellationToken ct)
    {
        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            var keepNames = keep.ToList();
            var kept = await db.Users.Where(u => keepNames.Contains(u.UserName!)).Select(u => u.Id).ToListAsync(ct);

            // The DbContext retries transient failures, so EF only allows a transaction of our own inside its execution
            // strategy, which retries the whole transaction. The deletes track nothing, so a retry just runs them again.
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async cancellationToken =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                // Children before parents: referral → patient/specialty/pathway, appointment → patient/pathway/stage,
                // pathway → patient and stage/map → definition don't cascade; nor do calendar blocks and templates →
                // activity type and day template.
                await db.ReferralEvents.ExecuteDeleteAsync(cancellationToken);
                await db.ReferralClockPauses.ExecuteDeleteAsync(cancellationToken);
                await db.Referrals.ExecuteDeleteAsync(cancellationToken);
                // Texts and emails to patients (no foreign keys) go with their appointments.
                await db.PatientMessages.ExecuteDeleteAsync(cancellationToken);
                await db.AppointmentEvents.ExecuteDeleteAsync(cancellationToken);
                await db.Appointments.ExecuteDeleteAsync(cancellationToken);
                // Clinic sessions (after the appointments booked into them), then the patterns that made them; both
                // name rooms and specialties, which go below.
                await db.ClinicSessions.ExecuteDeleteAsync(cancellationToken);
                await db.ClinicPatterns.ExecuteDeleteAsync(cancellationToken);
                // MDT items (who was present cascades) before the meetings, pathways and specialties they name.
                await db.MdtItems.ExecuteDeleteAsync(cancellationToken);
                await db.MeetingAttendees.ExecuteDeleteAsync(cancellationToken);
                await db.Meetings.ExecuteDeleteAsync(cancellationToken);
                await db.CalendarBlockExceptions.ExecuteDeleteAsync(cancellationToken);
                await db.CalendarBlocks.ExecuteDeleteAsync(cancellationToken);
                await db.CalendarDayOverrides.ExecuteDeleteAsync(cancellationToken);
                await db.ClosedDays.ExecuteDeleteAsync(cancellationToken);
                // Rooms and every site but the main one, which goes back to how the migration made it.
                await db.Rooms.ExecuteDeleteAsync(cancellationToken);
                await db.Sites.Where(x => x.Id != Site.MainSiteId).ExecuteDeleteAsync(cancellationToken);
                await db.Sites.Where(x => x.Id == Site.MainSiteId).ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.Name, Site.MainSiteName)
                    .SetProperty(p => p.AddressLines, (string?)null)
                    .SetProperty(p => p.Postcode, (string?)null)
                    .SetProperty(p => p.Phone, (string?)null)
                    .SetProperty(p => p.IsArchived, false), cancellationToken);
                await db.StaffWeekTemplates.ExecuteDeleteAsync(cancellationToken);
                await db.WeekTemplateDays.ExecuteDeleteAsync(cancellationToken);
                await db.WeekTemplates.ExecuteDeleteAsync(cancellationToken);
                await db.DayTemplateBlocks.ExecuteDeleteAsync(cancellationToken);
                await db.DayTemplates.ExecuteDeleteAsync(cancellationToken);
                var seededActivityTypes = CalendarActivityTypeConfiguration.Seeded.Select(t => t.Id).ToList();
                await db.CalendarActivityTypes.Where(t => !seededActivityTypes.Contains(t.Id)).ExecuteDeleteAsync(cancellationToken);
                // Attached files' records (no foreign keys) go with their patients. Their bytes stay in Azurite,
                // unreferenced, so the pre-reset snapshot still finds them if it's restored.
                await db.Attachments.ExecuteDeleteAsync(cancellationToken);
                await db.PatientPathwayAuditEntries.ExecuteDeleteAsync(cancellationToken);
                // Note versions have no foreign keys; the notes themselves go with their pathways (cascade).
                await db.PatientPathwayNoteVersions.ExecuteDeleteAsync(cancellationToken);
                // So do requests to delete notes.
                await db.PathwayNoteDeletionRequests.ExecuteDeleteAsync(cancellationToken);
                // The activity log is about the patients and accounts being wiped, so it goes with them.
                await db.ActivityEntries.ExecuteDeleteAsync(cancellationToken);
                await db.PatientPathwayAssignments.ExecuteDeleteAsync(cancellationToken);
                await db.PatientPathwayStages.ExecuteDeleteAsync(cancellationToken);
                await db.PatientPathways.ExecuteDeleteAsync(cancellationToken);
                await db.PathwayTemplateItems.ExecuteDeleteAsync(cancellationToken);
                await db.PathwayTemplates.ExecuteDeleteAsync(cancellationToken);
                // Letter templates point at specialties, so they go first.
                await db.LetterTemplates.ExecuteDeleteAsync(cancellationToken);
                // Every specialty but General (which the migrations seeded), back as it was; memberships go first.
                await db.StaffSpecialties.ExecuteDeleteAsync(cancellationToken);
                await db.Specialties.Where(s => s.Id != Specialty.GeneralId).ExecuteDeleteAsync(cancellationToken);
                await db.Specialties.Where(s => s.Id == Specialty.GeneralId).ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.Name, Specialty.GeneralName)
                    .SetProperty(s => s.IsArchived, false), cancellationToken);
                await db.PatientPathwayStageDefinitions.Where(d => d.Id != MissingDefinitionId).ExecuteDeleteAsync(cancellationToken);
                await db.Patients.ExecuteDeleteAsync(cancellationToken);
                // The GP practice directory, after the patients and referrals that point at it.
                await db.Practices.ExecuteDeleteAsync(cancellationToken);
                await db.UserSessions.ExecuteDeleteAsync(cancellationToken);
                // Rules, choices and notifications name the accounts, templates and stages being wiped.
                await db.MonitoringRules.ExecuteDeleteAsync(cancellationToken);
                await db.NotificationPreferences.ExecuteDeleteAsync(cancellationToken);
                await db.Notifications.ExecuteDeleteAsync(cancellationToken);
                // Settings and sign-in challenges have no foreign key to their account: those of accounts going, go.
                await db.UserSettings.Where(s => !kept.Contains(s.UserId)).ExecuteDeleteAsync(cancellationToken);
                await db.SignInChallenges.Where(c => !kept.Contains(c.UserId)).ExecuteDeleteAsync(cancellationToken);
                // The admins' sign-in rules go back to a new database's: no row, so every rule is off (SecuritySettings).
                // Kept, a rule such as "require two-factor for admins" would hold admin (who has none) at sign-in.
                await db.SecuritySettings.ExecuteDeleteAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }, ct);
        });

        return await services.Run(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var removed = new List<string>();
            foreach (var user in await users.Users.ToListAsync(ct))
            {
                if (keep.Contains(user.UserName!))
                    continue;
                Guards.Check(await users.DeleteAsync(user), $"Delete user {user.UserName}");
                removed.Add(user.UserName!);
            }

            return removed;
        });
    }
}
