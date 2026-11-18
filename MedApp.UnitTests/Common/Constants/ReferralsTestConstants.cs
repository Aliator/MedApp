using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specialties;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic referrals for the referral tests: invented practices and clinicians, never real ones.</summary>
public static class ReferralsTestConstants
{
    public const string Practice = "Synthetic Lane Surgery";
    public const string OtherPractice = "Synthetic Row Practice";
    public const string Clinician = "Dr A Synthetic";
    public const string OdsCode = "Z99999";
    public const string Reason = "Synthetic reason for referral: a changing mole, three months.";
    public const string Detail = "Synthetic detail: the practice phoned to withdraw it.";

    /// <summary>One string standing for every piece of free text, to check it never reaches a staff view.</summary>
    public const string Canary = "SYNTHETIC-REFERRAL-CANARY-7741";

    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);
    public static readonly DateOnly Today = new(2026, 11, 18);
    public static readonly DateOnly ReceivedOn = new(2026, 11, 10);

    public static readonly PathwayActor Registrar =
        new(Guid.Parse("0000007e-0000-0000-0000-000000000001"), "b.synthetic", TestPermissions.Staff);

    public static readonly PathwayActor Viewer =
        new(Guid.Parse("0000007e-0000-0000-0000-000000000002"), "v.synthetic", new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients });

    public static Referral CreateReferral(
        Patient? patient = null,
        Specialty? specialty = null,
        ReferralStatus status = ReferralStatus.AwaitingTriage,
        int version = 1)
    {
        patient ??= PatientsTestConstants.CreateValidPatient();
        specialty ??= SpecialtiesTestConstants.CreateSpecialty();
        return new Referral
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            SpecialtyId = specialty.Id,
            Specialty = specialty,
            ReceivedOn = ReceivedOn,
            ReferrerPractice = Practice,
            ReferrerClinician = Clinician,
            ReferrerOdsCode = OdsCode,
            RequestedPriority = PatientPathwayPriority.High,
            Reason = Reason,
            Status = status,
            RegisteredAtUtc = NowUtc.AddDays(-7),
            RegisteredByUserId = Registrar.UserId,
            RegisteredByUsername = Registrar.Username,
            Version = version,
            LastUpdated = NowUtc.AddDays(-7)
        };
    }
}
