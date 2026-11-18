using MediatR;
using MedApp.Application.Authentication.Users.Commands.SetStaffDetails;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Hours.Commands.SetStaffHours;
using MedApp.Application.Consent.Setup;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Sites.Commands.CreateRoom;
using MedApp.Application.Sites.Commands.CreateSite;
using MedApp.Application.Sites.Commands.SetSiteHours;
using MedApp.Application.Sites.Commands.UpdateSite;
using MedApp.Domain.Letters;
using MedApp.Domain.Sites;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>What the centre set up through Administration, as the seed made it: the IDs the rest of the story uses.</summary>
public sealed class CentreSetup
{
    public required IReadOnlyList<(Guid Id, string Name)> Practices { get; init; }
    public required Guid OutreachSiteId { get; init; }

    /// <summary>Rooms by name ("Main site: Clinic room 1").</summary>
    public required IReadOnlyDictionary<string, Guid> Rooms { get; init; }

    /// <summary>The consent forms: the skin procedures' and the gynaecology biopsy's.</summary>
    public required Guid SkinConsentFormId { get; init; }
    public required Guid GynaecologyConsentFormId { get; init; }

    /// <summary>Letter templates by team name and kind.</summary>
    public required IReadOnlyDictionary<(string Team, LetterKind Kind), Guid> LetterTemplates { get; init; }

    public int SitesWithHours { get; init; }
    public int StaffWithHours { get; init; }
}

/// <summary>
/// Phase 3 to 5's setup (<c>docs/centre-phases-3-5-plan.md</c>, sections 2.7, 3.9 and 4.13), made through the app's own
/// commands by the clinical lead (or the service manager, for hours), as an admin set it up:
/// <list type="bullet">
/// <item>Staff full names and job titles (they go on letters), when the centre started.</item>
/// <item>The GP practice directory: invented practices on invented streets, postcodes in the <c>ZZ</c> area (none
/// exists), phone numbers from Ofcom's drama range (01632 960xxx), no ODS codes.</item>
/// <item>The main site's details, an invented outreach clinic, their rooms and opening hours: a Tuesday evening clinic
/// until 20:00 at the main site, and Saturday biomechanics mornings at the outreach clinic.</item>
/// <item>Consent forms (invented names, marked demo) and letter templates for each team: a clinic letter, a
/// discharge summary, a referral response and a request for information. Made when the lab, results and letters went
/// live (<see cref="DemoTimeline.LabAndLettersFromUtc"/>).</item>
/// <item>Working hours for the part-time and visiting staff, from then: the visiting consultants on their days here, the
/// diabetes nurse specialist mornings only. Everyone else keeps the default.</item>
/// </list>
/// </summary>
public static class DemoCentreSetup
{
    public const string OutreachSiteName = "Wrenhurst outreach clinic";

    /// <summary>Invented practices (the referrals' names), with invented addresses, ZZ postcodes and drama-range phones.</summary>
    public static IReadOnlyList<(string Name, string[] Address, string Postcode, string Phone)> Practices =>
        ReferralTexts.Practices.Select((name, i) => (
            name,
            new[] { $"{10 + 7 * i} {Streets[i % Streets.Length]}", Towns[i % Towns.Length] },
            $"ZZ{1 + i % 9} {1 + i % 9}{(char)('A' + i)}{(char)('B' + i)}",
            $"01632 960{100 + i:000}"))
        .ToList();

    /// <summary>Invented streets and towns: none is a real place.</summary>
    public static readonly string[] Streets =
        ["Brackenmoor Road", "Kestrel Lane", "Thornwick Vale", "Wrenhurst Street", "Pellowick Lane", "Quillbrook Way", "Larkfold Avenue", "Morrowdene Close"];

    public static readonly string[] Towns = ["Brackenmoor", "Thornwick", "Quillbrook", "Wrenhurst"];

    public static readonly string[] MainRooms = ["Clinic room 1", "Clinic room 2", "Clinic room 3", "Minor surgery room", "Phlebotomy room"];
    public static readonly string[] OutreachRooms = ["Consulting room A", "Gait lab"];

    public const string SkinConsentForm = "Consent for a skin procedure (demo)";
    public const string GynaecologyConsentForm = "Consent for an endometrial biopsy (demo)";

