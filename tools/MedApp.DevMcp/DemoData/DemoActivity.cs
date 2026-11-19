using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Activity;
using MedApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.DevMcp.DemoData.DemoPatients;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The activity log's sign-ins, views and searches, as the light demo's setup slice has them (<c>seedActivityLog</c>):
/// who signed in, and who viewed, listed or changed which patient's record, written with the app's writer
/// (<see cref="IActivityLog"/>, at the seed's clock), never what was seen. The changes the story made through the app's
/// commands are in the log already, written by those commands; the one change here (a mobile number) goes through its
/// command too. Everyone starts signed out: no sessions.
/// <para>
/// The sign-in and search entries' shapes (<see cref="SignInEntries"/>, <see cref="RefusedSignIn"/>,
/// <see cref="SearchDetails"/>, <see cref="ReferralViewDetails"/>) are the ones the commands write:
/// <c>DemoActivityParityTests</c> runs the real commands and checks they still are.
/// </para>
/// </summary>
public static class DemoActivity
{
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

    /// <summary>
    /// What opening a referral records with its <c>ReferralViewed</c> entry (<c>GetReferralByIdHandler</c>): its ID, and
    /// why the viewer sees it (<c>access</c>, decision 207; the seed's viewers are its own team's, <c>Owner</c>).
    /// </summary>
    public static object ReferralViewDetails(Guid referralId, string access = "Owner") => new { referralId, access };

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
        Search(nhsNumber: All[0].NhsNumber),
        Search(lastName: "-", from: new DateOnly(1950, 1, 1), to: new DateOnly(1960, 12, 31)),
        Search(email: "-"),
    ];

    private static SearchPatientsQuery Search(
        string? firstName = null, string? lastName = null, string? email = null, DateOnly? from = null, DateOnly? to = null, string? nhsNumber = null) =>
        new(1, 10, null, true, firstName, lastName, email, from, to, nhsNumber);

    /// <summary>Writes the log's sign-ins and views, and the booking coordinator's change to a mobile number. Returns how many entries it wrote.</summary>
    public static async Task<int> CreateAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var (admin, nadia, tom, jo, hannah) = (DemoPeople.Admin, DemoPeople.Thornbury, DemoPeople.Hallworth, DemoPeople.Whitlock, DemoPeople.Marsh);
        var entries = new List<ActivityEntry>();

        // A mobile number added to Ellis Varden's record (synthetic: Ofcom's drama range), through the patient command.
        await seed.Send(t.Ago(2, 11, 0), hannah, new UpdatePatientCommand(seed.Patients[EllisVarden], null, null, null, null, MobilePhone: "07700 900123"),
            "update Ellis Varden's mobile number");

        await using (var scope = seed.Services.Scope())
        {
            var log = scope.ServiceProvider.GetRequiredService<IActivityLog>();
            void Add(DateTime at, ActivityAction action, DemoPerson by, string? patient = null, object? details = null, string? pathway = null)
            {
                if (at >= t.NowUtc)
                    return;
                seed.Services.Clock.UtcNow = at;
                entries.Add(log.Entry(action, patient is null ? null : seed.Patients[patient], details,
                    pathwayId: pathway is null ? null : seed.Stories[pathway].Id, actor: by.ActivityActor));
            }

            void SignIn(DateTime at, DemoPerson by)
            {
                foreach (var (action, details) in SignInEntries(twoFactor: false))
                    Add(at, action, by, details: details);
            }

            SignIn(t.Ago(2, 11, 25), nadia);
            Add(t.Ago(2, 11, 30), ActivityAction.PatientViewed, nadia, OliverMaddox);
            Add(t.Ago(2, 11, 32), ActivityAction.ReferralViewed, nadia, OliverMaddox, ReferralViewDetails(seed.Tour["referralToTriage"]));
            Add(t.Ago(3, 14, 5), ActivityAction.PathwayViewed, tom, SamuelOkafor, pathway: SamuelOkafor);
            SignIn(t.Ago(2, 8, 40), admin);
            Add(t.Ago(2, 8, 45), ActivityAction.PatientListed, admin, TobiasLund);
            Add(t.Ago(2, 8, 46), ActivityAction.PatientViewed, admin, TobiasLund);
            Add(t.Ago(2, 16, 30), ActivityAction.PatientViewed, admin, GraceAbernethy);
            Add(t.Ago(1, 9, 5), RefusedSignIn.Action, jo, details: RefusedSignIn.Details);
            SignIn(t.Ago(1, 9, 6), jo);
            Add(t.Ago(1, 15, 20), ActivityAction.PatientViewed, nadia, HarrietQuill);
            Add(t.Ago(1, 15, 22), ActivityAction.PathwayViewed, nadia, HarrietQuill, pathway: HarrietQuill);
            SignIn(t.Ago(0, 8, 30), nadia);
            Add(t.Ago(0, 8, 35), ActivityAction.PatientListed, nadia, PriyaHolloway);
            Add(t.Ago(0, 8, 36), ActivityAction.PathwayViewed, nadia, PriyaHolloway, pathway: PriyaHolloway);
        }

        await seed.Db(async db =>
        {
            db.ActivityEntries.AddRange(entries);
            await db.SaveChangesAsync(seed.Ct);
        });
        return entries.Count;
    }
}
