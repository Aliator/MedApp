using FluentAssertions;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.Audit.Queries.SearchProcedureAudit;

[TestFixture]
public sealed class SearchProcedureAuditValidatorTests
{
    private readonly SearchProcedureAuditValidator _validator = new();

    [Test]
    public void Validate_DefaultQuery_Passes()
    {
        _validator.Validate(CreateAuditSearchQuery()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_KnownAction_Passes()
    {
        _validator.Validate(CreateAuditSearchQuery() with { Action = "StageUnlocked" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_UnknownAction_Fails()
    {
        _validator.Validate(CreateAuditSearchQuery() with { Action = "Exploded" }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_PageZero_Fails()
    {
        _validator.Validate(CreateAuditSearchQuery() with { Page = 0 }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_RangeEndsBeforeItStarts_Fails()
    {
        _validator.Validate(CreateAuditSearchQuery() with { From = Today, To = Today.AddDays(-1) }).IsValid.Should().BeFalse();
    }
}
