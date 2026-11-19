using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Common.Exceptions;
using ModelContextProtocol;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using MedApp.Application.Consent.Commands.RecordConsent;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;
using MedApp.Application.Specimens.Commands.MarkSpecimenReceived;
using MedApp.Application.Specimens.Commands.MarkSpecimenSent;
using MedApp.Application.Specimens.Commands.RecordSpecimen;
using MedApp.Contracts.Letters.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedApp.DevMcp.DemoData;

/// <summary>A pathway the story is replaying: its ID, patient, lead, and the app's latest answer about it.</summary>
public sealed class StoryPathway(Guid id, Guid patientId, DemoPerson lead, PatientPathwayResponse latest)
{
    public Guid Id { get; } = id;
    public Guid PatientId { get; } = patientId;
    public DemoPerson Lead { get; } = lead;
    public PatientPathwayResponse Latest { get; set; } = latest;

    /// <summary>The pathway's stage of this name: the template's, or (<paramref name="branch"/>) an added branch's.</summary>
    public PatientPathwayStageResponse Stage(string name, bool branch = false) =>
        Latest.Stages
            .Where(s => s.Name == name && (s.BranchAdditionId is not null) == branch)
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"The pathway '{Latest.Title}' has no {(branch ? "added " : string.Empty)}stage '{name}'.");
}

/// <summary>A step of a lab case's work: Accessioned, a test step by name, or a choice's outcome.</summary>
public sealed record LabWork(string? Step = null, string? Choice = null, string? Outcome = null, string? Detail = null, bool Accessioned = false)
{
    public static readonly LabWork Accession = new(Accessioned: true);
    public static readonly LabWork Adequate = new(Choice: "SampleAdequate", Outcome: "Adequate");
    public static readonly LabWork NoMoreWork = new(Choice: "NeedsMoreWork", Outcome: "NoMoreWork");

    public static LabWork TestStep(string name, string? detail = null) => new(Step: name, Detail: detail);
}

/// <summary>
/// The story's verbs: each replays one action through the app's own handler, as someone at a moment (the light demo's
/// seed writes the same records as data; here the app makes them, with their audit entries, notifications, grants and
/// gates).
/// </summary>
public static class DemoReplay
{
    /// <summary>A pathway made from a template by its lead (no referral), then assigned to them by the service manager.</summary>
    public static async Task<StoryPathway> CreateAsync(
        DemoSeed seed, string patient, string template, string title, int dueIn, string priority, DateTime at, DemoPerson lead, bool assign = true)
    {
        var patientId = seed.Patients[patient];
        var created = await seed.Send(at, lead, new CreatePatientPathwayCommand(
            patientId, seed.Templates[template], title, string.Empty, seed.Timeline.DaysFrom(dueIn), priority, lead.Actor), $"start {patient}'s pathway");
        var pathway = new StoryPathway(created.Id, patientId, lead, created);
        if (assign)
            await AssignAsync(seed, pathway, at.AddMinutes(5));
        return pathway;
    }

    /// <summary>The service manager assigns the pathway's lead to it.</summary>
    public static async Task AssignAsync(DemoSeed seed, StoryPathway pathway, DateTime at) =>
        pathway.Latest = await seed.Send(at, DemoPeople.Whitlock,
            new AssignPatientPathwayCommand(pathway.Id, pathway.Lead.Id, DemoPeople.Whitlock.Actor), $"assign {pathway.Latest.Title}");

    public static async Task StartAsync(DemoSeed seed, StoryPathway pathway, string stage, DateTime at, DemoPerson? by = null, bool branch = false)
    {
        var who = by ?? pathway.Lead;
        pathway.Latest = await seed.Send(at, who,
            new StartPathwayStageCommand(pathway.Id, pathway.Stage(stage, branch).Id, who.Actor), $"start '{stage}' on {pathway.Latest.Title}");
    }

    public static async Task FinishAsync(DemoSeed seed, StoryPathway pathway, string stage, DateTime at, string? outcome = null, DemoPerson? by = null, bool branch = false)
    {
        var who = by ?? pathway.Lead;
        pathway.Latest = await seed.Send(at, who,
            new FinishPathwayStageCommand(pathway.Id, pathway.Stage(stage, branch).Id, who.Actor, Outcome: outcome), $"finish '{stage}' on {pathway.Latest.Title}");
    }

    /// <summary>Starts and finishes a stage that needs no visit (a triage on the pathway's lead's desk).</summary>
    public static async Task DoAsync(DemoSeed seed, StoryPathway pathway, string stage, DateTime at, int minutes = 15)
    {
        await StartAsync(seed, pathway, stage, at);
        await FinishAsync(seed, pathway, stage, at.AddMinutes(minutes));
    }

