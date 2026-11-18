using System.Globalization;
using MediatR;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Domain.Patients;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>Why a patient is in the demo, beyond their pathways.</summary>
public enum PatientRole
{
    /// <summary>Referred, with pathways.</summary>
    Active,

    /// <summary>Registered in the last few weeks with a referral still awaiting triage: no pathways yet.</summary>
    Waiting,

    /// <summary>Referred, but not taken on (the referral was rejected, redirected elsewhere or withdrawn): no pathways.</summary>
    ReferredOnly,

    /// <summary>Imported from the older system, with closed records long past: due for the retention review.</summary>
    ImportedDue,

    /// <summary>Imported, with closed records, but not due for review yet.</summary>
    ImportedNotDue,
}

/// <summary>
/// A synthetic patient: an invented name no other patient has, an example.com email, and an NHS number from the 999
/// test range or none.
/// </summary>
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

    /// <summary>Whether the invented first name is a woman's: two pathways see only women, or only men.</summary>
    public bool Female { get; init; }
    public int Pathways { get; set; }
    public bool Restricted { get; set; }

    /// <summary>When the record was anonymised (the retention review), if it was: from then on nobody sees it.</summary>
    public DateTime? AnonymisedUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    /// <summary>Whether the record was on the system at a moment: registered by then, and not yet anonymised.</summary>
    public bool IsOnSystemAt(DateTime utc) => CreatedUtc <= utc && (AnonymisedUtc is null || utc < AnonymisedUtc);

    public bool IsChildAt(DateTime utc) => AgeAt(utc) < 18;

    public int AgeAt(DateTime utc)
    {
        var on = DateOnly.FromDateTime(utc);
        var age = on.Year - DateOfBirth.Year;
        return on < DateOfBirth.AddYears(age) ? age - 1 : age;
    }
}

/// <summary>The centre's patients: invented people, created through the app's own command.</summary>
public sealed class DemoPatients(DemoTimeline timeline, Random random)
{
    private static readonly string[] WomensNames =
    [
        "Olivia", "Amelia", "Isla", "Ava", "Mia", "Grace", "Sophia", "Freya", "Evie", "Ruby", "Ella", "Chloe", "Priya",
        "Aisha", "Zara", "Maryam", "Niamh", "Siobhan", "Margaret", "Patricia", "Susan", "Janet", "Eileen", "Joan",
        "Hazel", "Iris", "Maureen", "Sheila", "Barbara", "Lorna", "Wendy", "Carol",
    ];

    private static readonly string[] MensNames =
    [
        "Oliver", "George", "Harry", "Noah", "Jack", "Leo", "Arthur", "Oscar", "Muhammad", "Ibrahim", "Arjun", "Rohan",
        "Kwame", "Tomasz", "Callum", "Rhys", "Ewan", "David", "Michael", "Peter", "Brian", "Kenneth", "Graham", "Derek",
        "Yusuf", "Bilal", "Stanley", "Roy", "Walter", "Frank", "Desmond", "Alan",
    ];

    private static readonly string[] LastNames =
    [
        "Smith", "Jones", "Taylor", "Brown", "Williams", "Wilson", "Johnson", "Davies", "Patel", "Robinson", "Wright",
        "Thompson", "Evans", "Walker", "White", "Roberts", "Green", "Hall", "Wood", "Jackson", "Clarke", "Khan", "Ahmed",
        "Hughes", "Edwards", "Turner", "Murphy", "O'Brien", "Kowalski", "Mensah", "Begum", "Ali", "Campbell", "Stewart",
        "Morgan", "Price", "Bennett", "Shaw", "Lloyd", "Harrison", "Chapman", "Kaur", "Singh", "Nowak", "Okafor", "Reid",
        "Fletcher", "Gill", "Holt", "Marsden", "Pickering", "Quinn", "Sutton", "Varley", "Whitfield", "Yates",
    ];

    private readonly HashSet<string> _names = [];
    private readonly HashSet<string> _emails = [];
    private int _nhsIndex;

    public List<PatientSeed> All { get; } = [];

