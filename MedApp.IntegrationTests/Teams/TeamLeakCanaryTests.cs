using System.Net.Http.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Teams;

/// <summary>
/// The canary (decision 205 of <c>docs/multi-clinic-plan.md</c>): team A's records carry marker text in their free text
/// (<see cref="TeamLeakTest"/>), and every read the API has (each GET, and the searches sent as POSTs), called with team
/// A's IDs where it takes one, must give back none of it to:
/// <list type="bullet">
/// <item><b>team B</b>: no marker at all, and none of team A's record IDs;</item>
/// <item><b>the lab</b>, which holds a grant on the pathway A passed it, but not on A's other: nothing private;</item>
/// <item><b>team C</b>, asked a consult on A's private pathway, after A closed it: nothing private, nothing the lab
/// holds.</item>
/// </list>
/// Administrator-only reads are left out (admins read every team's records, read only).
/// </summary>
[TestFixture]
public sealed class TeamLeakCanaryTests : TeamLeakTest
{
    /// <summary>The reads sent as POSTs (a search, or a find by number), with the body each takes.</summary>
    private static readonly Dictionary<string, Func<LeakWorld, object>> PostReads = new()
    {
        ["api/lab-cases/search"] = _ => new { },
        ["api/hospital-requests/search"] = _ => new { },
        ["api/specimens/search"] = _ => new { },
        ["api/specimens/find"] = w => new { number = w.SpecimenNumber },
        ["api/hospital-requests/patient-matches"] = _ => new { lastName = "Hospital" },
    };

    /// <summary>Not data: the app's pages, fallbacks, the live connection and health checks.</summary>
    private static readonly string[] NotData = ["{*path:nonfile}", "/api/{**path}", "/api/fhir/{**path}", "/health", "/alive", "/api/notifications/hub"];

    [Test]
    public async Task EveryRead_GivesBackNothing_ToAnotherTeam_TheLabBeforeAHandover_OrAConsultedTeamAfterwards()
    {
        var w = await SeedAsync();
        var reads = Reads(w).ToList();
        reads.Should().HaveCountGreaterThan(80);

        using (new AssertionScope())
        {
            foreach (var (key, url, body) in reads)
            {
                var b = await ReadAsync(w.B.Client, url, body);
                b.Should().NotContainAny([Private, LabShared, ConsultText, HospitalText], $"team B read {key}");
                var sent = GuidsIn(url);
                GuidsIn(b).Where(id => !sent.Contains(id)).Intersect(w.PrivateIds)
                    .Should().BeEmpty($"team B read {key}: it named team A's records");

                (await ReadAsync(w.Lab.Client, url, body)).Should().NotContainAny([Private, ConsultText], $"the lab read {key}");

                (await ReadAsync(w.C.Client, url, body)).Should().NotContainAny([Private, LabShared, HospitalText], $"team C read {key}");
            }
        }
    }

    [Test]
    public async Task TeamA_SeesItsOwnMarkers_SoTheCanaryCanSing()
    {
        var w = await SeedAsync();

        (await ReadAsync(w.A.Client, PathwayUrl(w.PrivatePathway.Id), null)).Should().Contain(Private);
        (await ReadAsync(w.Lab.Client, PathwayUrl(w.SharedPathway.Id), null)).Should().Contain(LabShared);
        (await ReadAsync(w.Lab.Client, $"/api/hospital-requests/{w.HospitalRequestId}", null)).Should().Contain(HospitalText);
        (await ReadAsync(w.C.Client, $"/api/referrals/{w.ReferralId}", null)).Should().NotContain(Private);
    }

    private static IEnumerable<(string Key, string Url, object? Body)> Reads(LeakWorld w)
    {
        var endpoints = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => !NotData.Contains(e.RoutePattern.RawText))
            .Where(e =>
            {
                var permissions = e.Metadata.GetOrderedMetadata<RequirePermissionAttribute>().Select(a => a.Permission).ToList();
                return permissions.Count == 0 || !permissions.All(p => Permissions.Describe(p).IsAdminOnly);
            });

        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            var isRead = methods.Contains("GET");
            var postRead = methods.Contains("POST") && PostReads.ContainsKey(pattern);
            if (!isRead && !postRead)
                continue;

            var url = WithTeamAIds(pattern, w);
            if (url is null)
                continue; // a setup record's page (a team, a site): not a team's clinical record

            if (isRead)
                yield return ($"GET {pattern}", url + Query(pattern, w), null);
            if (postRead)
                yield return ($"POST {pattern}", url, PostReads[pattern](w));
        }
    }

    private static string Query(string pattern, LeakWorld w)
    {
        var day = $"{NextWorkingDay():yyyy-MM-dd}";
        return pattern switch
        {
            "api/calendar" => $"?staffUserId={w.A.UserId}&from={day}&to={day}",
            "api/calendar/team" => $"?date={day}&teamId={w.TeamA}",
            "api/attachments" => $"?patientId={w.PatientId}",
            "api/pathways/patient-pathways/labels" => $"?ids={w.PrivatePathway.Id}",
            "api/patients/labels" => $"?ids={w.PatientId}",
            "api/clinics/sessions" => $"?from={UkToday:yyyy-MM-dd}&to={UkToday.AddDays(14):yyyy-MM-dd}",
            "api/appointments/search" => "?pageSize=100",
            "api/pathways/patient-pathways/search" => "?pageSize=100",
            "api/referrals/search" => "?pageSize=100",
            "api/letters/search" => "?pageSize=100",
            "api/results/search" => "?pageSize=100",
            "api/specimens/search" => "?pageSize=100",
            _ => string.Empty,
        };
    }

    private static async Task<string> ReadAsync(HttpClient client, string url, object? body)
    {
        var response = body is null ? await client.GetAsync(url) : await client.PostAsJsonAsync(url, body);
        return await response.Content.ReadAsStringAsync();
    }
}