    /// <summary>
    /// A visit for a stage: the booking coordinator books the visit the stage asked for (or books one, if it asked for
    /// none), with <paramref name="staff"/> in a room; then, unless it's still ahead, the clinician records it attended.
    /// </summary>
    public static async Task<Guid> VisitAsync(
        DemoSeed seed, StoryPathway pathway, string stage, DemoPerson staff, DateTime start, int minutes, string room, DateTime bookedAt,
        bool branch = false, bool attended = true)
    {
        var stageId = pathway.Stage(stage, branch).Id;
        var end = start.AddMinutes(minutes);
        var booker = DemoPeople.Marsh;
        var requested = await seed.Db(db => db.Appointments
            .Where(a => a.StageId == stageId && a.Status == AppointmentStatus.Requested)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(seed.Ct));
        // The room asked for, or the centre's other room if another clinician has it then.
        Contracts.Appointments.Responses.AppointmentResponse? booked = null;
        foreach (var place in new[] { room }.Concat(new[] { DemoSetup.ClinicRoom1, DemoSetup.ProcedureRoom }.Where(r => r != room)))
        {
            try
            {
                booked = requested is { } appointmentId
                    ? await seed.Send(bookedAt, booker, new BookAppointmentCommand(appointmentId, staff.Id, start, end, nameof(AppointmentStatus.Requested), booker.Actor,
                        RoomId: seed.Rooms[place]), $"book the '{stage}' visit on {pathway.Latest.Title}")
                    : await seed.Send(bookedAt, booker, new CreateAppointmentCommand(pathway.PatientId, pathway.Id, stageId, staff.Id, start, end, null, booker.Actor,
                        RoomId: seed.Rooms[place]), $"book a '{stage}' visit on {pathway.Latest.Title}");
                break;
            }
            catch (McpException e) when (e.InnerException is ConflictException && e.Message.Contains(" is taken ", StringComparison.Ordinal))
            {
                // Try the other room.
            }
        }

        if (booked is null)
            throw new McpException($"The demo data couldn't book the '{stage}' visit on {pathway.Latest.Title}: no room is free then.");
        if (attended)
            await seed.Send(end.AddMinutes(1), staff, new RecordAppointmentOutcomeCommand(booked.Id, nameof(AppointmentStatus.Attended), staff.Actor),
                $"record the '{stage}' visit attended on {pathway.Latest.Title}");
        return booked.Id;
    }

    public static Task NoteAsync(DemoSeed seed, StoryPathway pathway, DemoPerson by, DateTime at, string text) =>
        seed.Send(at, by, new AddPathwayNoteCommand(pathway.Id, text, by.Actor), $"add a note to {pathway.Latest.Title}");

    /// <summary>The patient's consent to a stage, as the clinician recorded it (MedApp never assesses it).</summary>
    public static async Task<Guid> ConsentAsync(DemoSeed seed, StoryPathway pathway, string stage, string form, DateTime at, DemoPerson? by = null, bool branch = false)
    {
        var who = by ?? pathway.Lead;
        var record = await seed.Send(at, who, new RecordConsentCommand(pathway.Id, pathway.Stage(stage, branch).Id, seed.ConsentForms[form],
            Application.Calendar.Common.UkTime.DateOf(at), "Patient", null, who.Actor), $"record consent to '{stage}' on {pathway.Latest.Title}");
        return record.Id;
    }

    /// <summary>A sample taken at a stage, for a test from the lab's catalogue, recorded by whoever took it.</summary>
    public static Task<SpecimenResponse> SampleAsync(
        DemoSeed seed, StoryPathway pathway, string stage, string type, string test, DateTime takenAt, string? site, string? notes,
        DemoPerson? by = null, bool branch = false)
    {
        var who = by ?? pathway.Lead;
        return seed.Send(takenAt, who, new RecordSpecimenCommand(pathway.Id, pathway.Stage(stage, branch).Id, seed.SpecimenTypes[type], seed.LabTests[test],
            takenAt, site, notes, who.Id, who.Actor), $"record a sample on {pathway.Latest.Title}");
    }

    public static async Task SendSampleAsync(DemoSeed seed, Guid specimenId, DateTime at, DemoPerson by, string? courier = null)
    {
        var version = await SpecimenVersionAsync(seed, specimenId);
        await seed.Send(at, by, new MarkSpecimenSentCommand(specimenId, null, courier, version, by.Actor), "send a sample");
    }

    /// <summary>The lab receives a sample, which finds or opens its lab case (MC15, MC16).</summary>
    public static async Task ReceiveSampleAsync(DemoSeed seed, Guid specimenId, DateTime at, string? labReference = null)
    {
        var version = await SpecimenVersionAsync(seed, specimenId);
        await seed.Send(at, DemoPeople.Aldous, new MarkSpecimenReceivedCommand(specimenId, null, labReference, version, DemoPeople.Aldous.Actor), "receive a sample");
    }

