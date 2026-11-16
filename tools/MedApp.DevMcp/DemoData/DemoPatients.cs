using System.Globalization;
using MediatR;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Domain.Patients;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>Why a patient is in the demo, beyond their procedures.</summary>
public enum PatientRole
{
    /// <summary>Referred, with procedures.</summary>
    Active,

    /// <summary>Registered, waiting for a referral: no procedures yet.</summary>
    Waiting,

    /// <summary>Imported from the older system, with closed records long past: due for the retention review.</summary>
    ImportedDue,

    /// <summary>Imported, with closed records, but not due for review yet.</summary>
    ImportedNotDue,
}

/// <summary>A synthetic patient: invented name, example.com email, and an NHS number from the 999 test range or none.</summary>
public sealed class PatientSeed
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string Email { get; init; }
    public string? NhsNumber { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public DateTime LastUpdatedUtc { get; set; }
    public PatientRole Role { get; init; }
    public Guid Id { get; set; }
    public int Procedures { get; set; }
    public bool Restricted { get; set; }
    public string FullName => $"{FirstName} {LastName}";

    public bool IsChildAt(DateTime utc) => AgeAt(utc) < 18;

    public int AgeAt(DateTime utc)
    {
        var on = DateOnly.FromDateTime(utc);
        var age = on.Year - DateOfBirth.Year;
        return on < DateOfBirth.AddYears(age) ? age - 1 : age;
    }
}

/// <summary>The clinic's patients: invented people, created through the app's own command.</summary>
public sealed class DemoPatients(DemoTimeline timeline, Random random)
{
    private static readonly string[] FirstNames =
    [
        "Olivia", "Amelia", "Isla", "Ava", "Mia", "Grace", "Sophia", "Freya", "Evie", "Ruby", "Ella", "Chloe", "Priya",
        "Aisha", "Zara", "Maryam", "Niamh", "Siobhan", "Margaret", "Patricia", "Susan", "Janet", "Eileen", "Joan",
        "Oliver", "George", "Harry", "Noah", "Jack", "Leo", "Arthur", "Oscar", "Muhammad", "Ibrahim", "Arjun", "Rohan",
        "Kwame", "Tomasz", "Callum", "Rhys", "Ewan", "David", "Michael", "Peter", "Brian", "Kenneth", "Graham", "Derek",
        "Hazel", "Iris", "Maureen", "Sheila", "Barbara", "Lorna", "Yusuf", "Bilal", "Stanley", "Roy", "Wendy", "Carol",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Patel", "Robinson", "Wright",
        "Thompson", "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke", "Khan", "Ahmed",
        "Hughes", "Edwards", "Turner", "Murphy", "O'Brien", "Kowalski", "Mensah", "Begum", "Ali", "Campbell", "Stewart",
        "Morgan", "Price", "Bennett", "Shaw", "Lloyd", "Harrison", "Chapman", "Kaur", "Singh", "Nowak", "Okafor", "Reid",
        "Fletcher", "Gill", "Holt", "Marsden", "Pickering", "Quinn", "Sutton", "Varley", "Whitfield", "Yates",
    ];

    private readonly HashSet<string> _emails = [];
    private int _nhsIndex;

    public List<PatientSeed> All { get; } = [];

    /// <summary>A new patient registered just before <paramref name="referredUtc"/>; a child only if <paramref name="mayBeChild"/>.</summary>
    public PatientSeed NewPatient(DateTime referredUtc, bool mayBeChild, PatientRole role = PatientRole.Active)
    {
        var age = mayBeChild && random.NextDouble() < 0.12
            ? random.Next(5, 17)
            : random.NextDouble() switch
            {
                < 0.25 => random.Next(18, 45),
                < 0.62 => random.Next(45, 70),
                _ => random.Next(70, 94),
            };
        var created = DemoTimeline.InWorkingHours(referredUtc.AddDays(-random.Next(0, 22)).AddMinutes(-random.Next(0, 400)), random);
        if (created > referredUtc)
            created = referredUtc.AddMinutes(-random.Next(10, 60));
        var dob = DateOnly.FromDateTime(referredUtc).AddYears(-age).AddDays(-random.Next(1, 360));
        return Add(dob, created, role);
    }

    private PatientSeed Add(DateOnly dateOfBirth, DateTime createdUtc, PatientRole role)
    {
        var first = FirstNames[random.Next(FirstNames.Length)];
        var last = LastNames[random.Next(LastNames.Length)];
        string email;
        do email = $"{Slug(first)}.{Slug(last)}{random.Next(10, 100)}@example.com";
        while (!_emails.Add(email));

        // Most have an NHS number, from the 999 test range only (never issued to anyone real); some don't, as in life.
        var nhs = random.NextDouble() < 0.85 ? SyntheticNhsNumber(_nhsIndex++) : null;

        var patient = new PatientSeed
        {
            FirstName = first,
            LastName = last,
            DateOfBirth = dateOfBirth,
            Email = email,
            NhsNumber = nhs,
            CreatedUtc = createdUtc,
            LastUpdatedUtc = createdUtc,
            Role = role,
        };
        All.Add(patient);
        return patient;
    }