    /// <summary>
    /// A new patient registered before <paramref name="referredUtc"/>: mostly older adults, as a specialist centre
    /// sees; a child only if <paramref name="mayBeChild"/>; a woman or a man, and no younger than
    /// <paramref name="minimumAge"/>, where the pathway needs it. With <paramref name="justBefore"/>, minutes before
    /// (added to register their referral, decision 11); otherwise up to three weeks before.
    /// </summary>
    public PatientSeed NewPatient(
        DateTime referredUtc, bool mayBeChild, PatientRole role = PatientRole.Active, bool? female = null, int minimumAge = 0, bool justBefore = false)
    {
        int age;
        do
        {
            age = mayBeChild && random.NextDouble() < 0.07
                ? random.Next(5, 17)
                : random.NextDouble() switch
                {
                    < 0.15 => random.Next(18, 45),
                    < 0.5 => random.Next(45, 70),
                    _ => random.Next(70, 94),
                };
        }
        while (age < minimumAge);

        var created = justBefore
            ? referredUtc.AddMinutes(-random.Next(3, 25))
            : DemoTimeline.InWorkingHours(referredUtc.AddDays(-random.Next(0, 22)).AddMinutes(-random.Next(0, 400)), random);
        if (created > referredUtc)
            created = referredUtc.AddMinutes(-random.Next(10, 60));
        var dob = DateOnly.FromDateTime(referredUtc).AddYears(-age).AddDays(-random.Next(1, 360));
        return Add(dob, created, role, female ?? random.NextDouble() < 0.5);
    }

    private PatientSeed Add(DateOnly dateOfBirth, DateTime createdUtc, PatientRole role, bool female)
    {
        // Every name is used once, so the summary and the tour can name a record and the patient search finds just it.
        var firstNames = female ? WomensNames : MensNames;
        string first, last;
        do
        {
            first = firstNames[random.Next(firstNames.Length)];
            last = LastNames[random.Next(LastNames.Length)];
        }
        while (!_names.Add($"{first} {last}"));

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
            Female = female,
        };
        All.Add(patient);
        return patient;
    }

    /// <summary>
    /// For each pathway in time order, its patient: often someone already registered (a second referral: a skin lesion
    /// one year, a diabetes review the next), otherwise someone new, added just before their referral was registered.
    /// Children only on the pathways that see them; women only, or men only, and old enough, on the two pathways that
    /// need it. (The patients of referrals that never became pathways come from <see cref="ReferralStories"/>.)
    /// </summary>
    public void AssignPatients(IReadOnlyList<PathwayPlan> plans)
    {
        foreach (var plan in plans.OrderBy(p => p.ReferredUtc))
        {
            var patient = random.NextDouble() < 0.64 ? Existing(plan.PathwayKind, plan.ReferredUtc) : null;
            patient ??= New(plan.PathwayKind, plan.ReferredUtc, PatientRole.Active, justBefore: plan.FromReferral);
            patient.Pathways++;
            plan.Patient = patient;
        }
    }

    /// <summary>
    /// Someone already registered a day before <paramref name="referredUtc"/> who could be referred for this kind of
    /// pathway (sex, age, adult or child), likelier the fewer pathways they have; null if nobody fits.
    /// </summary>
    public PatientSeed? Existing(PathwayKind kind, DateTime referredUtc, Func<PatientSeed, bool>? also = null)
    {
        var mayBeChild = ProcedureKinds.SeesChildren(kind);
        var (female, minimumAge) = ProcedureKinds.Sees(kind);
        var eligible = All.Where(p => p.Role == PatientRole.Active
                                      && p.CreatedUtc < referredUtc.AddDays(-1)
                                      && p.Pathways < 4
                                      && (female is null || p.Female == female)
                                      && p.AgeAt(referredUtc) >= minimumAge
                                      && (mayBeChild || !p.IsChildAt(referredUtc))
                                      && (also?.Invoke(p) ?? true)).ToList();
        // Someone with one pathway is likelier to come back than someone with three.
        return eligible.Count == 0
            ? null
            : PathwayPlans.Weighted(random, eligible.Select(p => (p, p.Pathways switch { 0 or 1 => 6, 2 => 3, _ => 1 })).ToArray());
    }

    /// <summary>Someone new who could be referred for this kind of pathway, registered before <paramref name="referredUtc"/>.</summary>
    public PatientSeed New(PathwayKind kind, DateTime referredUtc, PatientRole role, bool justBefore)
    {
        var (female, minimumAge) = ProcedureKinds.Sees(kind);
        return NewPatient(referredUtc, ProcedureKinds.SeesChildren(kind), role, female, minimumAge, justBefore);
    }

    /// <summary>
    /// Patients imported from the older system when the centre started using MedApp, with old closed records: some due
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
            var patient = Add(dob, registered, role, random.NextDouble() < 0.5);
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
            // Imported records were brought over by the service manager when the centre started; the rest registered
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
