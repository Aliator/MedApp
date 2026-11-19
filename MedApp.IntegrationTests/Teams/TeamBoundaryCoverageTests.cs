using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Teams;

/// <summary>
/// The team boundary's coverage (decision 205 of <c>docs/multi-clinic-plan.md</c>): every endpoint the API maps is
/// classified <see cref="Kind.CentreWide"/> (setup, patients' demographics, the caller's own account),
/// <see cref="Kind.TeamScoped"/> (a team's clinical records: probed), <see cref="Kind.AdminOnly"/> (every permission it
/// names is Administrator-only: found from the endpoint itself) or <see cref="Kind.NotData"/>. An endpoint nobody
/// classified fails, as does a team-scoped one the probe can't reach.
/// <para>
/// <b>The probe:</b> a member of team B holding every permission staff can hold calls every team-scoped endpoint with
/// team A's records (<see cref="TeamLeakTest.WithTeamAIds"/>, and a body naming them where one is needed): a read of one
/// of A's records is a 404; nothing names A's records or carries their free text; and afterwards none of A's rows has
/// changed (<see cref="TeamLeakTest.FingerprintAsync"/>), whatever each change answered.
/// </para>
/// </summary>
[TestFixture]
public sealed class TeamBoundaryCoverageTests : TeamLeakTest
{
    private enum Kind { CentreWide, TeamScoped, AdminOnly, NotData }

    /// <summary>Each area by route prefix (the longest that matches wins), with why it's classified so.</summary>
    private static readonly (string Prefix, Kind Kind, string Why)[] Areas =
    [
        // A team's clinical records.
        ("api/pathways/patient-pathways", Kind.TeamScoped, "pathways: walled by team and grants"),
        ("api/pathways/note-deletion-requests", Kind.TeamScoped, "a note's deletion request: its pathway's walls"),
        ("api/pathways/{id:guid}", Kind.TeamScoped, "a pathway's consent records and samples"),
        ("api/pathways/insights", Kind.TeamScoped, "Insights: counts of the caller's teams' pathways"),
        ("api/patients/{id:guid}/timeline", Kind.TeamScoped, "the timeline: only what the caller's teams may see"),
        ("api/appointments", Kind.TeamScoped, "appointments: walled by team"),
        ("api/attachments", Kind.TeamScoped, "files: walled by their pathway's or referral's team"),
        ("api/consent-records", Kind.TeamScoped, "consent: its pathway's walls"),
        ("api/consults", Kind.TeamScoped, "consults: the asking team, and the asked team while open"),
        ("api/hospital-requests", Kind.TeamScoped, "hospital requests: the lab's"),
        ("api/lab-cases", Kind.TeamScoped, "lab cases: the lab's, and the asking clinic's through its pathway"),
        ("api/letters", Kind.TeamScoped, "letters: walled by team"),
        ("api/mdt", Kind.TeamScoped, "MDT lists: the MDT's team, and attendees' grants"),
        ("api/referrals", Kind.TeamScoped, "referrals: walled by team"),
        ("api/results", Kind.TeamScoped, "results: walled by team"),
        ("api/specimens", Kind.TeamScoped, "samples: the lab, and the asking clinic"),
        ("api/calendar", Kind.TeamScoped, "calendars: another team's appointments are only busy time"),
        ("api/calendar/team", Kind.TeamScoped, "the team calendar: another team's appointments are only busy time"),
        ("api/clinics/sessions/{id:guid}", Kind.TeamScoped, "a clinic session's slots: another team's appointments are only busy time"),

        // Patients are centre-wide (decision 183): demographics, and another team's pathways only as a summary.
        ("api/patients", Kind.CentreWide, "patients: centre-wide demographics, other teams' pathways only summarised"),
        ("api/fhir", Kind.CentreWide, "FHIR Patient: demographics"),
        ("api/demographics", Kind.CentreWide, "the demographics lookup: one patient's details from PDS"),

        // Setup and staff's own things.
        ("api/pathways/patient-pathway-stage", Kind.CentreWide, "the stage library and templates: configuration"),
        ("api/teams", Kind.CentreWide, "teams: configuration"),
        ("api/practices", Kind.CentreWide, "GP practices: organisations"),
        ("api/hospitals", Kind.CentreWide, "hospitals: organisations"),
        ("api/specimen-types", Kind.CentreWide, "specimen types: configuration"),
        ("api/lab-tests", Kind.CentreWide, "the lab's tests: configuration"),
        ("api/consent-forms", Kind.CentreWide, "consent forms: configuration"),
        ("api/letter-templates", Kind.CentreWide, "letter templates: an organisation's wording"),
        ("api/sites", Kind.CentreWide, "sites: configuration"),
        ("api/rooms", Kind.CentreWide, "rooms: configuration"),
        ("api/clinics", Kind.CentreWide, "clinic sessions and patterns: staff time, no patients"),
        ("api/calendar/activity-types", Kind.CentreWide, "activity types: configuration"),
        ("api/calendar/closed-days", Kind.CentreWide, "closed days: configuration and counts"),
        ("api/calendar/day-templates", Kind.CentreWide, "day templates: staff time"),
        ("api/calendar/week-templates", Kind.CentreWide, "week templates: staff time"),
        ("api/calendar/staff", Kind.CentreWide, "staff, their hours and templates: no patients"),
        ("api/calendar/blocks", Kind.CentreWide, "a member of staff's blocks: staff time"),
        ("api/calendar/meetings", Kind.CentreWide, "staff meetings (other teams' shown only as busy): no patients"),
        ("api/calendar/availability", Kind.CentreWide, "free and busy times: no patients"),
        ("api/monitoring", Kind.CentreWide, "the caller's own monitoring rules"),
        ("api/notifications", Kind.CentreWide, "the caller's own notifications"),
        ("api/me", Kind.CentreWide, "the caller's own notepad"),
        ("api/auth", Kind.CentreWide, "the caller's own account, sign-in, and staff accounts"),
        ("api/permissions", Kind.CentreWide, "the permission catalogue"),
        ("api/roles", Kind.CentreWide, "roles: configuration"),
        ("api/jobs", Kind.NotData, "the scheduled jobs' state: names, times and counts"),

        // Not data.
        ("api/notifications/hub", Kind.NotData, "the live notifications connection"),
        ("/api/notifications/hub", Kind.NotData, "the live notifications connection"),
        ("{*path:nonfile}", Kind.NotData, "the app's pages"),
        ("/api/{**path}", Kind.NotData, "an /api path matching nothing: a 404"),
        ("/api/fhir/{**path}", Kind.NotData, "a FHIR path matching nothing: a 404"),
        ("/health", Kind.NotData, "health check"),
        ("/alive", Kind.NotData, "health check"),
    ];

