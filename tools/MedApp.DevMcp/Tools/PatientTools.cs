using System.ComponentModel;
using System.Globalization;
using MediatR;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>Patient records. This is a medical app: only ever synthetic data, never real people.</summary>
[McpServerToolType]
public sealed class PatientTools
{
    [McpServerTool(Name = "list_patients", ReadOnly = true, Idempotent = true)]
    [Description("Lists patients sorted by last then first name, optionally filtered by a name or email fragment. Returns ids for use with the app's /patients/{id} routes.")]
    public static async Task<object> ListPatients(
        MedAppDbContext db,
        [Description("Case-insensitive fragment of first name, last name or email.")] string? search = null,
        [Description("Maximum rows, 1-200.")] int take = 25,
        CancellationToken ct = default)
    {
        var query = db.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.FirstName.Contains(search) || p.LastName.Contains(search) || p.Email.Contains(search));

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
            .Take(Math.Clamp(take, 1, 200))
            .Select(p => new { p.Id, p.FirstName, p.LastName, dateOfBirth = p.DateOfBirth, p.Email, p.CreatedAt })
            .ToListAsync(ct);
        return new { total, shown = rows.Count, patients = rows };
    }

    [McpServerTool(Name = "create_patient")]
    [Description("Creates one patient through the app's CreatePatient command, so the API's validation rules apply. SYNTHETIC DATA ONLY: invented names and example.com emails, never a real person.")]
    public static async Task<object> CreatePatient(
        IMediator mediator,
        [Description("First name (synthetic).")] string firstName,
        [Description("Last name (synthetic).")] string lastName,
        [Description("Date of birth, yyyy-MM-dd.")] string dateOfBirth,
        [Description("Email, e.g. someone@example.com (synthetic).")] string email,
        CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            throw new McpException("dateOfBirth must be yyyy-MM-dd.");
        return await Guards.Validated(() => mediator.Send(new CreatePatientCommand(firstName, lastName, dob, email), ct));
    }

    [McpServerTool(Name = "seed_patients")]
    [Description("Adds synthetic patients (invented names, dates of birth 1930-2020, @example.com emails) through the CreatePatient command. The same seed gives the same people.")]
    public static async Task<object> SeedPatients(
        IMediator mediator,
        [Description("How many to add, 1-200.")] int count = 10,
        [Description("Random seed, for repeatable data. Omit for a random one.")] int? seed = null,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 200);
        var usedSeed = seed ?? Random.Shared.Next();
        var created = new List<object>();
        foreach (var p in SyntheticPatients.Generate(count, usedSeed))
        {
            var response = await Guards.Validated(() =>
                mediator.Send(new CreatePatientCommand(p.FirstName, p.LastName, p.DateOfBirth, p.Email), ct));
            created.Add(response);
        }
        return new { seed = usedSeed, created = created.Count, patients = created };
    }

    [McpServerTool(Name = "delete_patient", Destructive = true)]
    [Description("Deletes one patient by id through the app's DeletePatient command. Needs confirm: true.")]
    public static async Task<object> DeletePatient(
        IMediator mediator,
        [Description("Patient id (from list_patients).")] Guid id,
        [Description("Must be true to delete.")] bool confirm = false,
        CancellationToken ct = default)
    {
        Guards.RequireConfirm(confirm, $"Deleting patient {id}");
        var deleted = await Guards.Validated(() => mediator.Send(new DeletePatientCommand(id), ct));
        if (!deleted) throw new McpException($"No patient with id {id}.");
        return new { id, deleted = true };
    }
}

/// <summary>Invented, generic names only. Emails use the reserved example.com domain (RFC 2606).</summary>
public static class SyntheticPatients
{
    private static readonly string[] FirstNames =
    [
        "Alex", "Billie", "Casey", "Dana", "Eden", "Finley", "Gray", "Harper", "Indy", "Jules", "Kai", "Logan",
        "Morgan", "Noel", "Oakley", "Parker", "Quinn", "Reese", "Sage", "Taylor", "Umi", "Val", "Wren", "Yael", "Zion",
    ];

    private static readonly string[] LastNames =
    [
        "Ashford", "Brightwell", "Calloway", "Dunmore", "Elsworth", "Fairbanks", "Greenhill", "Hartwell", "Ironside",
        "Juniper", "Kingsley", "Larkspur", "Merriweather", "Northcott", "Oakhurst", "Pemberton", "Quayle", "Rowntree",
        "Stonebridge", "Thistlewood", "Underhill", "Vexley", "Whitmore", "Yardley", "Zephyr",
    ];

    public static IEnumerable<(string FirstName, string LastName, DateOnly DateOfBirth, string Email)> Generate(int count, int seed)
    {
        var random = new Random(seed);
        var start = new DateOnly(1930, 1, 1).DayNumber;
        var end = new DateOnly(2020, 12, 31).DayNumber;
        for (var i = 0; i < count; i++)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            var dob = DateOnly.FromDayNumber(random.Next(start, end + 1));
            var email = $"{first}.{last}.{random.Next(1000, 10000)}@example.com".ToLowerInvariant();
            yield return (first, last, dob, email);
        }
    }
}
