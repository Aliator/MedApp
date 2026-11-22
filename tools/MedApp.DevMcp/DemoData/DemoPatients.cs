using MedApp.Application.Patients.Commands.CreatePatient;

namespace MedApp.DevMcp.DemoData;

/// <summary>A patient as the light demo has them: synthetic name, date of birth, 999-range NHS number and house number.</summary>
public sealed record DemoPatient(string FirstName, string LastName, DateOnly DateOfBirth, string NhsNumber, int House, string? Practice = null)
{
    public string Name => $"{FirstName} {LastName}";
}

/// <summary>
/// The clinic's patients (the light demo's <c>PATIENT_SPECS</c>, with their GP practices from its setup slice), registered
/// by the booking coordinator before their first referral or pathway. NHS numbers come only from the 999 range (never
/// issued to real people), each with a valid check digit. The hospital's two patients come with its requests
/// (<see cref="DemoLab"/>).
/// </summary>
public static class DemoPatients
{
    public const string EllisVarden = "Ellis Varden";
    public const string PriyaHolloway = "Priya Holloway";
    public const string SamuelOkafor = "Samuel Okafor";
    public const string GraceAbernethy = "Grace Abernethy";
    public const string TobiasLund = "Tobias Lund";
    public const string IlonaPetrova = "Ilona Petrova";
    public const string OliverMaddox = "Oliver Maddox";
    public const string AishaKhoury = "Aisha Khoury";
    public const string CallumAshby = "Callum Ashby";
    public const string HarrietQuill = "Harriet Quill";
    public const string FelixMoreton = "Felix Moreton";
    public const string NoraFennick = "Nora Fennick";
    public const string LeahCorrin = "Leah Corrin";
    public const string EdithMarlow = "Edith Marlow";
    public const string FreyaTolland = "Freya Tolland";
    public const string HamishDewar = "Hamish Dewar";
    public const string RuthPenhaligon = "Ruth Penhaligon";

    public static readonly IReadOnlyList<DemoPatient> All =
    [
        new("Ellis", "Varden", new DateOnly(1958, 3, 14), "9991001379", 10, DemoSetup.ExampleLane),
        new("Priya", "Holloway", new DateOnly(1971, 9, 2), "9991004114", 11, DemoSetup.ExampleLane),
        new("Samuel", "Okafor", new DateOnly(1989, 12, 21), "9991006850", 12),
        new("Grace", "Abernethy", new DateOnly(1979, 4, 8), "9991010963", 15),
        new("Tobias", "Lund", new DateOnly(1994, 11, 25), "9991012338", 16, DemoSetup.HarbourView),
        new("Ilona", "Petrova", new DateOnly(1983, 7, 12), "9991013709", 17),
        new("Oliver", "Maddox", new DateOnly(1961, 2, 3), "9991015078", 18, DemoSetup.HarbourView),
        new("Aisha", "Khoury", new DateOnly(1975, 10, 19), "9991016449", 19),
        new("Callum", "Ashby", new DateOnly(1970, 5, 5), "9991020551", 21),
        new("Harriet", "Quill", new DateOnly(1949, 1, 22), "9991022007", 22, DemoSetup.ExampleLane),
        new("Felix", "Moreton", new DateOnly(1968, 3, 9), "9991023372", 23),
        new("Nora", "Fennick", new DateOnly(1990, 6, 14), "9991041818", 24),
        new("Leah", "Corrin", new DateOnly(1986, 8, 30), "9991051376", 25),
        // The Eye clinic's (the owner, 20 November 2026).
        new("Edith", "Marlow", new DateOnly(1944, 6, 17), "9991060006", 26),
        new("Freya", "Tolland", new DateOnly(1992, 2, 11), "9991061371", 27),
        new("Hamish", "Dewar", new DateOnly(1950, 9, 3), "9991062742", 28),
        new("Ruth", "Penhaligon", new DateOnly(1966, 12, 29), "9991064117", 29, DemoSetup.ExampleLane),
    ];

    /// <summary>The hospital's patients, whom only its requests link to the centre (MC18).</summary>
    public static readonly DemoPatient WilfredAshdown = new("Wilfred", "Ashdown", new DateOnly(1947, 11, 3), "9991031375", 40);
    public static readonly DemoPatient MayaThistlewood = new("Maya", "Thistlewood", new DateOnly(1986, 5, 21), "9991032746", 41);

    /// <summary>Every NHS number the demo uses (all from the 999 range, each with a valid check digit).</summary>
    public static IEnumerable<string> NhsNumbers => All.Append(WilfredAshdown).Append(MayaThistlewood).Select(p => p.NhsNumber);

    /// <summary>Registers the clinic's patients through the patient command, as the booking coordinator did.</summary>
    public static async Task CreateAsync(DemoSeed seed)
    {
        var at = seed.Timeline.Ago(50, 9, 0);
        foreach (var patient in All)
        {
            at = at.AddMinutes(7);
            var made = await seed.Send(at, DemoPeople.Marsh, new CreatePatientCommand(
                patient.FirstName,
                patient.LastName,
                patient.DateOfBirth,
                $"{patient.FirstName}.{patient.LastName}@example.com".ToLowerInvariant(),
                patient.NhsNumber,
                [$"{patient.House} Sample Road", "Exampleton"],
                PracticeId: patient.Practice is { } practice ? seed.Practices[practice] : null), $"register {patient.Name}");
            seed.Patients[patient.Name] = made.Id;
        }
    }
}