    /// <summary>Endpoints that read nothing of a team's: the demo's statuses and counts for the side nav.</summary>
    private static readonly Dictionary<string, Kind> Overrides = new()
    {
        ["GET api/appointments/patient-messages/status"] = Kind.NotData,
        ["GET api/fhir/metadata"] = Kind.NotData,
        ["GET api/demographics/status"] = Kind.NotData,
    };

    /// <summary>Default queries for team-scoped reads that need one.</summary>
    private static string Query(string key, LeakWorld w)
    {
        var day = $"{NextWorkingDay():yyyy-MM-dd}";
        return key switch
        {
            "GET api/calendar" => $"?staffUserId={w.A.UserId}&from={day}&to={day}",
            "GET api/calendar/team" => $"?date={day}&teamId={w.TeamA}",
            "GET api/attachments" => $"?patientId={w.PatientId}",
            "POST api/attachments" => $"?pathwayId={w.PrivatePathway.Id}",
            "GET api/pathways/patient-pathways/labels" => $"?ids={w.PrivatePathway.Id}",
            _ => string.Empty,
        };
    }

    /// <summary>A body naming team A's records, for the changes that take one (others get an empty one).</summary>
    private static object? Body(string key, LeakWorld w)
    {
        var start = UkAt(NextWorkingDay(3), 10);
        return key switch
        {
            "POST api/appointments" => new
            {
                patientId = w.PatientId, pathwayId = w.PrivatePathway.Id, stageId = w.PrivatePathway.Stages[0].Id,
                staffUserId = w.B.UserId, startUtc = start, endUtc = start.AddMinutes(30),
            },
            "POST api/appointments/past" => new
            {
                patientId = w.PatientId, pathwayId = w.PrivatePathway.Id, stageId = w.PrivatePathway.Stages[0].Id,
                staffUserId = w.B.UserId, startUtc = Now.AddDays(-1), endUtc = Now.AddDays(-1).AddMinutes(30), status = "Attended",
            },
            "POST api/consults" => new { pathwayId = w.PrivatePathway.Id, askedTeamId = w.TeamB, question = "Probe question." },
            "POST api/letters" => new { templateId = Guid.Empty, pathwayId = w.PrivatePathway.Id },
            "POST api/mdt/items" => new { pathwayId = w.PrivatePathway.Id, meetingId = w.MeetingId, question = "Probe question." },
            "POST api/pathways/patient-pathways" => new
            {
                patientId = w.PatientId, templateId = w.TemplateId, title = "Probe", notes = string.Empty,
                dueDate = UkToday.AddDays(7), priority = "Medium",
            },
            "POST api/results/external" => new
            {
                attachmentId = w.AttachmentId, title = "Probe", pathwayId = w.PrivatePathway.Id, responsibleUserId = w.B.UserId,
            },
            "POST api/specimens/find" => new { number = w.SpecimenNumber },
            "POST api/lab-cases" => new { specimenIds = new[] { w.SpecimenId } },
            "POST api/pathways/patient-pathways/{id:guid}/notes" => new { text = "Probe note." },
            "PUT api/pathways/patient-pathways/{id:guid}/stages/{stageId:guid}/notes" => new { notes = "Probe stage notes." },
            "POST api/pathways/patient-pathways/{id:guid}/status" => new { status = "Cancelled", reason = "Probe." },
            "PATCH api/pathways/patient-pathways/{id:guid}" => new { title = "Probe" },
            "POST api/referrals/{id:guid}/withdraw" => new { reason = "Probe.", expectedVersion = 1 },
            "POST api/appointments/{id:guid}/cancel" => new { reason = "Probe." },
            "PUT api/appointments/{id:guid}/notes" => new { notes = "Probe notes." },
            "POST api/consults/{id:guid}/reply" => new { reply = "Probe reply.", expectedVersion = 3 },
            "POST api/mdt/items/{id:guid}/withdraw" => new { expectedVersion = 1 },
            "POST api/specimens/{id:guid}/lost" => new { detail = "Probe.", expectedVersion = 3 },
            _ => new { },
        };
    }

