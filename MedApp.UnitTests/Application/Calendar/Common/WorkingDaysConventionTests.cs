using FluentAssertions;
using FluentValidation;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>
/// Every command that puts something on a calendar refuses a closed day from one place (decision 72): its validator
/// takes <see cref="WorkingDays"/>, which looks the day's closed days up. A new command with a time and no such
/// validator would let a closed day through, so this finds them by their shape.
/// </summary>
[TestFixture]
public sealed class WorkingDaysConventionTests
{
    /// <summary>Commands with a time that may be on a closed day, and why.</summary>
    private static readonly Dictionary<Type, string> Exempt = new()
    {
        [typeof(RecordPastAppointmentCommand)] = "a walk-in may be on a closed day: what happened, happened (decision 74)",
    };

    private static readonly IReadOnlyList<Type> Commands = typeof(WorkingDays).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace?.Contains(".Commands.") == true && t.Name.EndsWith("Command"))
        .ToList();

    private static bool HasTimes(Type command) =>
        command.GetProperty("StartUtc") is { } start && command.GetProperty("EndUtc") is { } end
        && Nullable.GetUnderlyingType(start.PropertyType) is var startType && (startType ?? start.PropertyType) == typeof(DateTime)
        && (Nullable.GetUnderlyingType(end.PropertyType) ?? end.PropertyType) == typeof(DateTime);

    private static IReadOnlyList<Type> ValidatorsOf(Type command) =>
        typeof(WorkingDays).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IValidator<>).MakeGenericType(command).IsAssignableFrom(t))
            .ToList();

    private static bool TakesWorkingDays(Type validator) =>
        validator.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(WorkingDays)));

    [Test]
    public void EveryCommandWithATime_IsValidatedWithTheClosedDays()
    {
        var timed = Commands.Where(HasTimes).ToList();
        timed.Should().HaveCountGreaterThanOrEqualTo(8, "the scan must find the calendar commands");

        foreach (var command in timed.Where(c => !Exempt.ContainsKey(c)))
            ValidatorsOf(command).Any(TakesWorkingDays).Should().BeTrue($"{command.Name} puts something on a calendar, so a closed day must be refused");
    }

    [Test]
    public void ApplyingADayTemplate_IsValidatedWithTheClosedDays()
    {
        // A date rather than a time: templates don't apply on a closed day, so there's nothing to choose.
        ValidatorsOf(typeof(ApplyDayTemplateCommand)).Any(TakesWorkingDays).Should().BeTrue();
    }

    [Test]
    public void TheExemptions_AreStillCommandsWithATime_AndDontLookClosedDaysUp()
    {
        foreach (var command in Exempt.Keys)
        {
            HasTimes(command).Should().BeTrue(command.Name);
            ValidatorsOf(command).Any(TakesWorkingDays).Should().BeFalse(Exempt[command]);
        }
    }
}
