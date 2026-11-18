using FluentValidation;

namespace MedApp.Application.Specialties.Common;

public static class SpecialtyValidation
{
    /// <summary>A specialty's name: required, and at most <see cref="SpecialtyLimits.MaxNameLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> SpecialtyName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= SpecialtyLimits.MaxNameLength)
            .WithMessage($"Name must not exceed {SpecialtyLimits.MaxNameLength} characters.");
}
