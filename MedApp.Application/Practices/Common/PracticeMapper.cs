using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MedApp.Domain.Contacts;
using MedApp.Domain.Practices;

namespace MedApp.Application.Practices.Common;

/// <summary>GP practices as the API returns them.</summary>
public static class PracticeMapper
{
    /// <summary>A practice with what has it; it can be deleted only while nothing does.</summary>
    public static PracticeResponse ToResponse(Practice practice, PracticeUsage usage) => new()
    {
        Id = practice.Id,
        Name = practice.Name,
        OdsCode = practice.OdsCode,
        AddressLines = PostalAddress.Split(practice.AddressLines),
        Postcode = practice.Postcode,
        Phone = practice.Phone,
        SecureEmail = practice.SecureEmail,
        IsArchived = practice.IsArchived,
        Version = practice.Version,
        PatientCount = usage.PatientCount,
        ReferralCount = usage.ReferralCount,
        CanBeDeleted = !usage.IsInUse,
        CreatedAt = practice.CreatedAt,
        LastUpdated = practice.LastUpdated
    };

    /// <summary>A practice's response, looking up what has it.</summary>
    public static async Task<PracticeResponse> ToResponseAsync(
        Practice practice, IPracticeRepository repository, CancellationToken ct)
    {
        var usage = await repository.GetUsageAsync([practice.Id], ct);
        return ToResponse(practice, usage.GetValueOrDefault(practice.Id, PracticeUsage.None));
    }

    /// <summary>A practice as a patient's page shows it.</summary>
    public static PracticeSummaryResponse ToSummary(Practice practice) => new()
    {
        Id = practice.Id,
        Name = practice.Name,
        OdsCode = practice.OdsCode,
        AddressLines = PostalAddress.Split(practice.AddressLines),
        Postcode = practice.Postcode,
        Phone = practice.Phone,
        IsArchived = practice.IsArchived
    };
}
