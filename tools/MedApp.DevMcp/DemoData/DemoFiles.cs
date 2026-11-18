using Azure.Storage.Blobs;
using MediatR;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Attachments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Attached files, uploaded through the app's own command (its type sniffing, SHA-256, thumbnails, scan state, audit
/// entry and record), by the staff working on each record, at moments in the record's story: referral letters on
/// patients; histology reports, blood results, clinic and discharge letters, a lesion's site map (the site its notes
/// name) and a flow test's trace on pathway records; and a home programme and a phone note on stages under way (files go on a stage only
/// while it can be edited). Two are deleted, one by its uploader and one
/// by an admin. Some are listed and downloaded, as the activity log and audit record. Only when the attachment store
/// (the AppHost's Azurite) answers; otherwise skipped, and the summary says so.
/// </summary>
public static class DemoFiles
{
    /// <summary>Whether files can be stored: a local attachment store is configured and answers. Null if so; else why not.</summary>
    public static async Task<string?> WhyNotAvailableAsync(IConfiguration configuration, CancellationToken ct)
    {
        var connection = configuration.GetConnectionString(AttachmentStorageOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connection))
            return "No attachment store is configured (ConnectionStrings:Attachments), so no files were added.";

        try
        {
            var client = new BlobServiceClient(AttachmentStorageOptions.WithoutContainerName(connection),
                new BlobClientOptions { Retry = { MaxRetries = 0, NetworkTimeout = TimeSpan.FromSeconds(3) } });
            if (!client.Uri.IsLoopback)
                return $"The attachment store isn't local ({client.Uri.Host}), so no files were added.";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await client.GetPropertiesAsync(timeout.Token);
            return null;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            return $"The attachment store didn't answer ({e.GetType().Name}: {e.Message.Split('\n')[0]}); is the AppHost's Azurite running? No files were added.";
        }
    }

    public static async Task<object> CreateAsync(SeedServices services, DemoWorld world, IReadOnlyList<PatientSeed> patients, CancellationToken ct)
    {
        var random = world.Random;
        var now = world.Timeline.NowUtc;
        var recent = world.Timeline.ActivityFromUtc;
        var uploaded = new List<(Guid Id, StaffMember By, Guid PatientId, DateTime At)>();
        var deleted = 0;

        async Task<Guid?> Upload(StaffMember by, DateTime at, string name, byte[] bytes, Guid? patientId = null, Guid? pathwayId = null, Guid? stageId = null)
        {
            if (at >= now)
                at = now.AddMinutes(-random.Next(5, 60));
            services.At(at, by.ActivityActor);
            await using var content = new MemoryStream(bytes, writable: false);
            var response = await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
                new UploadAttachmentCommand(patientId, pathwayId, stageId, null, name, bytes.Length, content, by.Actor, by.IsAdminAt(at)), ct)));
            var patient = patientId ?? world.Pathways.Single(p => p.Id == pathwayId || p.Stages.Any(s => s.Id == stageId)).PatientId;
            uploaded.Add((response.Id, by, patient, at));
            return response.Id;
        }

        // Referral letters on patients registered in the last two months.
        var referred = patients.Where(p => p.Role == PatientRole.Active && !p.Restricted && p.CreatedUtc >= recent).OrderBy(p => p.CreatedUtc).Take(10).ToList();
        foreach (var patient in referred)
            await Upload(world.Coordinator, patient.CreatedUtc.AddMinutes(random.Next(5, 90)), "Referral letter.pdf",
                SyntheticFiles.Pdf("Referral letter (synthetic)", ["From: the GP", "Reason for referral: see the pathway record.", "Please see and advise."]),
                patientId: patient.Id);

        // On pathway records that are open or completed (files can't be added to cancelled or archived ones).
        var restricted = patients.Where(p => p.Restricted).Select(p => p.Id).ToHashSet();
        var takesFiles = world.Pathways.Where(p => !restricted.Contains(p.PatientId) && p.TemplateId is not null && p.Status is not (Status.Cancelled or Status.Archived) && p.CreatedAt >= now.AddDays(-150)).ToList();
        PatientPathway? Pick(Func<PatientPathway, bool> where) =>
            takesFiles.Where(where).OrderBy(_ => random.Next()).FirstOrDefault();
        DateTime When(PatientPathway p) => Max(p.CreatedAt.AddDays(random.Next(1, 20)), recent.AddDays(random.Next(0, 50)));
        bool Finished(PatientPathway p, string stage) => p.Stages.Any(s => s.Name == stage && s.IsCompleted);
        bool IsSkinLesion(PatientPathway p) => p.TemplateId == world.TemplateIds[PathwayKind.SkinLesion] || p.TemplateId == world.TemplateIds[PathwayKind.SkinLesion2024];

        // The lab's reports, by the biomedical scientist.
        foreach (var _ in Enumerable.Range(0, 6))
        {
            var p = Pick(x => Finished(x, DemoPathwayKinds.Histology));
            if (p is null)
                continue;
            await Upload(Worker(world, p, DemoPathwayKinds.Histology), When(p), "Histology report.pdf",
                SyntheticFiles.Pdf("Histology report (synthetic)",
                    [$"Specimen for: {p.Title}", $"Lab number: S{random.Next(10000, 99999)}", "Description and conclusion: as recorded in the stage notes.",
                     "Authorised by the biomedical scientist."]),
                pathwayId: p.Id);
        }

        foreach (var _ in Enumerable.Range(0, 4))
        {
            var p = Pick(x => Finished(x, DemoPathwayKinds.LabTests));
            if (p is null)
                continue;
            await Upload(Worker(world, p, DemoPathwayKinds.LabTests), When(p), "Blood and urine results.pdf",
                SyntheticFiles.Pdf("Blood and urine results (synthetic)",
                    [$"Sodium: {random.Next(136, 144)} mmol/L", $"Creatinine: {random.Next(55, 98)} micromol/L", $"Cholesterol: {random.Next(3, 6)}.{random.Next(0, 9)} mmol/L",
                     "Urine: no infection", "The tests asked for: as recorded in the stage notes."]),
                pathwayId: p.Id);
        }

        // Pictures (with thumbnails): where a lesion is, sketched at the first visit; and a flow test's trace.
        foreach (var _ in Enumerable.Range(0, 4))
        {
            var p = Pick(x => IsSkinLesion(x) && Finished(x, DemoPathwayKinds.FirstClinicVisit) && world.Sites.ContainsKey(x.Id));
            if (p is null)
                continue;
            await Upload(Worker(world, p, DemoPathwayKinds.FirstClinicVisit), When(p), "Lesion site map.png",
                SyntheticFiles.SiteMapPng(world.Sites[p.Id]), pathwayId: p.Id);
        }

        foreach (var _ in Enumerable.Range(0, 3))
        {
            var p = Pick(x => x.TemplateId == world.TemplateIds[PathwayKind.Prostate] && Finished(x, DemoPathwayKinds.FirstClinicVisit));
            if (p is null)
                continue;
            await Upload(Worker(world, p, DemoPathwayKinds.FirstClinicVisit), When(p), "Flow test.png", SyntheticFiles.FlowTracePng(random.Next(7, 19), random), pathwayId: p.Id);
        }

        // Clinic letters after a visit, and discharge letters at the end.
        foreach (var _ in Enumerable.Range(0, 5))
        {
            var visit = random.NextDouble() < 0.7 ? DemoPathwayKinds.FirstClinicVisit : DemoPathwayKinds.ClinicReview;
            var p = Pick(x => Finished(x, visit));
            if (p is null)
                continue;
            await Upload(Worker(world, p, visit), When(p), "Clinic letter.pdf",
                SyntheticFiles.Pdf("Clinic letter (synthetic copy)", [$"Seen about: {p.Title}", "Findings and plan: as recorded in the stage notes.", "Copy to the GP and the patient."]),
                pathwayId: p.Id);
        }

        foreach (var _ in Enumerable.Range(0, 4))
        {
            var p = Pick(x => x.Status == Status.Completed || x.Status == Status.InReview);
            if (p is null)
                continue;
            await Upload(Worker(world, p, p.Stages.OrderBy(s => s.StageOrder).Last().Name), When(p), "Discharge letter to GP.pdf",
                SyntheticFiles.Pdf("Discharge letter (synthetic copy)", [$"Seen about: {p.Title}", "Outcome: as in the record.", "For the GP: as in the plan.", "Copy to the patient."]),
                pathwayId: p.Id);
        }

        // On stages under way: a home programme for a biomechanics session, and a phone note.
        var sessions = world.Pathways.Where(p => !restricted.Contains(p.PatientId) && PathwayWorkflow.IsOpen(p.Status)
                                                   && PathwayWorkflow.CurrentStage(p) is { Name: DemoPathwayKinds.BiomechanicsSession, StartedAtUtc: not null }).Take(2).ToList();
        foreach (var p in sessions)
        {
            var stage = PathwayWorkflow.CurrentStage(p)!;
            await Upload(world.Member(DemoStaff.PodiatristUsername), Max(stage.StartedAtUtc!.Value.AddMinutes(20), recent), "Home programme.png",
                SyntheticFiles.DiagramPng("Home programme", ["Stretch", "Strength", "Balance", "Walking"]), stageId: stage.Id);
        }

        var underWay = world.Pathways.Where(p => !restricted.Contains(p.PatientId) && PathwayWorkflow.IsOpen(p.Status) && p.Status != Status.Unassigned
                                                   && PathwayWorkflow.CurrentStage(p) is { StartedAtUtc: not null } s && s.Name != DemoPathwayKinds.BiomechanicsSession)
            .OrderBy(_ => random.Next()).Take(3).ToList();
        foreach (var p in underWay)
        {
            var stage = PathwayWorkflow.CurrentStage(p)!;
            await Upload(Worker(world, p, stage.Name), Max(stage.StartedAtUtc!.Value.AddMinutes(15), recent), "Phone note.txt",
                SyntheticFiles.Text("Telephone note (synthetic)", ["Called the patient to confirm the plan.", "No new concerns.", "Next step as in the record."]),
                stageId: stage.Id);
        }

        // Two deleted: one attached to the wrong record by its uploader, one removed by an admin.
        if (referred.Count > 2)
        {
            var wrong = await Upload(world.Coordinator, referred[1].CreatedUtc.AddMinutes(100), "Referral letter (duplicate).pdf",
                SyntheticFiles.Pdf("Referral letter (synthetic duplicate)", ["Duplicate scan."]), patientId: referred[1].Id);
            services.At(referred[1].CreatedUtc.AddMinutes(104), world.Coordinator.ActivityActor);
            await services.Run(sp => sp.GetRequiredService<IMediator>().Send(new DeleteAttachmentCommand(wrong!.Value, world.Coordinator.Actor, false), ct));
            deleted++;

            var extra = await Upload(world.Coordinator, referred[2].CreatedUtc.AddMinutes(60), "Scanned letter.pdf",
                SyntheticFiles.Pdf("Scanned letter (synthetic)", ["Belongs to another record."]), patientId: referred[2].Id);
            var deleteAt = Max(referred[2].CreatedUtc.AddDays(2), recent);
            services.At(deleteAt, world.Lead.ActivityActor);
            await services.Run(sp => sp.GetRequiredService<IMediator>().Send(new DeleteAttachmentCommand(extra!.Value, world.Lead.Actor, true), ct));
            deleted++;
        }

        // Some files looked at and downloaded by the staff working on them, as the activity log and audit record.
        var downloads = 0;
        foreach (var file in uploaded.Where(u => u.At < now.AddHours(-2)).OrderBy(_ => random.Next()).Take(8))
        {
            var by = random.NextDouble() < 0.6 ? file.By : world.Lead;
            var at = file.At.AddHours(1 + random.NextDouble() * 48);
            if (at >= now)
                continue;
            services.At(at, by.ActivityActor);
            var ok = await services.Run(async sp =>
            {
                var mediator = sp.GetRequiredService<IMediator>();
                await mediator.Send(new GetAttachmentsQuery(
                    file.PatientId, null, null, null, false, by.Id, by.IsAdminAt(at), by.PermissionsAt(at).Contains(Permission.AttachFiles)), ct);
                try
                {
                    var download = await mediator.Send(new DownloadAttachmentCommand(file.Id, by.Actor), ct);
                    await download.Content.DisposeAsync();
                    return true;
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    return false; // a deleted file can't be downloaded
                }
            });
            if (ok)
                downloads++;
        }

        return new { uploaded = uploaded.Count, deleted, downloaded = downloads };
    }

    /// <summary>Who worked on a stage of a pathway (who started it), else its first assignee, else the coordinator.</summary>
    private static StaffMember Worker(DemoWorld world, PatientPathway pathway, string stageName)
    {
        var started = pathway.Stages.FirstOrDefault(s => s.Name == stageName && s.StartedByUserId is not null)?.StartedByUserId
                      ?? pathway.Assignments.FirstOrDefault()?.UserId;
        return world.Staff.FirstOrDefault(s => s.Id == started && s.LeftUtc is null) ?? world.Coordinator;
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
