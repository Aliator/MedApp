using FluentAssertions;
using MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;
using MedApp.Application.Appointments.Common;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.UpdateAppointmentNotes;

[TestFixture]
public sealed class UpdateAppointmentNotesValidatorTests
{
    private readonly UpdateAppointmentNotesValidator _validator = new();

    private static UpdateAppointmentNotesCommand Valid() => new(Guid.NewGuid(), Notes, Colleague);

    private IEnumerable<string> Errors(UpdateAppointmentNotesCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_Notes_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyNotes_Passes()
    {
        _validator.Validate(Valid() with { Notes = string.Empty }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoAppointment_Fails()
    {
        Errors(Valid() with { AppointmentId = Guid.Empty }).Should().Equal("Appointment id is required.");
    }

    [Test]
    public void Validate_NullNotes_Fails()
    {
        Errors(Valid() with { Notes = null! }).Should().Equal("Notes must not be null.");
    }

    [Test]
    public void Validate_NotesAtTheLimit_Passes()
    {
        _validator.Validate(Valid() with { Notes = new string('a', AppointmentLimits.MaxNotesLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        Errors(Valid() with { Notes = new string('a', AppointmentLimits.MaxNotesLength + 1) })
            .Should().Equal("Notes must not exceed 2000 characters.");
    }
}