    /// <summary>
    /// For each procedure in time order, its patient: often someone already registered (a second referral, a
    /// diagnostic test then an operation), otherwise someone new. Children only on the pathways that see them.
    /// </summary>
    public void AssignPatients(IReadOnlyList<ProcedurePlan> plans)
    {
        foreach (var plan in plans.OrderBy(p => p.CreatedUtc))
        {
            var mayBeChild = ProcedureKinds.SeesChildren(plan.Pathway);
            PatientSeed? patient = null;
            if (random.NextDouble() < 0.62)
            {
                var eligible = All.Where(p => p.Role == PatientRole.Active
                                              && p.CreatedUtc < plan.CreatedUtc.AddDays(-1)
                                              && p.Procedures < 4
                                              && (mayBeChild || !p.IsChildAt(plan.CreatedUtc))).ToList();
                // Someone with one procedure is likelier to come back than someone with three.
                if (eligible.Count > 0)
                    patient = ProcedurePlans.Weighted(random, eligible.Select(p => (p, p.Procedures switch { 1 => 6, 2 => 3, _ => 1 })).ToArray());
            }

            patient ??= NewPatient(plan.CreatedUtc, mayBeChild);
            patient.Procedures++;
            plan.Patient = patient;
        }

        // Registered in the last few weeks, waiting for a referral.
        for (var i = 0; i < 14; i++)
            NewPatient(timeline.NowUtc.AddDays(-random.Next(1, 28)), mayBeChild: true, PatientRole.Waiting);
    }

    /// <summary>
    /// Patients imported from the older system when the clinic started using MedApp, with old closed records: some due
    /// for the retention review now (adults 8 years after their last treatment; a child at 25), some not yet.
    /// </summary>
    public List<PatientSeed> AddImported()
    {
        var imported = new List<PatientSeed>();
        var today = timeline.Today;

        // Due now: adults treated 9 to 12 years ago, and one child treated at 11, now over 25. One of them is anonymised
        // and two kept (DemoPrivacy), so the review lists four, or six with the kept ones.
        foreach (var yearsAgo in new[] { 9, 10, 11, 12, 9, 11 })
            imported.Add(Imported(today.AddYears(-yearsAgo).AddDays(-random.Next(20, 300)), adultAge: random.Next(40, 80), PatientRole.ImportedDue));
        imported.Add(Imported(today.AddYears(-14).AddDays(-random.Next(20, 200)), adultAge: 11, PatientRole.ImportedDue));

        // Not due yet: adults treated 5 to 7 years ago, and a child treated at 9, six years ago.
        foreach (var yearsAgo in new[] { 5, 6, 7, 7 })
            imported.Add(Imported(today.AddYears(-yearsAgo).AddDays(-random.Next(20, 300)), adultAge: random.Next(30, 85), PatientRole.ImportedNotDue));
        imported.Add(Imported(today.AddYears(-6).AddDays(-random.Next(20, 200)), adultAge: 9, PatientRole.ImportedNotDue));

        return imported;

        PatientSeed Imported(DateOnly lastTreated, int adultAge, PatientRole role)
        {
            var dob = lastTreated.AddYears(-adultAge).AddDays(-random.Next(1, 360));
            var registered = UkDate(lastTreated.AddYears(-random.Next(1, 4)));
            var patient = Add(dob, registered, role);
            patient.LastUpdatedUtc = UkDate(lastTreated).AddHours(random.Next(1, 7));
            return patient;
        }

        static DateTime UkDate(DateOnly date) => DemoTimeline.At(date, 10, 0);
    }

    /// <summary>
    /// Creates every patient through the app's CreatePatient command (its validation and its activity entry, by whoever
    /// registered them, at that moment), then dates the record as it was registered: imported ones keep the older
    /// system's dates.
    /// </summary>
    public async Task CreateAsync(SeedServices services, IReadOnlyList<StaffMember> staff, CancellationToken ct)
    {
        var coordinator = DemoStaff.Get(staff, DemoStaff.CoordinatorUsername);
        var manager = DemoStaff.Get(staff, DemoStaff.ManagerUsername);

        foreach (var patient in All)
        {
            var imported = patient.Role is PatientRole.ImportedDue or PatientRole.ImportedNotDue;
            // Imported records were brought over by the service manager when the clinic started; the rest registered
            // by the booking coordinator as referrals came in.
            services.At(imported ? timeline.AppStartUtc.AddDays(-2) : patient.CreatedUtc, imported ? manager.ActivityActor : coordinator.ActivityActor);
            var created = await services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(
                new CreatePatientCommand(patient.FirstName, patient.LastName, patient.DateOfBirth, patient.Email, patient.NhsNumber), ct)));
            patient.Id = created.Id;
        }

        // The handler stamps records with the real time; date them as registered (one statement per patient).
        await services.Run(async sp =>
        {
            var db = sp.GetRequiredService<MedAppDbContext>();
            foreach (var patient in All)
                await db.Patients.Where(p => p.Id == patient.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.CreatedAt, patient.CreatedUtc)
                    .SetProperty(p => p.LastUpdated, patient.LastUpdatedUtc), ct);
        });
    }

    /// <summary>
    /// The <paramref name="n"/>th valid NHS number from 999 100 0000 up: the 999 range is kept for testing, so these are
    /// never anyone's. Numbers whose check digit would be 10 (not valid) are skipped.
    /// </summary>
    public static string SyntheticNhsNumber(int n)
    {
        var found = -1;
        for (var stem = 999_100_000; ; stem++)
        {
            var digits = stem.ToString(CultureInfo.InvariantCulture);
            if (NhsNumbers.CheckDigit(digits) is { } check && ++found == n)
                return digits + check;
        }
    }

    private static string Slug(string s) => new string(s.Where(char.IsLetter).ToArray()).ToLowerInvariant();
}
