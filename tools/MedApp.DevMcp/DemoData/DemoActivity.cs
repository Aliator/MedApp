using MediatR;
using MedApp.Application.Activity.Common;
using MedApp.Application.Authentication.Sessions.Commands.Login;
using MedApp.Application.Authentication.Sessions.Commands.LoginTwoFactor;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The activity log over the last 60 days, in its own format, written with the app's writer (<see cref="IActivityLog"/>,
/// at the seed's clock): sign-ins and sign-outs on working days, a few refused, the views of patients and procedures
/// by the people working on them (once per view window, <see cref="ActivityOptions.ViewWindowMinutes"/>, as the app
/// records them), the patients each list showed, and searches by their filter names
/// (<see cref="PatientSearch.FiltersUsed"/>) and counts, never the terms. A patient is seen only while they're on the
/// system: from their registration, and not after they were anonymised. Today's sign-ins go through the app's own
/// sign-in command instead, so they come with live sessions; so does the locked account's run of wrong passwords.
/// <para>
/// The sign-in and search entries' shapes (<see cref="SignInEntries"/>, <see cref="RefusedSignIn"/>,
/// <see cref="SearchDetails"/>) are the ones the commands write: <c>DemoActivityParityTests</c> runs the real commands
/// and checks they still are.
/// </para>
/// </summary>
public static class DemoActivity
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

    /// <summary>The earliest anyone signs in: half an hour before the working day.</summary>
    private static readonly TimeOnly EarliestSignIn = new(7, 30);

    /// <summary>
    /// What one sign-in writes, in order, by the account signing in: <c>LoginHandler</c>'s entry; or, for an account
    /// with two-factor, its challenge, then <c>LoginTwoFactorHandler</c>'s entry for an authenticator code.
    /// </summary>
    public static IReadOnlyList<(ActivityAction Action, object? Details)> SignInEntries(bool twoFactor) => twoFactor
        ? [(ActivityAction.TwoFactorChallenged, null), (ActivityAction.SignedIn, new { secondStep = "authenticator" })]
        : [(ActivityAction.SignedIn, null)];

    /// <summary>What a wrong password writes (<c>LoginHandler</c>): by the account tried if it exists, else by the name typed.</summary>
    public static (ActivityAction Action, object Details) RefusedSignIn { get; } =
        (ActivityAction.SignInFailed, new { reason = "InvalidCredentials" });

    /// <summary>A patient search's entry details (<c>SearchPatientsHandler</c>): the filters used, and how many it found.</summary>
    public static object SearchDetails(IReadOnlyList<string> filters, int resultCount) => new { filters, resultCount };

    /// <summary>
    /// The searches people make, by what they fill in. What each records comes from the app's own
    /// <see cref="PatientSearch.FiltersUsed"/>; the terms are placeholders, never recorded.
    /// </summary>
    public static IReadOnlyList<SearchPatientsQuery> Searches { get; } =
    [
        Search(lastName: "-"),
        Search(firstName: "-", lastName: "-"),
        Search(nhsNumber: DemoPatients.SyntheticNhsNumber(0)),
        Search(lastName: "-", from: new DateOnly(1950, 1, 1), to: new DateOnly(1960, 12, 31)),
        Search(email: "-"),
    ];

    private static SearchPatientsQuery Search(
        string? firstName = null, string? lastName = null, string? email = null, DateOnly? from = null, DateOnly? to = null, string? nhsNumber = null) =>
        new(1, 10, null, true, firstName, lastName, email, from, to, nhsNumber);

    public static async Task<int> CreateAsync(
        SeedServices services, DemoWorld world, IReadOnlyList<PatientSeed> patients, CancellationToken ct)
    {
        var entries = new List<ActivityEntry>();
        await using (var scope = services.Scope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IActivityLog>();
            var minutes = scope.ServiceProvider.GetRequiredService<IOptions<ActivityOptions>>().Value.ViewWindowMinutes;
            Build(services, log, world, patients, TimeSpan.FromMinutes(Math.Max(0, minutes)), entries);
        }

        for (var i = 0; i < entries.Count; i += 4000)
        {
            var chunk = entries.Skip(i).Take(4000).ToList();
            await services.Run(async sp =>
            {
                var db = sp.GetRequiredService<MedAppDbContext>();
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.ActivityEntries.AddRange(chunk);
                await db.SaveChangesAsync(ct);
            });
        }

        return entries.Count;
    }

    private static void Build(
        SeedServices services, IActivityLog log, DemoWorld world, IReadOnlyList<PatientSeed> patients, TimeSpan viewWindow, List<ActivityEntry> entries)
    {
        var random = world.Random;
        var timeline = world.Timeline;
        var lastView = new Dictionary<(Guid, ActivityAction, Guid, Guid?), DateTime>();
        var active = patients.Where(p => p.Role is PatientRole.Active or PatientRole.Waiting).ToList();
        var byId = patients.ToDictionary(p => p.Id);
        var openPatients = world.Procedures.Where(p => ProcedureWorkflow.IsOpen(p.Status)).Select(p => p.PatientId).Distinct().ToList();
        var dueForReview = patients.Where(p => p.Role == PatientRole.ImportedDue).Select(p => p.Id).ToList();
        var touchesByDay = world.Touches
            .Where(t => t.AtUtc >= timeline.ActivityFromUtc && t.AtUtc < timeline.NowUtc)
            .ToLookup(t => (t.UserId, UkTime.DateOf(t.AtUtc)));
        var appointmentsByDay = world.Appointments
            .Where(a => a.StartUtc is { } s && s >= timeline.ActivityFromUtc && a.Status is AppointmentStatus.Booked or AppointmentStatus.Attended or AppointmentStatus.DidNotAttend)
            .ToLookup(a => (a.StaffUserId!.Value, UkTime.DateOf(a.StartUtc!.Value)));

        void Add(DateTime at, ActivityAction action, StaffMember? by, Guid? patientId = null, object? details = null, Guid? procedureId = null, ActivityActor? actor = null)
        {
            if (at >= timeline.NowUtc)
                return;
            services.Clock.UtcNow = at;
            entries.Add(log.Entry(action, patientId, details, procedureId: procedureId, actor: actor ?? by!.ActivityActor));
        }

        // A view, unless the same person saw the same thing within the view window (ActivityLog.RecordViewsAsync); and
        // only of a patient on the system then (registered, and not yet anonymised).
        void View(DateTime at, ActivityAction action, StaffMember by, Guid patientId, Guid? procedureId = null)
        {
            if (!byId[patientId].IsOnSystemAt(at))
                return;
            var key = (by.Id, action, patientId, procedureId);
            if (lastView.TryGetValue(key, out var last) && at - last < viewWindow && at >= last)
                return;
            lastView[key] = at;
            Add(at, action, by, patientId, procedureId: procedureId);
        }

        var first = UkTime.DateOf(timeline.ActivityFromUtc);
        for (var date = first; date <= timeline.Today; date = date.AddDays(1))
        {
            if (!WorkingHours.IsWorkingDay(date))
                continue;
            var isToday = date == timeline.Today;

            foreach (var member in world.Staff)
            {
                var dayStart = DemoTimeline.At(date, 8, 0);
                if (!member.WorksAt(dayStart) || member.Kind == StaffKind.NewStarter || world.Calendars.IsOnLeave(member.Id, date))
                    continue;

                var touches = touchesByDay[(member.Id, date)].OrderBy(t => t.AtUtc).ToList();
                var signIn = touches.Count > 0
                    ? touches[0].AtUtc.AddMinutes(-random.Next(3, 25))
                    : dayStart.AddMinutes(random.Next(-10, 70));
                if (UkTime.TimeOf(signIn) < EarliestSignIn)
                    signIn = DemoTimeline.At(date, 7, 30).AddMinutes(random.Next(0, 20));

                // Today's sign-ins are made by the sign-in command itself (SignInTodayAsync), with their sessions.
                if (!isToday)
                {
                    if (random.NextDouble() < 0.02)
                        Add(signIn.AddMinutes(-1), RefusedSignIn.Action, member, details: RefusedSignIn.Details);

                    // The two-factor account's code is typed about 25 seconds after its password.
                    var steps = SignInEntries(member.Username == DemoStaff.TwoFactorUsername && signIn >= timeline.TwoFactorEnabledUtc);
                    for (var step = 0; step < steps.Count; step++)
                        Add(signIn.AddSeconds(-25 * (steps.Count - 1 - step)), steps[step].Action, member, details: steps[step].Details);
                }

                // The day's diary (the calendar lists its appointments' patients), and the procedure list.
                var diary = appointmentsByDay[(member.Id, date)].ToList();
                foreach (var appointment in diary)
                    View(signIn.AddMinutes(2), ActivityAction.PatientListed, member, appointment.PatientId);

                if (member.Kind is not (StaffKind.ServiceManager or StaffKind.BookingCoordinator) && openPatients.Count > 0)
                    foreach (var patientId in openPatients.OrderBy(_ => random.Next()).Take(random.Next(4, 11)))
                        View(signIn.AddMinutes(4), ActivityAction.PatientListed, member, patientId);

                // Searches: by filter names and the count found, never what was typed.
                var searches = member.Kind == StaffKind.BookingCoordinator ? random.Next(2, 6) : random.Next(0, 2);
                for (var s = 0; s < searches; s++)
                {
                    var at = signIn.AddMinutes(random.Next(10, 480));
                    var query = Searches[random.Next(Searches.Count)];
                    var registered = active.Where(p => p.IsOnSystemAt(at)).ToList();
                    var found = Math.Min(query.NhsNumber is not null ? 1 : random.Next(1, 7), registered.Count);
                    Add(at, ActivityAction.PatientsSearched, member, details: SearchDetails(PatientSearch.FiltersUsed(query), found));
                    foreach (var patient in registered.OrderBy(_ => random.Next()).Take(found))
                        View(at, ActivityAction.PatientListed, member, patient.Id);
                }

                // Every change they made, they made from the record: the patient's page, then the procedure's.
                foreach (var touch in touches)
                {
                    View(touch.AtUtc.AddMinutes(-random.Next(2, 6)), ActivityAction.PatientViewed, member, touch.PatientId);
                    if (touch.ProcedureId is { } procedureId)
                        View(touch.AtUtc.AddSeconds(-random.Next(20, 110)), ActivityAction.ProcedureViewed, member, touch.PatientId, procedureId);
                }

                // Admins look over the retention review now and then.
                if (member.Kind == StaffKind.ClinicalLead && date.DayOfWeek == DayOfWeek.Monday)
                    foreach (var patientId in dueForReview)
                        View(signIn.AddMinutes(30), ActivityAction.PatientListed, member, patientId);

                if (isToday)
                    continue;

                var lastAt = touches.Count > 0 ? touches[^1].AtUtc : signIn.AddHours(6);
                var signOut = lastAt.AddMinutes(random.Next(5, 70));
                if (signOut.Date == lastAt.Date && random.NextDouble() < 0.75)
                    Add(signOut, ActivityAction.SignedOut, member);
            }
        }

        // A few sign-ins refused for a name no account has (a typo): by no account, with the name typed.
        foreach (var (typo, daysAgo) in new[] { ("k.elison", 9), ("h.mash", 23), ("admn", 41) })
        {
            var at = DemoTimeline.WorkingMoment(timeline.NowUtc.AddDays(-daysAgo), 8, 12);
            Add(at, RefusedSignIn.Action, null, details: RefusedSignIn.Details, actor: new ActivityActor(null, typo, "192.0.2.40"));
        }
    }

    /// <summary>
    /// Today, on a working day from 07:30: staff who've worked today sign in through the app's own command (a session
    /// each, and its activity entry), the two-factor account through both steps. Whatever the day, the healthcare
    /// assistant then gets locked out after a run of wrong passwords (they can come on any day), and stays locked
    /// until tomorrow evening, so the Users page offers Unlock.
    /// </summary>
    public static async Task<object> SignInTodayAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        var timeline = world.Timeline;
        var now = timeline.NowUtc;
        var signedIn = new List<string>();
        if (WorkingHours.IsWorkingDay(timeline.Today))
        {
            var touchesToday = world.Touches
                .Where(t => t.AtUtc < now && UkTime.DateOf(t.AtUtc) == timeline.Today)
                .ToLookup(t => t.UserId);

            foreach (var member in world.Staff.Where(s => s.WorksAt(now) && s.Kind != StaffKind.NewStarter && !world.Calendars.IsOnLeave(s.Id, timeline.Today)))
            {
                var first = touchesToday[member.Id].Select(t => t.AtUtc).DefaultIfEmpty(DemoTimeline.At(timeline.Today, 8, 10)).Min();
                var at = first.AddMinutes(-world.Random.Next(4, 20));
                if (at >= now)
                    at = now.AddMinutes(-world.Random.Next(5, 30));
                // Nobody signs in before the working day starts, as in the history: in the small hours, nobody has yet.
                if (UkTime.DateOf(at) != timeline.Today || UkTime.TimeOf(at) < EarliestSignIn)
                    continue;

                services.At(at, member.ActivityActor);
                var outcome = await services.Run(sp => sp.GetRequiredService<IMediator>().Send(
                    new LoginCommand(member.Username, member.Password!, member.IpAddress, Browser), ct));
                if (outcome.Challenge is { } challenge)
                {
                    var code = Totp.Compute(Base32.Decode(member.TwoFactorKey!.Replace(" ", string.Empty)), Totp.TimeStep(services.Clock.GetUtcNow()));
                    services.At(at.AddSeconds(20), member.ActivityActor);
                    await services.Run(sp => sp.GetRequiredService<IMediator>().Send(
                        new LoginTwoFactorCommand(challenge, code, null, member.IpAddress, Browser), ct));
                }

                signedIn.Add(member.Username);
            }
        }

        // The locked account: five wrong passwords in a few minutes (the app's own refusals and lockout), then the
        // lockout stretched to tomorrow evening, as five minutes would pass before anyone opened the Users page.
        var locked = DemoStaff.Get(world.Staff, DemoStaff.LockedUsername);
        for (var i = 0; i < 6; i++)
        {
            services.At(now.AddMinutes(-20 + 2 * i), locked.ActivityActor);
            try
            {
                await services.Run(sp => sp.GetRequiredService<IMediator>().Send(
                    new LoginCommand(locked.Username, "wrong-password-" + i, "198.51.100.23", Browser), ct));
            }
            catch (Exception e) when (e is InvalidCredentialsException or AccountLockedOutException)
            {
                // Refused and recorded, as at the sign-in page.
            }
        }

        var lockedUntil = new DateTimeOffset(DemoTimeline.At(timeline.Today.AddDays(1), 18, 0));
        await services.Run(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(locked.Id.ToString())
                       ?? throw new InvalidOperationException($"{locked.Username} wasn't found.");
            user.LockoutEnd = lockedUntil;
            Guards.Check(await users.UpdateAsync(user), $"Lock {locked.Username} out");
        });

        return new { signedIn, lockedOut = locked.Username, lockedUntil };
    }
}
