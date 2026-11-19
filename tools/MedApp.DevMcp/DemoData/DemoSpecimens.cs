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

    /// <summary>
    /// Where the stories send samples: the centre's own lab, which uses MedApp, and an invented partner lab, which doesn't
    /// (it takes longer, and now and then loses one). Only the stories' timing: MC13 dropped the labs list, so nothing
    /// is saved.
    /// </summary>
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
        var labs = Labs.Select(l => new DemoLaboratory(l.Name, l.UsesMedApp)).ToList();
        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            db.SpecimenTypes.AddRange(types);
            await db.SaveChangesAsync(ct);
        });

        return new SpecimenSetup(types, labs);
    }
}

/// <summary>A lab a story sends samples to (its timing only; not saved).</summary>
public sealed record DemoLaboratory(string Name, bool UsesMedApp);

/// <summary>The specimen types as saved, and the stories' labs (for the specimens the story takes).</summary>
public sealed record SpecimenSetup(IReadOnlyList<SpecimenType> Types, IReadOnlyList<DemoLaboratory> Labs)
{
    public SpecimenType Type(string name) => Types.Single(t => t.Name == name);

    public DemoLaboratory InHouse => Labs.Single(l => l.UsesMedApp);

    public DemoLaboratory Partner => Labs.Single(l => !l.UsesMedApp);
}
