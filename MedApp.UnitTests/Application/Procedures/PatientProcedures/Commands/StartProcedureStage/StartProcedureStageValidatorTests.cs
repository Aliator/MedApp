using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;

[TestFixture]
public sealed class StartProcedureStageValidatorTests
{
    private readonly StartProcedureStageValidator _validator = new();

    private static StartProcedureStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Staff);

    [Test]
    public void Validate_BothIds_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoProcedure_Fails()
    {
        _validator.Validate(Valid() with { ProcedureId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_NoStageId_Fails()
    {
        _validator.Validate(Valid() with { StageId = Guid.Empty }).IsValid.Should().BeFalse();
    }
}
