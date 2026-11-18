using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>The rules and messages for letter templates, shared by their handlers.</summary>
public static class LetterTemplateRules
{
    public const string ChangedMeanwhileMessage =
        "Someone changed this letter template while you had it open. Reload to see the change, then try again.";

    /// <summary>A template some letter was written from: archived, never deleted.</summary>
    public const string InUseMessage =
        "Letters have been written from this template, so it can't be deleted. Archive it instead.";

    public static string NameTakenMessage(string name, string? teamName) =>
        teamName is null
            ? $"A letter template for every team is already called '{name}'."
            : $"A {teamName} letter template is already called '{name}'.";

    /// <summary>The same, found when saving (another was saved meanwhile).</summary>
    public static string NameTakenMessage(string name) =>
        $"Another letter template for the same team is already called '{name}'.";

    public static async Task<LetterTemplate> GetOrThrowAsync(
        this ILetterTemplateRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw new NotFoundException($"Letter template '{id}' was not found.");

    /// <summary>A change must start from the version saved now, or someone else has changed it since (409).</summary>
    public static void EnsureVersion(LetterTemplate template, int expectedVersion)
    {
        if (template.Version != expectedVersion)
            throw new ConflictException(ChangedMeanwhileMessage);
    }

    /// <summary>A text as it's kept: Windows line breaks made plain, and no blank space at its end.</summary>
    public static string Tidy(string? text) => (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();

    /// <summary>A plain text's lines (the letterhead or closing), blank ones kept: they space the lines out.</summary>
    public static IReadOnlyList<string> Lines(string? text)
    {
        var tidy = Tidy(text);
        return tidy.Length == 0 ? [] : tidy.Split('\n').Select(l => l.TrimEnd()).ToList();
    }
}
