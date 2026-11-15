using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.UnitTests.Application.Procedures.Common;

[TestFixture]
public sealed class ProcedureEnumsTests
{
    [TestCase("high", PatientProcedurePriority.High)]
    [TestCase(" Urgent ", PatientProcedurePriority.Urgent)]
    public void Parse_KnownName_ReturnsTheValueIgnoringCaseAndSpaces(string value, PatientProcedurePriority expected)
    {
        ProcedureEnums.Parse<PatientProcedurePriority>(value).Should().Be(expected);
    }

    [TestCase("Whenever")]
    [TestCase("1")]
    [TestCase(" ")]
    public void Parse_UnknownNameOrNumber_ThrowsArgumentException(string value)
    {
        var act = () => ProcedureEnums.Parse<PatientProcedurePriority>(value);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"'{value}' is not a {nameof(PatientProcedurePriority)}.*")
            .Which.ParamName.Should().Be("value");
    }

    [TestCase("InReview", true)]
    [TestCase("inreview", true)]
    [TestCase("Finished", false)]
    [TestCase("3", false)]
    [TestCase(null, false)]
    public void IsStatus_Value_AcceptsOnlyStatusNames(string? value, bool expected)
    {
        ProcedureEnums.IsStatus(value).Should().Be(expected);
    }
}
