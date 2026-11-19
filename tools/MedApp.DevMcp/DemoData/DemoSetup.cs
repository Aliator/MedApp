using MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;
using MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;
using MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using MedApp.Application.Calendar.Hours.Commands.SetStaffHours;
using MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using MedApp.Application.Clinics.Commands.CreateClinicPattern;
using MedApp.Application.Clinics.Commands.CreateClinicSession;
using MedApp.Application.Clinics.Commands.CreateSessionsFromPattern;
using MedApp.Application.Consent.Setup;
using MedApp.Application.Hospitals.Setup;
using MedApp.Application.Lab.Tests;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.Commands.SetMonitoringRuleEnabled;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Practices.Commands.SetPracticeArchived;
using MedApp.Application.Sites.Commands.CreateRoom;
using MedApp.Application.Sites.Commands.CreateSite;
using MedApp.Application.Sites.Commands.SetSiteHours;
using MedApp.Application.Sites.Commands.UpdateSite;
using MedApp.Application.Specimens.Setup;
using MedApp.Application.Teams.Commands.CreateTeam;
using MedApp.Application.Teams.Commands.SetTeamArchived;
using MedApp.Contracts.Lab.Requests;
using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Sites;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data.Configurations.Calendar;
using Microsoft.EntityFrameworkCore;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The clinic's setup, as the light demo's setup slice has it (<c>MedApp.Web/src/demo/slices/setup.ts</c>, with the
/// lab's catalogue, hospitals, consent forms and letter templates from the other slices), made through the app's own
/// commands by the admin (or the person whose calendar it is): sites and rooms with their hours, the GP practice
/// directory, activity types, closed days, the Dermatology team, the stage library and its templates, specimen types,
/// lab tests, hospitals, consent forms, letter templates, working hours, calendar templates, monitoring rules and
/// clinic sessions. All synthetic: invented sites and practices, <c>ZZ</c> postcodes, Ofcom's drama numbers.
/// </summary>
public static class DemoSetup
{
    public const string ClinicRoom1 = "Clinic room 1";
    public const string ProcedureRoom = "Procedure room";
    public const string Northfield = "Northfield Health Centre (synthetic)";

    public const string ExampleLane = "Example Lane Surgery (synthetic)";
    public const string HarbourView = "Harbour View Medical Practice (synthetic)";

    public const string SkinBiopsy = "Skin biopsy (punch)";
    public const string BloodSample = "Blood sample";
    public const string SkinScraping = "Skin scraping";

    public const string SkinHistology = "Skin histology";
    public const string FullBloodCount = "Full blood count";
    public const string FungalScraping = "Skin scraping for fungi";
    public const string ScrapingMicroscopy = "Skin scraping microscopy";

    public const string Riverside = "Riverside General Hospital (synthetic)";

    public const string AdultForm = "Consent to a procedure (adult) 3.2";
    public const string AdultFormOld = "Consent to a procedure (adult) 3.1";
    public const string PhotographyForm = "Clinical photography";

    public const string ClinicLetter = "Clinic letter";
    public const string OnwardReferral = "Onward referral to hospital";

    public const string Clinic = "Clinic";
    public const string AdminTime = "Admin time";
    public const string Lunch = "Lunch";
    public const string Triage = "Triage";
    public const string ProcedureList = "Procedure list";

    private const string Letterhead = "Community Specialist Centre (synthetic)\n1 Example Way\nExampleton\nZZ9 9ZZ";
    private const string Closing = "Yours sincerely,\n\n\n{{author.name}}\n{{author.jobTitle}}";
    public const string ReLine = "**Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}**";

    /// <summary>The onward referral's opening: who the patient is and where they're registered, all from merge fields.</summary>
    public const string OnwardReferralOpening =
        $"Dear colleague,\n\n{ReLine}\n\n{{{{patient.address}}}}\n\nRegistered with {{{{practice.name}}}}. Last seen in the Dermatology clinic on {{{{appointment.lastAttendedDate}}}}.\n\nPlease see {{{{patient.name}}}} for the treatment below.";

    /// <summary>What the clinic had set up before its first pathway: everything but the calendars and clinic sessions.</summary>
    public static async Task CreateAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var admin = DemoPeople.Admin;
        var calendar = admin.Calendar;

        // Sites and rooms: the main site renamed, its two rooms, and a second site with its own hours.
        var at = t.Ago(200, 9, 10);
        await seed.Send(at, admin, new UpdateSiteCommand(Site.MainSiteId, "Community Specialist Centre (synthetic)", ["1 Example Way", "Exampleton"], "ZZ1 4DD", "01632 960100"), "rename the main site");
        seed.Sites["Centre"] = Site.MainSiteId;
        foreach (var room in new[] { ClinicRoom1, ProcedureRoom })
            seed.Rooms[room] = (await seed.Send(at.AddMinutes(1), admin, new CreateRoomCommand(Site.MainSiteId, room), $"add {room}")).Id;
        var northfield = await seed.Send(at.AddMinutes(3), admin, new CreateSiteCommand(Northfield, ["5 Sample Street", "Northfield"], "ZZ5 5EE", "01632 960200"), "add Northfield");
        seed.Sites[Northfield] = northfield.Id;
        foreach (var room in new[] { "Room A", "Room B" })
            seed.Rooms[room] = (await seed.Send(at.AddMinutes(4), admin, new CreateRoomCommand(northfield.Id, room), $"add {room}")).Id;
        await seed.Send(t.Ago(60, 9, 0), admin, new SetSiteHoursCommand(northfield.Id,
            new[] { "Monday", "Tuesday", "Wednesday", "Thursday" }.Select(d => new SiteHoursDay(d, new TimeOnly(8, 30), new TimeOnly(17, 0))).ToList()), "set Northfield's hours");

        // The GP practice directory: two in use, one archived.
        at = t.Ago(200, 9, 30);
        seed.Practices[ExampleLane] = (await seed.Send(at, admin, new CreatePracticeCommand(ExampleLane, "DEMO01", ["1 Example Lane", "Exampleton"], "ZZ1 1AA", "01632 960001", null), "add a practice")).Id;
        seed.Practices[HarbourView] = (await seed.Send(at.AddMinutes(1), admin, new CreatePracticeCommand(HarbourView, "DEMO02", ["22 Quay Street", "Exampleton"], "ZZ2 2BB", "01632 960002", null), "add a practice")).Id;
        var oldMill = await seed.Send(at.AddMinutes(2), admin, new CreatePracticeCommand("Old Mill Health Centre (synthetic)", null, ["Mill Road", "Sampleford"], "ZZ3 3CC", null, null), "add a practice");
        await seed.Send(t.Ago(40), admin, new SetPracticeArchivedCommand(oldMill.Id, true, oldMill.Version), "archive a practice");

        // Activity types: the clinic's own beside the ones every database has, those it doesn't use archived.
        at = t.Ago(150, 9, 0);
        foreach (var type in CalendarActivityTypeConfiguration.Seeded)
            seed.ActivityTypes[type.Name] = type.Id;
        foreach (var (name, colour) in new[] { (Clinic, "blue"), (Triage, "orange"), (ProcedureList, "purple") })
            seed.ActivityTypes[name] = (await seed.Send(at, admin, new CreateActivityTypeCommand(name, colour), $"add the {name} activity type")).Id;
        await seed.Send(at, admin, new UpdateActivityTypeCommand(seed.ActivityTypes[AdminTime], null, "gray"), "recolour Admin time");
        foreach (var unused in CalendarActivityTypeConfiguration.Seeded.Where(s => s.Name is not (Lunch or AdminTime)))
            await seed.Send(at, admin, new SetActivityTypeArchivedCommand(unused.Id, true), $"archive {unused.Name}");

        // The teams: Dermatology, the one clinic, beside the lab every database has. General, which every database starts
        // with, is archived: the demo has one clinic and the lab.
        at = t.Ago(120, 9, 0);
        var dermatology = await seed.Send(at, admin, new CreateTeamCommand(DemoPeople.Dermatology, 14, 28, 63, 126), "add Dermatology");
        seed.Teams[DemoPeople.Dermatology] = dermatology.Id;
        var generalVersion = await seed.Db(db => db.Teams.Where(x => x.Id == Team.GeneralId).Select(x => x.Version).SingleAsync(seed.Ct));
        await seed.Send(at.AddMinutes(1), admin, new SetTeamArchivedCommand(Team.GeneralId, true, generalVersion), "archive General");
        await DemoPeople.AddMembershipsAsync(seed, at.AddMinutes(5));

        await CreateTemplatesAsync(seed);

        // The lab's catalogue: specimen types and tests (one of each archived, never used, so it can be deleted).
        at = t.Ago(150, 10, 0);
        foreach (var (name, days, snomed) in new[] { (SkinBiopsy, 14, "309066003"), (BloodSample, 5, "119297000"), (SkinScraping, 10, (string?)null) })
            seed.SpecimenTypes[name] = (await seed.Send(at, admin, new CreateSpecimenTypeCommand(name, days, snomed), $"add {name}")).Id;
        var swab = await seed.Send(at, admin, new CreateSpecimenTypeCommand("Skin swab", 4), "add Skin swab");
        await seed.Send(t.Ago(60), admin, new SetSpecimenTypeArchivedCommand(swab.Id, true), "archive Skin swab");

        foreach (var (test, archived) in LabTests())
        {
            var made = await seed.Send(at, admin, new CreateLabTestCommand(test), $"add the {test.Name} test");
            seed.LabTests[test.Name] = made.Id;
            if (archived)
                await seed.Send(t.Ago(60), admin, new SetLabTestArchivedCommand(made.Id, true), $"archive {test.Name}");
        }

        // The hospitals that send the lab samples (one has never sent any, so it can be deleted).
        seed.Hospitals[Riverside] = (await seed.Send(at, admin, new CreateHospitalCommand(Riverside, "ZZR01", "Histopathology requests desk", "Post", 10), "add a hospital")).Id;
        await seed.Send(at, admin, new CreateHospitalCommand("Hillcrest Community Hospital (synthetic)", "ZZH02", null, "EmailOutsideMedApp", 14), "add a hospital");

        // Consent forms: the adult form's older version (archived once its successor is in use: the story does it),
        // the current one, a best interests form never used, and the photo consent form (MC27).
        seed.ConsentForms[AdultFormOld] = (await seed.Send(t.Ago(200), admin, new CreateConsentFormCommand("Consent to a procedure (adult)", "3.1"), "add a consent form")).Id;
        seed.ConsentForms[AdultForm] = (await seed.Send(t.Ago(60), admin, new CreateConsentFormCommand("Consent to a procedure (adult)", "3.2"), "add a consent form")).Id;
        await seed.Send(t.Ago(60), admin, new CreateConsentFormCommand("Treatment in the patient’s best interests (lacks capacity)", "1.0"), "add a consent form");
        seed.ConsentForms[PhotographyForm] = (await seed.Send(t.Ago(60), admin, new CreateConsentFormCommand(PhotographyForm, "1.0"), "add a consent form")).Id;

        // Letter templates: two for every team, and Dermatology's own (MC28). Merge fields copy identity and
        // administrative details only; the clinical text is the author's.
        at = t.Ago(90, 9, 0);
        var dermatologyId = seed.TeamId(DemoPeople.Dermatology);
        foreach (var (name, kind, teamId, body) in new (string, string, Guid?, string)[]
        {
            (ClinicLetter, "ClinicLetter", null, $"Dear colleague,\n\n{ReLine}\n\n## Diagnosis\n\n## What we found\n\n## Plan"),
            ("End of care summary to GP", "DischargeSummary", null, $"Dear colleague,\n\n{ReLine}\n\n{{{{patient.name}}}}'s care with us is complete.\n\n## Summary of care\n\n## For the GP"),
            ("Referral response", "ReferralResponse", dermatologyId, $"Dear colleague,\n\n{ReLine}\n\nThank you for your referral, received on {{{{referral.receivedDate}}}}.\n\n## Our decision"),
            ("Results review letter to GP", "ClinicLetter", dermatologyId, $"Dear colleague,\n\n{ReLine}\n\nWe went through {{{{patient.name}}}}'s results with them on {{{{appointment.lastAttendedDate}}}}.\n\n## Results\n\n## What we discussed\n\n## Plan"),
            ("Dermatology end of care letter", "DischargeSummary", dermatologyId, $"Dear colleague,\n\n{ReLine}\n\n{{{{patient.name}}}} was last seen in the Dermatology clinic on {{{{appointment.lastAttendedDate}}}}. Their care with us is complete, and we hand it back to you.\n\n## Summary of care\n\n## Advice given\n\n## For the GP"),
            (OnwardReferral, "OnwardReferral", dermatologyId, $"{OnwardReferralOpening}\n\n## Reason for referral\n\n## Findings and results\n\n## What we are asking for"),
        })
            seed.LetterTemplates[name] = (await seed.Send(at, admin, new CreateLetterTemplateCommand(name, kind, teamId, Letterhead, body, Closing), $"add the {name} template")).Id;

        // Working hours: Tom Hallworth works Tuesday to Thursday at the centre; everyone else the default.
        await seed.Send(t.Ago(90), admin, new SetStaffHoursCommand(DemoPeople.Hallworth.Id,
            new[] { "Tuesday", "Wednesday", "Thursday" }.Select(d => new StaffHoursDay(d, new TimeOnly(9, 0), new TimeOnly(17, 0), Site.MainSiteId)).ToList(), calendar), "set Tom Hallworth's hours");

        // The closed days: the next bank holidays everywhere, and a training day at Northfield in about three weeks.
        at = t.Ago(30, 9, 0);
        foreach (var (date, name) in new[] { (t.NextDate(12, 25), "Christmas Day"), (t.NextDate(12, 26), "Boxing Day"), (t.NextDate(1, 1), "New Year's Day") })
            await seed.Send(at, admin, new CreateClosedDayCommand(date, name, calendar), $"close {name}");
        var training = t.DaysFrom(21);
        while (training.DayOfWeek != DayOfWeek.Wednesday)
            training = training.AddDays(1);
        await seed.Send(at, admin, new CreateClosedDayCommand(training, "Staff training day", calendar, northfield.Id), "close Northfield for training");

        await CreateMonitoringRulesAsync(seed);
    }

    /// <summary>The lab's test catalogue (MC13), and whether each is archived.</summary>
    private static IEnumerable<(LabTestRequest Test, bool Archived)> LabTests()
    {
        static LabTestRequest Test(string name, string kind, string? snomed, int target, int limit, int atRisk, params (string Name, string Kind, bool Optional)[] steps) => new()
        {
            Name = name,
            Kind = kind,
            SnomedCode = snomed,
            TargetDays = target,
            LimitDays = limit,
            AtRiskDays = atRisk,
            Steps = steps.Select(s => new LabTestStepRequest { Name = s.Name, Kind = s.Kind, IsOptional = s.Optional }).ToList(),
        };

        yield return (Test(SkinHistology, "Histology", "252416005", 7, 10, 2,
            ("Cut-up", "Standard", false), ("Processing and embedding", "Standard", false), ("Sectioning and staining", "Standard", false),
            ("Extra stains", "ExtraTests", true), ("Sent for a second opinion", "SentForSecondOpinion", true),
            ("Second opinion back", "SecondOpinionReturned", true), ("Microscopy and report", "Standard", false)), false);
        yield return (Test(FullBloodCount, "Haematology", "26604007", 2, 3, 1, ("Analyser run", "Standard", false), ("Blood film review", "ExtraTests", true)), false);
        yield return (Test(FungalScraping, "Microbiology", null, 14, 21, 3, ("Microscopy", "Standard", false), ("Culture", "Standard", false), ("Report", "Standard", false)), false);
        yield return (Test("Liver function tests", "ClinicalChemistry", null, 2, 3, 1, ("Analyser run", "Standard", false)), true);
        yield return (Test(ScrapingMicroscopy, "Microbiology", null, 5, 7, 1, ("Microscopy", "Standard", false), ("Report", "Standard", false)), false);
    }

    /// <summary>
    /// The stage library and Dermatology's templates (<see cref="DemoDermatology"/>), written as an admin set them up
    /// before the first pathway: one definition per stage name, each template's stages, links and addable branches.
    /// </summary>
    private static async Task CreateTemplatesAsync(DemoSeed seed)
    {
        var definitions = DemoDermatology.Definitions().ToDictionary(d => d.Name, d => new PatientPathwayStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = d.Name,
            Description = d.Description,
            Instructions = string.Empty,
            SnomedCode = d.SnomedCode,
            SnomedTerm = d.SnomedTerm,
            VisitRequirement = d.Visit,
            NeedsLetterKind = d.NeedsLetter,
            NeedsConsent = d.NeedsConsent,
            WaitsForResults = d.WaitsForResults,
        });

        var dermatology = seed.TeamId(DemoPeople.Dermatology);
        var templates = DemoDermatology.Templates().Select(t => new PathwayTemplate
        {
            Id = Guid.NewGuid(),
            Name = t.Name,
            TeamId = dermatology,
            RetentionClass = t.RetentionClass,
            Maps = t.Stages.Select(s => new PathwayTemplateMap
            {
                StageDefinitionId = definitions[s.Name].Id,
                StageDefinition = definitions[s.Name],
                StageOrder = s.StageOrder,
                IsOptional = s.IsOptional,
                ChoiceKind = s.ChoiceKind,
                Outcomes = s.Outcomes.ToList(),
                OwnerTeamId = s.IsLabs ? Team.LabId : null,
                StopsClock = s.StopsClock,
            }).ToList(),
            Links = t.Links.ToList(),
            AddableBranches = t.Branches
                .Select(b => new PathwayTemplateBranch(Guid.NewGuid(), b.Name, b.StageNames.Select(n => definitions[n].Id).ToList(), b.AttachAfterStageOrders, b.IsRepeatable))
                .ToList(),
        }).ToList();

        await seed.Db(async db =>
        {
            db.PatientPathwayStageDefinitions.AddRange(definitions.Values);
            db.PathwayTemplates.AddRange(templates);
            await db.SaveChangesAsync(seed.Ct);
        });

        foreach (var (name, definition) in definitions)
            seed.Definitions[name] = definition.Id;
        foreach (var template in templates)
            seed.Templates[template.Name] = template.Id;
    }

    /// <summary>The admin's monitoring rules (one switched off since), and their service alerts (MC19).</summary>
    private static async Task CreateMonitoringRulesAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var admin = DemoPeople.Admin;
        await seed.Send(t.Ago(20), admin, new CreateMonitoringRuleCommand(admin.Id, null, "StageFinished", "High",
            seed.Templates["Suspected skin cancer"], seed.Definitions["Histology"], true), "add a monitoring rule");
        var onHold = await seed.Send(t.Ago(15), admin, new CreateMonitoringRuleCommand(admin.Id, DemoPeople.Hallworth.Id, "PathwayBlocked", null, null, null, true), "add a monitoring rule");
        await seed.Send(t.Ago(4), admin, new SetMonitoringRuleEnabledCommand(onHold.Id, admin.Id, false), "switch a monitoring rule off");
        await seed.Send(t.Ago(10), admin, new CreateMonitoringRuleCommand(admin.Id, null, "ReferralNotTriaged", null, null, null, true, 2, seed.TeamId(DemoPeople.Dermatology)), "add a service alert");
        await seed.Send(t.Ago(10), admin, new CreateMonitoringRuleCommand(admin.Id, null, "LabCaseOverdue", null, null, null, true, 0), "add a service alert");
    }

    /// <summary>
    /// Calendar templates (the admin's early referral admin, their usual week and default; Nadia Thornbury's skin clinic
    /// day, used by no week), and Tom Hallworth's clinic sessions: his Tuesday skin clinic's pattern and the sessions it
    /// made for this week and the next two, and a one-off biopsy list next Thursday afternoon.
    /// </summary>
    public static async Task CreateCalendarSetupAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var admin = DemoPeople.Admin;
        var thornbury = DemoPeople.Thornbury;
        var at = t.Ago(45, 9, 0);

        var referralAdmin = await seed.Send(at, admin, new CreateDayTemplateCommand(admin.Id, "Referral admin",
            [new DayTemplateBlockValue(seed.ActivityTypes[AdminTime], new TimeOnly(8, 15), new TimeOnly(9, 0), "Referral admin")], admin.Calendar), "add a day template");
        await seed.Send(at, thornbury, new CreateDayTemplateCommand(thornbury.Id, "Skin clinic day",
        [
            new DayTemplateBlockValue(seed.ActivityTypes[Clinic], new TimeOnly(9, 0), new TimeOnly(12, 30), "Skin clinic"),
            new DayTemplateBlockValue(seed.ActivityTypes[Lunch], new TimeOnly(12, 30), new TimeOnly(13, 15), null),
            new DayTemplateBlockValue(seed.ActivityTypes[AdminTime], new TimeOnly(16, 0), new TimeOnly(17, 0), "Letters"),
        ], thornbury.Calendar), "add a day template");
        var week = await seed.Send(at.AddMinutes(5), admin, new CreateWeekTemplateCommand(admin.Id, "Usual week",
            null, referralAdmin.Id, referralAdmin.Id, null, referralAdmin.Id, admin.Calendar), "add a week template");
        await seed.Send(t.Ago(30, 9, 0), admin, new SetStaffWeekTemplateCommand(admin.Id, week.Id, admin.Calendar), "set the admin's usual week");

        // Tom Hallworth's Tuesday skin clinic, from four weeks ago, and its sessions from this week (made on Monday).
        var hallworth = DemoPeople.Hallworth;
        var dermatology = seed.TeamId(DemoPeople.Dermatology);
        var patternFrom = t.ThisMonday.AddDays(-27);
        var pattern = await seed.Send(DemoTimeline.At(t.ThisMonday.AddDays(-28), 9), admin, new CreateClinicPatternCommand(hallworth.Id, seed.Rooms[ClinicRoom1], dermatology,
            "Tuesday", new TimeOnly(9, 0), new TimeOnly(12, 0), 20, 1, patternFrom, null, admin.Calendar), "add Tom Hallworth's clinic pattern");
        await seed.Send(t.Past(DemoTimeline.At(t.ThisMonday, 8, 0)), admin, new CreateSessionsFromPatternCommand(pattern.Id, 3, admin.Calendar), "make the clinic's sessions");
        var thursday = t.ThisWeek(10);
        await seed.Send(t.Past(DemoTimeline.At(t.ThisMonday, 8, 5)), admin, new CreateClinicSessionCommand(hallworth.Id, seed.Rooms[ProcedureRoom], dermatology,
            DemoTimeline.At(thursday, 13, 30), DemoTimeline.At(thursday, 16, 30), 30, admin.Calendar), "add a biopsy list");
    }
}
