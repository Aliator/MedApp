using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

[TestFixture]
public sealed class CreatePatientProcedureValidatorTests
{
    private readonly CreatePatientProcedureValidator _validator = new(new FixedTimeProvider(Now));

    private static CreatePatientProcedureCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, string.Empty, Today, "Medium", Staff);

    [Test]
    public void Validate_DueToday_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_DueYesterday_Fails()
    {
        _validator.Validate(Valid() with { DueDate = Today.AddDays(-1) }).IsValid.Should().BeFalse();
    }

    [TestCase("Critical")]
    [TestCase("")]
    [TestCase("1")]
    public void Validate_UnknownPriority_Fails(string priority)
    {
        _validator.Validate(Valid() with { Priority = priority }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoTemplate_Fails()
    {
        _validator.Validate(Valid() with { TemplateId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_TitleTooLong_Fails()
    {
        _validator.Validate(Valid() with { Title = new string('a', 201) }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        _validator.Validate(Valid() with { Notes = new string('a', MaxNotesLength + 1) }).IsValid.Should().BeFalse();
    }
}
