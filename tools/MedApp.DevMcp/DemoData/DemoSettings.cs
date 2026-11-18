using System.Text.Json;
using MediatR;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.Application.Pathways.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// App settings saved with accounts, in the web app's own keys and shapes (<c>src/app/settingsStore.ts</c>: each a
/// string under a <c>medapp.</c> key): the lead's rows per page, saved searches on the pathways and patients lists
/// (one the pathways list starts on, and "Waiting for histology" by the stage reached; stored under the lists' old
/// keys, procedures), and pins; saved searches for two members of staff. Saved through the
/// app's command.
/// </summary>
public static class DemoSettings
{
    public static async Task<object> CreateAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        var timeline = world.Timeline;
        var lead = world.Lead;
        var today = timeline.Today;
        var skinLesion = world.TemplateIds[PathwayKind.SkinLesion].ToString();
        var prostate = world.TemplateIds[PathwayKind.Prostate].ToString();
        var bleeding = world.TemplateIds[PathwayKind.PostMenopausalBleeding].ToString();
        // The stage reached, by its stage definition, as the pathways list's URL keeps it (lower case).
        var histology = world.Definitions[DemoPathwayKinds.Histology].Id.ToString();
        var deleted = world.Deletions.Select(d => d.Pathway.Id).ToHashSet();

        var mine = world.Pathways
            .Where(p => !deleted.Contains(p.Id) && PathwayWorkflow.IsOpen(p.Status) && p.Assignments.Any(a => a.UserId == lead.Id))
            .OrderByDescending(p => p.LastUpdated)
            .ToList();

        var myWork = Favourite("My open work", new() { ["mine"] = true });
        var leadSettings = new Dictionary<string, string>
        {
            ["medapp.pageSize"] = "25",
            ["medapp.favourites"] = Json(new Dictionary<string, object>
            {
                ["procedures"] = new
                {
                    saved = new[]
                    {
                        myWork,
                        Favourite("Urgent skin referrals", new() { ["templateId"] = skinLesion, ["priority"] = "Urgent" }),
                        Favourite("Overdue prostate assessments", new() { ["templateId"] = prostate, ["overdue"] = true }),
                        Favourite("Blocked", new() { ["status"] = "Blocked" }),
                        Favourite("Waiting for histology", new() { ["stage"] = histology }),
                    },
                    startId = myWork.id,
                },
                ["patients"] = new
                {
                    saved = new[]
                    {
                        Favourite("75 and over", new() { ["dobTo"] = today.AddYears(-75).ToString("yyyy-MM-dd") }),
                        Favourite("Children", new() { ["dobFrom"] = today.AddYears(-18).AddDays(1).ToString("yyyy-MM-dd") }),
                    },
                },
            }),
            ["medapp.pins"] = Json(new
            {
                patients = mine.Select(p => p.PatientId).Distinct().Take(2).Select(id => id.ToString()).ToArray(),
                pathways = mine.Take(1).Select(p => p.Id.ToString()).ToArray(),
            }),
        };

        var coordinator = world.Coordinator;
        var coordinatorSettings = new Dictionary<string, string>
        {
            ["medapp.favourites"] = Json(new Dictionary<string, object>
            {
                ["procedures"] = new
                {
                    saved = new[]
                    {
                        Favourite("Skin lesions in progress", new() { ["templateId"] = skinLesion, ["status"] = "InProgress" }),
                        Favourite("Urgent bleeding referrals", new() { ["templateId"] = bleeding, ["priority"] = "Urgent" }),
                    },
                },
            }),
        };

        var dermatologist = world.Member(DemoStaff.DermatologistUsername);
        var dermatologistSettings = new Dictionary<string, string>
        {
            ["medapp.pageSize"] = "50",
            ["medapp.favourites"] = Json(new Dictionary<string, object>
            {
                ["procedures"] = new { saved = new[] { Favourite("My overdue", new() { ["mine"] = true, ["overdue"] = true }) } },
            }),
        };

        foreach (var (member, settings, daysAgo) in new[] { (lead, leadSettings, 6), (coordinator, coordinatorSettings, 20), (dermatologist, dermatologistSettings, 33) })
        {
            services.At(DemoTimeline.WorkingMoment(timeline.NowUtc.AddDays(-daysAgo), 9, 5), member.ActivityActor);
            await services.Run(sp => sp.GetRequiredService<IMediator>().Send(new SaveMySettingsCommand(member.Id, Json(settings)), ct));
        }

        return new { savedFor = new[] { lead.Username, coordinator.Username, dermatologist.Username }, leadPins = mine.Count > 0 };

        static FavouriteSeed Favourite(string name, Dictionary<string, object> values) => new(Guid.NewGuid().ToString(), name, values);
    }

    private sealed record FavouriteSeed(string id, string name, Dictionary<string, object> values);

    private static string Json(object value) => JsonSerializer.Serialize(value);
}