    public static async Task<CentreSetup> CreateAsync(SeedServices services, IReadOnlyList<StaffMember> staff, DemoTimeline timeline, CancellationToken ct)
    {
        var lead = staff.Single(s => s.Kind == StaffKind.ClinicalLead);
        var at = timeline.AppStartUtc.AddDays(-10);

        Task<T> Send<T>(IRequest<T> request) =>
            services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(request, ct)));

        // Names and job titles, as they go on letters.
        services.At(at, lead.ActivityActor);
        foreach (var member in staff.Where(s => s.Kind != StaffKind.Leaver || s.WorksAt(at)))
            await Send(new SetStaffDetailsCommand(member.Username, FullName(member), member.Seed.JobTitle));

        // The GP practice directory.
        var practices = new List<(Guid, string)>();
        foreach (var (name, address, postcode, phone) in Practices)
        {
            var practice = await Send(new CreatePracticeCommand(name, null, address, postcode, phone, null));
            practices.Add((practice.Id, name));
        }

        // Sites, rooms and their hours.
        await Send(new UpdateSiteCommand(Site.MainSiteId, Site.MainSiteName, ["2 Larkfold Avenue", "Brackenmoor"], "ZZ1 4QL", "01632 960001"));
        var outreach = await Send(new CreateSiteCommand(OutreachSiteName, ["Wrenhurst Community Hall", "18 Morrowdene Close", "Wrenhurst"], "ZZ7 2NW", "01632 960002"));
        var rooms = new Dictionary<string, Guid>();
        foreach (var room in MainRooms)
            rooms[$"{Site.MainSiteName}: {room}"] = (await Send(new CreateRoomCommand(Site.MainSiteId, room))).Id;
        foreach (var room in OutreachRooms)
            rooms[$"{OutreachSiteName}: {room}"] = (await Send(new CreateRoomCommand(outreach.Id, room))).Id;

        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        // The main site stays open until 20:00 on Tuesdays for the evening clinic.
        await Send(new SetSiteHoursCommand(Site.MainSiteId, weekdays
            .Select(d => new SiteHoursDay(d.ToString(), new TimeOnly(8, 0), d == DayOfWeek.Tuesday ? new TimeOnly(20, 0) : new TimeOnly(18, 0)))
            .ToList()));
        // The outreach clinic, with Saturday biomechanics mornings.
        await Send(new SetSiteHoursCommand(outreach.Id, weekdays
            .Select(d => new SiteHoursDay(d.ToString(), new TimeOnly(8, 0), new TimeOnly(18, 0)))
            .Append(new SiteHoursDay(nameof(DayOfWeek.Saturday), new TimeOnly(8, 30), new TimeOnly(12, 30)))
            .ToList()));

        // Consent forms and letter templates, when the lab, results and letters went live.
        var live = timeline.LabAndLettersFromUtc.AddDays(-3);
        services.At(live, lead.ActivityActor);
        var skinForm = await Send(new CreateConsentFormCommand(SkinConsentForm, "v1 (demo)"));
        var gynaeForm = await Send(new CreateConsentFormCommand(GynaecologyConsentForm, "v1 (demo)"));

        var teams = await services.Run(sp => Task.FromResult(sp.GetRequiredService<Infrastructure.Data.MedAppDbContext>()
            .Teams.Select(s => new { s.Id, s.Name }).ToList()));
        var templates = new Dictionary<(string, LetterKind), Guid>();
        foreach (var name in DemoTeams.Names)
        {
            var teamId = teams.Single(s => s.Name == name).Id;
            foreach (var kind in LetterKindsSeeded)
            {
                var template = await Send(new CreateLetterTemplateCommand(
                    $"{name}: {KindName(kind)}", kind.ToString(), teamId, Letterhead, BodySkeleton(kind), Closing));
                templates[(name, kind)] = template.Id;
            }
        }

        // Working hours from then: the visiting consultants on their days here, the diabetes nurse specialist mornings.
        var manager = staff.Single(s => s.Username == DemoStaff.ManagerUsername);
        var hoursAt = timeline.LabAndLettersFromUtc;
        services.At(hoursAt, manager.ActivityActor);
        var hoursActor = new CalendarActor(manager.Id, manager.Username, manager.PermissionsAt(hoursAt));
        var withHours = 0;
        foreach (var (username, days) in PartTimeHours)
        {
            var member = staff.Single(s => s.Username == username);
            await Send(new SetStaffHoursCommand(member.Id, days.Select(d => new StaffHoursDay(d.Day.ToString(), d.Starts, d.Ends, Site.MainSiteId)).ToList(), hoursActor));
            withHours++;
        }

        return new CentreSetup
        {
            Practices = practices,
            OutreachSiteId = outreach.Id,
            Rooms = rooms,
            SkinConsentFormId = skinForm.Id,
            GynaecologyConsentFormId = gynaeForm.Id,
            LetterTemplates = templates,
            SitesWithHours = 2,
            StaffWithHours = withHours,
        };
    }

    /// <summary>The working patterns set for part-time and visiting staff: their days here, as their calendars have them.</summary>
    public static readonly IReadOnlyList<(string Username, (DayOfWeek Day, TimeOnly Starts, TimeOnly Ends)[] Days)> PartTimeHours =
    [
        (DemoStaff.GynaecologistUsername, [(DayOfWeek.Tuesday, new(8, 0), new(18, 0)), (DayOfWeek.Thursday, new(8, 0), new(18, 0))]),
        (DemoStaff.UrologistUsername, [(DayOfWeek.Wednesday, new(8, 0), new(18, 0)), (DayOfWeek.Friday, new(8, 0), new(18, 0))]),
        (DemoStaff.DiabetesNurseUsername,
        [
            (DayOfWeek.Monday, new(8, 0), new(13, 0)), (DayOfWeek.Tuesday, new(8, 0), new(13, 0)), (DayOfWeek.Wednesday, new(8, 0), new(13, 0)),
            (DayOfWeek.Thursday, new(8, 0), new(13, 0)), (DayOfWeek.Friday, new(8, 0), new(13, 0)),
        ]),
    ];

    /// <summary>The kinds of letter each team has a template for.</summary>
    public static readonly LetterKind[] LetterKindsSeeded =
        [LetterKind.ClinicLetter, LetterKind.DischargeSummary, LetterKind.ReferralResponse, LetterKind.InformationRequest];

    public static string KindName(LetterKind kind) => kind switch
    {
        LetterKind.ClinicLetter => "clinic letter",
        LetterKind.DischargeSummary => "discharge summary",
        LetterKind.ReferralResponse => "referral response",
        LetterKind.InformationRequest => "request for information",
        _ => kind.ToString(),
    };

    /// <summary>A staff member's full name as letters print it: invented, with Dr for the doctors.</summary>
    public static string FullName(StaffMember member) => member.Kind switch
    {
        StaffKind.ClinicalLead => "Dr Alex Ellery",
        StaffKind.Dermatologist or StaffKind.SkinGp or StaffKind.Gynaecologist or StaffKind.Urologist or StaffKind.Leaver => $"Dr {member.Seed.Name}",
        _ => member.Seed.Name,
    };

    private const string Letterhead =
        "{{site.name}}\nCommunity specialist centre (synthetic demo: not a real service)\nTelephone 01632 960001";

    private const string Closing = "Yours sincerely,\n\n{{approver.name}}\n{{approver.jobTitle}}";

    private static string BodySkeleton(LetterKind kind) => kind switch
    {
        LetterKind.ReferralResponse =>
            "Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}\n\n" +
            "Thank you for your referral, received on {{referral.receivedDate}}.\n\n[What we will do next]",
        LetterKind.InformationRequest =>
            "Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}\n\n" +
            "Thank you for your referral, received on {{referral.receivedDate}}. Before we can see your patient, please send us:\n\n[What we need]",
        LetterKind.DischargeSummary =>
            "Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}\n{{patient.address}}\n\n" +
            "Dear colleague at {{practice.name}},\n\nYour patient has been discharged from our care.\n\n" +
            "What we found:\n[Findings]\n\nWhat we did:\n[Treatment]\n\nWhat we ask of you:\n[Actions for the GP]",
        _ =>
            "Re: {{patient.name}}, born {{patient.dateOfBirth}}, NHS number {{patient.nhsNumber}}\n{{patient.address}}\n\n" +
            "Dear colleague at {{practice.name}},\n\nI saw your patient in clinic.\n\n[What was found and agreed]",
    };
}
