using FluentAssertions;
using FluentValidation;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>
/// Every command that puts something on a calendar checks the person's working day from one place (decisions 72 and
/// 120): its validator takes <see cref="WorkingDays"/>, which looks up the day's closed days and the person's hours. A
/// new command with a time and no such validator would let a closed day, or a time outside someone's hours, through,
/// so this finds them by their shape. A walk-in (<see cref="RecordPastAppointmentCommand"/>) takes it too, for the
/// person's hours, and asks it to leave closed days out (decision 74).
/// </summary>
[TestFixture]
public sealed class WorkingDaysConventionTests
{
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

        foreach (var command in timed)
            ValidatorsOf(command).Any(TakesWorkingDays).Should().BeTrue($"{command.Name} puts something on a calendar, so a closed day and the person's hours must be checked");
    }

    [Test]
    public void ApplyingADayTemplate_IsValidatedWithTheClosedDays()
    {
        // A date rather than a time: templates don't apply on a closed day, so there's nothing to choose.
        ValidatorsOf(typeof(ApplyDayTemplateCommand)).Any(TakesWorkingDays).Should().BeTrue();
    }

    [Test]
    public void AWalkIn_IsValidatedWithTheHours_ButNotTheClosedDays()
    {
        HasTimes(typeof(RecordPastAppointmentCommand)).Should().BeTrue();
        ValidatorsOf(typeof(RecordPastAppointmentCommand)).Any(TakesWorkingDays).Should().BeTrue(
            "a walk-in is inside the person's hours, though it may be on a closed day: what happened, happened (decision 74)");
    }
}
