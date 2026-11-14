using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

[TestFixture]
public sealed class FinishProcedureStageValidatorTests
{
    private readonly FinishProcedureStageValidator _validator = new();

    private static FinishProcedureStageCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), Staff);

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
