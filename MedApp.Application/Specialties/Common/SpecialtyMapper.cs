using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Specialties;

namespace MedApp.Application.Specialties.Common;

public static class SpecialtyMapper
{
    /// <summary>A specialty with what has it; it can be deleted only while nothing does.</summary>
    public static SpecialtyResponse ToResponse(Specialty specialty, SpecialtyUsage usage) => new()
    {
        Id = specialty.Id,
        Name = specialty.Name,
        IsArchived = specialty.IsArchived,
        Version = specialty.Version,
        TemplateCount = usage.TemplateCount,
        PathwayCount = usage.PathwayCount,
        MemberCount = usage.MemberCount,
        ReferralCount = usage.ReferralCount,
        TriagerCount = usage.TriagerCount,
        CanBeDeleted = !usage.IsInUse,
        CreatedAt = specialty.CreatedAt,
        LastUpdated = specialty.LastUpdated
    };

    /// <summary>A specialty's response, looking up what has it.</summary>
    public static async Task<SpecialtyResponse> ToResponseAsync(
        Specialty specialty, ISpecialtyRepository repository, CancellationToken ct)
    {
        var usage = await repository.GetUsageAsync([specialty.Id], ct);
        return ToResponse(specialty, usage.GetValueOrDefault(specialty.Id, SpecialtyUsage.None));
    }

    /// <summary>A specialty someone belongs to.</summary>
    public static StaffSpecialtyResponse ToStaffResponse(Specialty specialty) => new()
    {
        Id = specialty.Id,
        Name = specialty.Name,
        IsArchived = specialty.IsArchived
    };

    /// <summary>The specialties someone belongs to, sorted by name.</summary>
    public static IReadOnlyList<StaffSpecialtyResponse> ToStaffResponses(IEnumerable<Specialty> specialties) =>
        specialties
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)
            .Select(ToStaffResponse)
            .ToList();
}
