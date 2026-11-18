using MediatR;
using MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;
using MedApp.Application.Specialties.Commands.CreateSpecialty;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The centre's specialties (its services), and which staff belong to each. Made through the app's commands, as an
/// admin made them: the specialties when the centre started, each person's memberships a few minutes after their
/// account (recorded in the activity log as <c>StaffSpecialtiesChanged</c>). General, which every database starts with,
/// stays, with nothing in it.
/// </summary>
public static class DemoSpecialties
{
    public const string Dermatology = "Dermatology";
    public const string Gynaecology = "Gynaecology";
    public const string Urology = "Urology";
    public const string Diabetes = "Diabetes";
    public const string Biomechanics = "Biomechanics";

    public static readonly string[] Names = [Dermatology, Gynaecology, Urology, Diabetes, Biomechanics];

    /// <summary>
    /// Which specialties each kind of staff belongs to: consultants, the GP with a special interest, the nurse
    /// specialists and the podiatrist in theirs, the pharmacist in several, and the clinical lead (the local
    /// <c>admin</c>) in every one, so it can show triage. Everyone else (the manager, the booking coordinator, the lab,
    /// the healthcare assistant, the new starter) in none.
    /// </summary>
    public static IReadOnlyList<string> For(StaffKind kind) => kind switch
    {
        StaffKind.ClinicalLead => Names,
        StaffKind.Dermatologist or StaffKind.SkinGp or StaffKind.SkinCancerNurse => [Dermatology],
        StaffKind.Gynaecologist => [Gynaecology],
        StaffKind.Urologist => [Urology],
        StaffKind.DiabetesNurse => [Diabetes],
        StaffKind.Podiatrist => [Biomechanics],
        StaffKind.Pharmacist => [Dermatology, Urology, Diabetes],
        _ => [],
    };

    /// <summary>The specialty each pathway (template) belongs to, the archived skin lesion pathway too.</summary>
    public static string For(PathwayKind kind) => kind switch
    {
        PathwayKind.SkinLesion or PathwayKind.SkinLesion2024 or PathwayKind.InflammatorySkin => Dermatology,
        PathwayKind.PostMenopausalBleeding => Gynaecology,
        PathwayKind.Prostate => Urology,
        PathwayKind.Diabetes => Diabetes,
        PathwayKind.Biomechanics => Biomechanics,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A pathway with no specialty."),
    };

    /// <summary>The specialties' IDs by name, and when the last was added.</summary>
    public sealed record Created(IReadOnlyDictionary<string, Guid> Ids, DateTime AddedUtc);

    /// <summary>Adds the specialties, as the clinical lead did before the centre opened.</summary>
    public static async Task<Created> CreateAsync(
        SeedServices services, StaffMember lead, DemoTimeline timeline, CancellationToken ct)
    {
        var ids = new Dictionary<string, Guid>();
        var at = timeline.AppStartUtc.AddDays(-30).AddHours(1);
        foreach (var name in Names)
        {
            services.At(at, lead.ActivityActor);
            var specialty = await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(new CreateSpecialtyCommand(name), ct)));
            ids[name] = specialty.Id;
            at = at.AddMinutes(2);
        }

        return new Created(ids, at);
    }

    /// <summary>
    /// Gives each member of staff their specialties, a few minutes after their account was made (or after the
    /// specialties were, for anyone whose account is older). Returns how many memberships there are.
    /// </summary>
    public static async Task<int> AddMembershipsAsync(
        SeedServices services, IReadOnlyList<StaffMember> staff, Created specialties, CancellationToken ct)
    {
        var lead = staff[0];
        var count = 0;
        foreach (var member in staff.Where(m => m.Kind != StaffKind.Leaver))
        {
            var names = For(member.Kind);
            if (names.Count == 0)
                continue;

            var from = member.StartedUtc > specialties.AddedUtc ? member.StartedUtc : specialties.AddedUtc;
            services.At(from.AddMinutes(2), lead.ActivityActor);
            await services.Run(sp => Guards.Validated(() =>
                sp.GetRequiredService<IMediator>().Send(
                    new SetUserSpecialtiesCommand(member.Username, names.Select(n => specialties.Ids[n]).ToList()), ct)));
            count += names.Count;
        }

        return count;
    }
}
