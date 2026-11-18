using MedApp.Domain.Specimens;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The demo centre's specimen setup (<c>docs/centre-phases-3-5-plan.md</c>, section 2.7): the specimen types and labs
/// an admin set up when the centre started, so the Lab page and the Record Specimen dialog have something to offer.
/// The specimens themselves are taken in the pathways' stories (<see cref="PathwayReplay"/>), through the specimen rules.
/// </summary>
public static class DemoSpecimens
{
    /// <summary>The types, with the days each lab is expected to take. Invented, for a demo.</summary>
    public static readonly IReadOnlyList<(string Name, int Days)> Types =
    [
        ("Skin biopsy (punch)", 14),
        ("Skin excision", 21),
        ("Endometrial biopsy", 14),
        ("Urine", 3),
        ("Blood", 2),
    ];

    /// <summary>The centre's own lab, which uses MedApp, and an invented partner lab, which doesn't.</summary>
    public static readonly IReadOnlyList<(string Name, bool UsesMedApp)> Labs =
    [
        ("Centre pathology laboratory", true),
        ("Northgate partner laboratory", false),
    ];

    public static async Task<SpecimenSetup> CreateSetupAsync(SeedServices services, DemoTimeline timeline, CancellationToken ct)
    {
        var at = timeline.AppStartUtc.AddDays(-14);
        var types = Types.Select(t => new SpecimenType
        {
            Id = Guid.NewGuid(), Name = t.Name, ExpectedTurnaroundDays = t.Days, CreatedAt = at, LastUpdated = at,
        }).ToList();
        var labs = Labs.Select(l => new Laboratory
        {
            Id = Guid.NewGuid(), Name = l.Name, UsesMedApp = l.UsesMedApp, CreatedAt = at, LastUpdated = at,
        }).ToList();
        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            db.SpecimenTypes.AddRange(types);
            db.Laboratories.AddRange(labs);
            await db.SaveChangesAsync(ct);
        });

        return new SpecimenSetup(types, labs);
    }
}

/// <summary>The specimen types and labs as saved (for the specimens the story takes, which name them).</summary>
public sealed record SpecimenSetup(IReadOnlyList<SpecimenType> Types, IReadOnlyList<Laboratory> Labs)
{
    public SpecimenType Type(string name) => Types.Single(t => t.Name == name);

    public Laboratory InHouse => Labs.Single(l => l.UsesMedApp);

    public Laboratory Partner => Labs.Single(l => !l.UsesMedApp);
}
