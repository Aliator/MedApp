using FluentValidation;
using MediatR;
using MedApp.Domain.Teams;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The seed's working state: the app's services (with the clock and actor it moves), the story's moments, its people,
/// and the IDs of what it has made so far, by name. <see cref="Send{T}"/> replays one request through the app's own
/// handler, as someone at a moment: the handler checks what it would check for them (permissions, team walls, gates).
/// </summary>
public sealed class DemoSeed(SeedServices services, DemoTimeline timeline, IReadOnlyList<DemoPerson> people, CancellationToken ct)
{
    public SeedServices Services { get; } = services;
    public DemoTimeline Timeline { get; } = timeline;
    public IReadOnlyList<DemoPerson> People { get; } = people;
    public CancellationToken Ct { get; } = ct;

    /// <summary>The teams by name (the lab's is <see cref="Team.LabName"/>).</summary>
    public Dictionary<string, Guid> Teams { get; } = new() { [Team.LabName] = Team.LabId };

    public Dictionary<string, Guid> Definitions { get; } = [];
    public Dictionary<string, Guid> Templates { get; } = [];
    public Dictionary<string, Guid> Patients { get; } = [];
    public Dictionary<string, Guid> Practices { get; } = [];
    public Dictionary<string, Guid> Sites { get; } = [];
    public Dictionary<string, Guid> Rooms { get; } = [];
    public Dictionary<string, Guid> ActivityTypes { get; } = [];
    public Dictionary<string, Guid> SpecimenTypes { get; } = [];
    public Dictionary<string, Guid> LabTests { get; } = [];
    public Dictionary<string, Guid> ConsentForms { get; } = [];
    public Dictionary<string, Guid> LetterTemplates { get; } = [];
    public Dictionary<string, Guid> Hospitals { get; } = [];

    /// <summary>The pathways the story replayed, by patient (and which, for a patient with two).</summary>
    public Dictionary<string, StoryPathway> Stories { get; } = [];

    /// <summary>Counts for the summary, by what they count.</summary>
    public Dictionary<string, int> Counts { get; } = [];

    /// <summary>The records the tour opens, by what they show.</summary>
    public Dictionary<string, Guid> Tour { get; } = [];

    public Guid TeamId(string name) => Teams[name];

    /// <summary>
    /// Sends a request through the app's handler as <paramref name="by"/> at <paramref name="at"/>, naming what it was
    /// for if the app refuses it (with the app's own words).
    /// </summary>
    public async Task<T> Send<T>(DateTime at, DemoPerson by, IRequest<T> request, string what)
    {
        Services.At(at, by.ActivityActor);
        try
        {
            return await Services.Run(sp => sp.GetRequiredService<IMediator>().Send(request, Ct));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw Refused(e, what, at, by);
        }
    }

    public async Task Send(DateTime at, DemoPerson by, IRequest request, string what)
    {
        Services.At(at, by.ActivityActor);
        try
        {
            await Services.Run(sp => sp.GetRequiredService<IMediator>().Send(request, Ct));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw Refused(e, what, at, by);
        }
    }

    /// <summary>Reads (or writes, where no command does it) the database directly.</summary>
    public Task<T> Db<T>(Func<MedAppDbContext, Task<T>> work) =>
        Services.Run(sp => work(sp.GetRequiredService<MedAppDbContext>()));

    public Task Db(Func<MedAppDbContext, Task> work) =>
        Services.Run(sp => work(sp.GetRequiredService<MedAppDbContext>()));

    private static McpException Refused(Exception e, string what, DateTime at, DemoPerson by)
    {
        var why = e is ValidationException validation
            ? string.Join("; ", validation.Errors.Select(x => $"{x.PropertyName}: {x.ErrorMessage}"))
            : $"{e.GetType().Name}: {e.Message}";
        return new McpException($"The demo data couldn't {what} (as {by.Username} at {at:yyyy-MM-dd HH:mm} UTC): {why}", e);
    }
}
