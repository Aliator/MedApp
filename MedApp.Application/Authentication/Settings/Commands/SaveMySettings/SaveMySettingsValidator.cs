using System.Text.Json;
using FluentValidation;

namespace MedApp.Application.Authentication.Settings.Commands.SaveMySettings;

public sealed class SaveMySettingsValidator : AbstractValidator<SaveMySettingsCommand>
{
    /// <summary>Plenty for every setting and a good many saved searches.</summary>
    public const int MaxLength = 64 * 1024;

    public SaveMySettingsValidator()
    {
        RuleFor(x => x.Settings)
            .NotEmpty()
            .WithMessage("Settings are required.")
            .MaximumLength(MaxLength)
            .WithMessage($"Settings must not exceed {MaxLength} characters.")
            .Must(BeAJsonObject)
            .WithMessage("Settings must be a JSON object.");
    }

    private static bool BeAJsonObject(string settings)
    {
        try
        {
            using var document = JsonDocument.Parse(settings);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
