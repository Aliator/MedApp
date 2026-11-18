using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>Letter templates and merge fields as the API returns them.</summary>
public static class LetterTemplateMapper
{
    /// <summary>
    /// A template, with its specialty's name (null for every specialty) and whether it can be deleted (no letter has
    /// been written from it).
    /// </summary>
    public static LetterTemplateResponse ToResponse(LetterTemplate template, string? specialtyName, bool canBeDeleted = false) => new()
    {
        Id = template.Id,
        Name = template.Name,
        Kind = template.Kind.ToString(),
        SpecialtyId = template.SpecialtyId,
        SpecialtyName = template.SpecialtyId is null ? null : specialtyName,
        Letterhead = template.Letterhead,
        BodySkeleton = template.BodySkeleton,
        Closing = template.Closing,
        MergeFields = LetterMergeFields.Used(template.Letterhead, template.BodySkeleton, template.Closing),
        IsArchived = template.IsArchived,
        CanBeDeleted = canBeDeleted,
        Version = template.Version,
        CreatedAt = template.CreatedAt,
        LastUpdated = template.LastUpdated
    };

    public static LetterMergeFieldResponse ToResponse(LetterMergeField field) => new()
    {
        Name = field.Name,
        Label = field.Label,
        Description = field.Description,
        Example = field.Example
    };
}