    public static Task<int> SpecimenVersionAsync(DemoSeed seed, Guid specimenId) =>
        seed.Db(db => db.Specimens.Where(s => s.Id == specimenId).Select(s => s.Version).SingleAsync(seed.Ct));

    /// <summary>The lab case a sample is in.</summary>
    public static async Task<Guid> CaseOfAsync(DemoSeed seed, Guid specimenId) =>
        await seed.Db(db => db.Specimens.Where(s => s.Id == specimenId).Select(s => s.LabCaseId).SingleAsync(seed.Ct))
        ?? throw new InvalidOperationException("The sample isn't in a lab case.");

    private static Task<int> CaseVersionAsync(DemoSeed seed, Guid caseId) =>
        seed.Db(db => db.LabCases.Where(c => c.Id == caseId).Select(c => c.Version).SingleAsync(seed.Ct));

    /// <summary>
    /// The lab works a case: each step recorded by the lab's pathologist, spread evenly between <paramref name="from"/>
    /// and <paramref name="until"/>, as the light demo's seed spreads them. The outcomes are the pathologist's, recorded
    /// as given; MedApp never chooses one.
    /// </summary>
    public static async Task WorkCaseAsync(DemoSeed seed, Guid caseId, DateTime from, DateTime until, params LabWork[] work)
    {
        var lab = DemoPeople.Aldous;
        var testId = await seed.Db(db => db.LabCases.Where(c => c.Id == caseId).Select(c => c.LabTestId).SingleAsync(seed.Ct));
        var steps = await seed.Db(db => db.LabTests.Where(t => t.Id == testId).SelectMany(t => t.Steps).ToDictionaryAsync(s => s.Name, s => s.Id, seed.Ct));
        var gap = (until - from) / (work.Length + 1);
        for (var i = 0; i < work.Length; i++)
        {
            var at = from + gap * (i + 1);
            var item = work[i];
            var version = await CaseVersionAsync(seed, caseId);
            if (item.Choice is { } choice)
                await seed.Send(at, lab, new RecordLabOutcomeCommand(caseId, choice, item.Outcome, item.Detail, version, lab.Actor), $"record '{item.Outcome}' on a lab case");
            else
                await seed.Send(at, lab, new RecordLabStepCommand(caseId, item.Accessioned ? null : steps[item.Step!], item.Accessioned ? "Accessioned" : null,
                    null, item.Detail, version, lab.Actor), $"record '{item.Step ?? "Accessioned"}' on a lab case");
        }
    }

    /// <summary>The histology steps every histology case goes through, then the report's step.</summary>
    public static readonly LabWork[] Histology =
    [
        LabWork.Accession, LabWork.Adequate, LabWork.TestStep("Cut-up"), LabWork.TestStep("Processing and embedding"),
        LabWork.TestStep("Sectioning and staining"), LabWork.TestStep("Microscopy and report"),
    ];

    /// <summary>The pathologist enters the results and authorises the report.</summary>
    public static async Task AuthoriseAsync(DemoSeed seed, Guid caseId, DateTime at, string results)
    {
        var version = await CaseVersionAsync(seed, caseId);
        await seed.Send(at, DemoPeople.Aldous, new AuthoriseLabReportCommand(caseId, results, version, DemoPeople.Aldous.Actor), "authorise a lab report");
    }

    /// <summary>A letter from a template on the pathway, its body written by its author (and its recipients, if given).</summary>
    public static async Task<LetterResponse> WriteLetterAsync(
        DemoSeed seed, StoryPathway pathway, DemoPerson author, string template, DateTime at, string body, DateTime editedAt,
        IReadOnlyList<LetterRecipientCommandItem>? recipients = null)
    {
        var letter = await seed.Send(at, author, new CreateLetterCommand(seed.LetterTemplates[template], pathway.Id, null, author.Actor), $"write a letter on {pathway.Latest.Title}");
        return await seed.Send(editedAt, author, new UpdateLetterCommand(letter.Id, letter.Letterhead, body, letter.Closing, letter.LargePrint,
            recipients ?? letter.Recipients.Select(r => new LetterRecipientCommandItem(r.Kind, r.PracticeId, r.Name, r.AddressLines, r.Postcode, r.Email)).ToList(),
            letter.Version, author.Actor), $"edit a letter on {pathway.Latest.Title}");
    }

    /// <summary>A clinic letter's body: the template's headings, filled in by its author.</summary>
    public static string ClinicBody(string diagnosis, string found, string plan) =>
        $"Dear colleague,\n\n{DemoSetup.ReLine}\n\n## Diagnosis\n\n{diagnosis}\n\n## What we found\n\n{found}\n\n## Plan\n\n{plan}";
}
