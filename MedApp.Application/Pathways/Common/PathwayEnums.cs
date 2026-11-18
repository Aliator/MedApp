using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>Reads the enum names the API takes as strings, ignoring case.</summary>
public static class PathwayEnums
{
    public static string PriorityNames => string.Join(", ", Enum.GetNames<PatientPathwayPriority>());

    public static string StatusNames => string.Join(", ", Enum.GetNames<PatientPathwayStatus>());

    public static string AuditActionNames => string.Join(", ", Enum.GetNames<PatientPathwayAuditAction>());

    public static string VisitRequirementNames => string.Join(", ", Enum.GetNames<VisitRequirement>());

    public static bool IsPriority(string? value) => TryParse<PatientPathwayPriority>(value, out _);

    public static bool IsStatus(string? value) => TryParse<PatientPathwayStatus>(value, out _);

    public static bool IsAuditAction(string? value) => TryParse<PatientPathwayAuditAction>(value, out _);

    public static bool IsVisitRequirement(string? value) => TryParse<VisitRequirement>(value, out _);

    /// <summary>Parses an enum by name only (not by number), ignoring case.</summary>
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
               && !char.IsDigit(value.Trim()[0])
               && Enum.TryParse(value.Trim(), ignoreCase: true, out result)
               && Enum.IsDefined(result);
    }

    public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum =>
        TryParse<TEnum>(value, out var result)
            ? result
            : throw new ArgumentException($"'{value}' is not a {typeof(TEnum).Name}.", nameof(value));
}
