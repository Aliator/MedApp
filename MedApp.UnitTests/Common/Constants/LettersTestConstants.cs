using MedApp.Application.Letters.Common;
using MedApp.Domain.Letters;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic letter templates: an invented centre, and only merge fields for anything about a patient.</summary>
public static class LettersTestConstants
{
    public const string TemplateName = "Synthetic clinic letter";
    public const string OtherTemplateName = "Synthetic discharge summary";
    public const string Letterhead = "Synthetic Specialist Centre\n1 Imaginary Lane\nFictionton ZZ1 1ZZ";
    public const string BodySkeleton = "Dear Doctor,\n\n## Diagnosis\n\n## Plan\n\nRe: {{patient.name}}, born {{patient.dateOfBirth}}";
    public const string Closing = "Yours sincerely,\n\n{{author.name}}\n{{author.jobTitle}}";

    public static readonly DateTime CreatedAtUtc = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);

    public static LetterTemplate CreateTemplate(
        string name = TemplateName,
        Guid? specialtyId = null,
        LetterKind kind = LetterKind.ClinicLetter,
        bool isArchived = false,
        int version = 3) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        SpecialtyId = specialtyId,
        Letterhead = Letterhead,
        BodySkeleton = BodySkeleton,
        Closing = Closing,
        IsArchived = isArchived,
        Version = version,
        CreatedAt = CreatedAtUtc,
        LastUpdated = CreatedAtUtc
    };

    /// <summary>A renderer whose font shows everything, except the characters given.</summary>
    public static Mock<ILetterRenderer> RendererMissing(params string[] characters)
    {
        var renderer = new Mock<ILetterRenderer>();
        renderer.Setup(r => r.FindUnsupportedCharacters(It.IsAny<string?>()))
            .Returns((string? text) => characters.Where(c => text?.Contains(c, StringComparison.Ordinal) == true).ToList());
        return renderer;
    }
}
