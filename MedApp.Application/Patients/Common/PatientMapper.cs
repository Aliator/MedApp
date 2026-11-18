using MedApp.Contracts.Patients.Responses;
using MedApp.Application.Practices.Common;
using MedApp.Domain.Contacts;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// Patients as the API returns them, with their restricted, disputed and anonymised flags. Their audit timestamps
/// stay inside the app, except in a subject access export (<see cref="PatientExportMapper"/>).
/// </summary>
public static class PatientMapper
{
    /// <summary>A patient, as lists show them (their practice by its ID only).</summary>
    public static PatientResponse ToResponse(Patient patient) => ToResponse(patient, null);

    /// <summary>
    /// A patient with the practice they're registered with (<paramref name="practice"/>), for a single patient's
    /// response; it's left out unless it's the patient's.
    /// </summary>
    public static PatientResponse ToResponse(Patient patient, Practice? practice) => new(
        patient.Id,
        patient.FirstName,
        patient.LastName,
        patient.DateOfBirth,
        patient.Email)
    {
        NhsNumber = patient.NhsNumber,
        AddressLines = PostalAddress.Split(patient.AddressLines),
        Postcode = patient.Postcode,
        MobilePhone = patient.MobilePhone,
        PracticeId = patient.PracticeId,
        Practice = practice is not null && practice.Id == patient.PracticeId ? PracticeMapper.ToSummary(practice) : null,
        IsRestricted = patient.RestrictedAtUtc is not null,
        RestrictionReason = patient.RestrictionReason,
        RestrictedAtUtc = patient.RestrictedAtUtc,
        IsDisputed = patient.DisputedAtUtc is not null,
        DisputeReason = patient.DisputeReason,
        DisputedAtUtc = patient.DisputedAtUtc,
        IsAnonymised = patient.AnonymisedAtUtc is not null,
        AnonymisedAtUtc = patient.AnonymisedAtUtc
    };

    /// <summary>The patient's flags, for the banners on the pages about them that aren't the patient's own.</summary>
    public static PatientFlagsResponse ToFlags(Patient patient) => new()
    {
        IsRestricted = patient.RestrictedAtUtc is not null,
        RestrictionReason = patient.RestrictionReason,
        RestrictedAtUtc = patient.RestrictedAtUtc,
        IsDisputed = patient.DisputedAtUtc is not null,
        DisputeReason = patient.DisputeReason,
        DisputedAtUtc = patient.DisputedAtUtc,
        IsAnonymised = patient.AnonymisedAtUtc is not null,
        AnonymisedAtUtc = patient.AnonymisedAtUtc
    };

    /// <summary>A patient's name only, for a pin's label.</summary>
    public static PatientLabelResponse ToLabel(Guid id, string name) => new(id, name);

    /// <summary>The patient's name as staff see it: "First Last".</summary>
    public static string FullName(Patient patient) => $"{patient.FirstName} {patient.LastName}";
}
