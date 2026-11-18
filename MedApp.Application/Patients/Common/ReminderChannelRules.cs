using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Patients;

namespace MedApp.Application.Patients.Common;

/// <summary>
/// The patient's choice of appointment messages (<see cref="ReminderChannel"/>, centre plan decision 133): asked on the
/// patient form, changed when the patient asks (texts can't be replied to). A choice without the contact it needs is
/// allowed: its messages are stored as not sent, and the appointment says why.
/// </summary>
public static class ReminderChannelRules
{
    public static readonly string Names = string.Join(", ", Enum.GetNames<ReminderChannel>());

    public static readonly string NotAChannel = $"Reminder channel must be one of: {Names}.";

    /// <summary>The choice by name (ignoring case); none for null, empty or anything else.</summary>
    public static ReminderChannel ParseOrNone(string? value) =>
        PathwayEnums.TryParse<ReminderChannel>(value, out var channel) ? channel : ReminderChannel.None;

    /// <summary>An optional choice, by name.</summary>
    public static IRuleBuilderOptions<T, string?> ReminderChannelName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => value is null || PathwayEnums.TryParse<ReminderChannel>(value, out _)).WithMessage(NotAChannel);
}
