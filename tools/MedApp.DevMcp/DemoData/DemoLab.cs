using MedApp.Domain.Teams;
using System.Security.Cryptography;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Configurations.Specimens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Saves what the replays took to the lab and the MDT (<see cref="PathwayReplay"/>): specimens, the reports' file
/// records, results, consent records and MDT items, after the pathways they belong to; restarts the specimen number
/// sequence after the numbers used; and adds the one result that needs a clinician (the leaver's, never signed off).
/// The reports' bytes go to the attachment store with the other files (<see cref="UploadReportsAsync"/>), when it answers.
/// </summary>
public static class DemoLab
{
    /// <summary>
    /// A report from another service that the leaver recorded as a result the week before they left, on a patient's
    /// record (no pathway), and never signed off: it shows as needing a clinician, as their account is deleted.
    /// </summary>
    public static void AddLeaversResult(DemoWorld world, IReadOnlyList<PatientSeed> patients)
    {
        var leaver = world.Staff.Single(s => s.Kind == StaffKind.Leaver);
        var at = DemoTimeline.WorkingMoment(leaver.LeftUtc!.Value.AddDays(-9), 11, 20);
        var patient = world.Pathways
            .Where(p => p.Assignments.Any(a => a.UserId == leaver.Id) && PathwayWorkflow.IsClosed(p.Status) && p.CreatedAt < at)
            .Select(p => patients.FirstOrDefault(x => x.Id == p.PatientId))
            .FirstOrDefault(p => p is { Restricted: false, AnonymisedUtc: null });
        if (patient is null)
            return;

        var bytes = SyntheticFiles.Pdf("Ultrasound report from another service (synthetic)",
            ["SYNTHETIC: not a real result.", "Scanned report received by post.", "No values: a demonstration file."]);
        var report = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TeamId = Team.GeneralId,
            FileName = "Ultrasound report (other service).pdf",
            ContentType = "application/pdf",
            SizeBytes = bytes.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ScanStatus = AttachmentScanStatus.NotScanned,
            UploadedByUserId = leaver.Id,
            UploadedByUsername = leaver.Username,
            UploadedAtUtc = at,
        };
        var result = new Result
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            TeamId = Team.GeneralId,
            Source = ResultSource.ExternalReport,
            Title = "Ultrasound report from another service",
            ReportAttachmentId = report.Id,
            ReportedAtUtc = at.AddMinutes(1),
            ReportedByUserId = leaver.Id,
            ResponsibleUserId = leaver.Id,
        };
        var change = new ResultChange(result, null, leaver.Actor, at.AddMinutes(1));
        change.Record(leaver.Username);

        world.Attachments.Add(report);
        world.FileBytes[report.Id] = bytes;
        world.Audit.Add(AttachmentAudit.Entry(report, new AttachmentTarget(patient.Id, null, null), leaver.Actor,
            PatientPathwayAuditAction.AttachmentAdded, at));
        world.Audit.AddRange(change.AuditEntries);
        world.Results.Add(result);
        world.Touches.Add(new Touch(leaver.Id, at, patient.Id, null));
    }

    /// <summary>Saves the lab's, the MDT's and consent's records, after the pathways (their foreign keys).</summary>
    public static async Task SaveAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        // The navigations the rules used in memory would be inserted again: the foreign keys are enough.
        foreach (var specimen in world.Specimens)
        {
            specimen.SpecimenType = null!;
            specimen.Laboratory = null!;
            specimen.Patient = null!;
            specimen.Pathway = null!;
        }

        foreach (var result in world.Results)
        {
            result.Patient = null!;
            result.Pathway = null;
            result.Specimen = null;
        }

        foreach (var item in world.MdtItems)
        {
            item.Pathway = null!;
            item.Team = null!;
            item.Meeting = null;
            item.Patient = null!;
        }

        await Batch(world.Attachments, (db, items) => db.Attachments.AddRange(items), 2000);
        await Batch(world.Specimens, (db, items) => db.Specimens.AddRange(items), 1000);
        await Batch(world.Results, (db, items) => db.Results.AddRange(items), 1000);
        await Batch(world.ConsentRecords, (db, items) => db.ConsentRecords.AddRange(items), 1000);
        await Batch(world.MdtItems, (db, items) => db.MdtItems.AddRange(items), 1000);

        // The sequence hands out numbers after the ones the story used, so a specimen recorded now gets a new one.
        // A sequence's restart value can't be a parameter: the SQL is built from a number and a constant name only.
        var sql = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"ALTER SEQUENCE [{SpecimenConfiguration.NumberSequence}] RESTART WITH {world.NextSpecimenNumber}");
        await services.Run(sp => sp.GetRequiredService<MedAppDbContext>().Database.ExecuteSqlRawAsync(sql, ct));

        async Task Batch<T>(IReadOnlyList<T> all, Action<MedAppDbContext, IEnumerable<T>> add, int size)
        {
            for (var i = 0; i < all.Count; i += size)
            {
                var chunk = all.Skip(i).Take(size).ToList();
                await services.Run(async sp =>
                {
                    var db = sp.GetRequiredService<MedAppDbContext>();
                    db.ChangeTracker.AutoDetectChangesEnabled = false;
                    add(db, chunk);
                    await db.SaveChangesAsync(ct);
                });
            }
        }
    }

    /// <summary>Stores the reports' bytes (the files MedApp made or took in during the story) where the app reads them.</summary>
    public static async Task<int> UploadReportsAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        await using var scope = services.Scope();
        var store = scope.ServiceProvider.GetRequiredService<IFileStore>();
        foreach (var attachment in world.Attachments)
        {
            await using var content = new MemoryStream(world.FileBytes[attachment.Id], writable: false);
            await store.SaveAsync(AttachmentStorageKeys.File(attachment.Id), content, attachment.ContentType, ct);
        }

        return world.Attachments.Count;
    }

    /// <summary>Counts, for the summary.</summary>
    public static object Summarise(DemoWorld world)
    {
        var now = world.Timeline.NowUtc;
        return new
        {
            from = world.Timeline.LabAndLettersFromUtc,
            specimens = world.Specimens.Count,
            specimensByStatus = world.Specimens.GroupBy(s => s.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            specimensOverdueAtLab = world.Specimens.Count(s => SpecimenWorkflow.IsOpen(s.Status)
                && s.TakenAtUtc.AddDays(world.SpecimenSetup.Types.Single(t => t.Id == s.SpecimenTypeId).ExpectedTurnaroundDays) < now),
            results = world.Results.Count,
            resultsByStatus = world.Results.GroupBy(r => r.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            resultsAwaitingOverTwoWeeks = world.Results.Count(r => r.IsAwaitingSignOff && r.ReportedAtUtc < now.AddDays(-14)),
            signedOffActions = world.Results.Where(r => r.Action is not null).GroupBy(r => r.Action!.Value).OrderBy(g => g.Key)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            consentRecords = world.ConsentRecords.Count,
            consentWithdrawn = world.ConsentRecords.Count(c => c.Status == ConsentStatus.Withdrawn),
            mdtItems = world.MdtItems.Count,
            mdtItemsByStatus = world.MdtItems.GroupBy(i => i.Status).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            mdtMeetings = world.MdtMeetings.Count,
            branchesChosen = world.Pathways.SelectMany(p => p.Stages).Count(s => s.ChoiceKind is not null && s.Outcome is not null),
            stagesSkippedByBranch = world.Pathways.SelectMany(p => p.Stages).Count(s => s.SkipReason == StageSkipReason.BranchNotChosen),
            stagesSkippedDeclined = world.Pathways.SelectMany(p => p.Stages).Count(s => s.SkipReason == StageSkipReason.PatientDeclined),
            stagesReinstated = world.Audit.Count(a => a.Action == PatientPathwayAuditAction.StageReinstated),
            didNotAttendDecided = world.Appointments.Count(a => a.DnaFollowUp is not null),
            didNotAttendAwaitingDecision = world.Appointments.Count(a => a.Status == Domain.Appointments.AppointmentStatus.DidNotAttend
                && a.DnaFollowUp is null && a.EndUtc >= world.Timeline.LabAndLettersFromUtc),
        };
    }
}