    [Test]
    public void EveryEndpoint_IsClassified()
    {
        var unclassified = Endpoints().Where(e => ClassOf(e) is null).Select(e => e.Key).ToList();

        unclassified.Should().BeEmpty("every endpoint must say whether it holds a team's records (add it to Areas)");
    }

    [Test]
    public async Task EveryTeamScopedEndpoint_AsAnotherTeam_GivesNothingOfTeamA_AndChangesNothing()
    {
        var w = await SeedAsync();
        var before = await FingerprintAsync(w);
        var probed = 0;

        using (new AssertionScope())
        {
            foreach (var endpoint in Endpoints().Where(e => ClassOf(e) == Kind.TeamScoped))
            {
                var url = WithTeamAIds(endpoint.Pattern, w);
                url.Should().NotBeNull($"{endpoint.Key} needs a probe: add its parameters to WithTeamAIds");
                if (url is null)
                    continue;

                url += Query(endpoint.Key, w);
                var response = await SendAsync(w.B.Client, endpoint.Method, url, Body(endpoint.Key, w));
                var text = await response.Content.ReadAsStringAsync();
                probed++;

                var body = Body(endpoint.Key, w);
                w.LeakedIn(text, url, System.Text.Json.JsonSerializer.Serialize(body))
                    .Should().BeEmpty($"{endpoint.Key} as team B named team A's records");
                text.Should().NotContainAny([Private, LabShared, ConsultText, HospitalText], endpoint.Key);

                // A read of one of team A's records is a 404 (a patient's timeline and a clinic session are centre-wide
                // pages that show B only its own, and another team's visits as busy time).
                var readsOne = endpoint.Method == "GET" && GuidsIn(url.Split('?')[0]).Overlaps(w.PrivateIds)
                               && !endpoint.Pattern.StartsWith("api/patients/") && !endpoint.Pattern.StartsWith("api/clinics/");
                if (readsOne)
                    response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{endpoint.Key} read team A's record as team B");
            }
        }

        probed.Should().BeGreaterThan(100);
        (await FingerprintAsync(w)).Should().Be(before, "no probe as team B may change team A's records");
    }

    private sealed record Endpoint(string Method, string Pattern, IReadOnlyList<Permission> Permissions)
    {
        public string Key => $"{Method} {Pattern}";
    }

    private static IEnumerable<Endpoint> Endpoints() =>
        Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => new Endpoint(
                    method,
                    e.RoutePattern.RawText ?? string.Empty,
                    e.Metadata.GetOrderedMetadata<RequirePermissionAttribute>().Select(a => a.Permission).ToList())))
            .DistinctBy(e => e.Key)
            .OrderBy(e => e.Pattern, StringComparer.Ordinal)
            .ThenBy(e => e.Method, StringComparer.Ordinal);

    private static Kind? ClassOf(Endpoint endpoint)
    {
        if (Overrides.TryGetValue(endpoint.Key, out var kind))
            return kind;
        if (endpoint.Permissions.Count > 0 && endpoint.Permissions.All(p => Permissions.Describe(p).IsAdminOnly))
            return Kind.AdminOnly;

        var pattern = endpoint.Pattern;
        var matches = Areas
            .Where(a => pattern == a.Prefix || pattern.StartsWith(a.Prefix + "/", StringComparison.Ordinal))
            .OrderByDescending(a => a.Prefix.Length)
            .ToList();
        return matches.Count == 0 ? null : matches[0].Kind;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url, object? body)
    {
        if (method == "POST" && url.StartsWith("/api/attachments") && !url.Contains("/api/attachments/"))
            return UploadAttachmentAsync(client, url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : $"patientId={Guid.Empty}", SyntheticPdf, "probe.pdf");

        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is not ("GET" or "DELETE"))
            request.Content = JsonContent.Create(body ?? new { });
        return client.SendAsync(request);
    }
}
