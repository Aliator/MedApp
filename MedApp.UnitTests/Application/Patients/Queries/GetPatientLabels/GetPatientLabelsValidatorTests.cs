using FluentAssertions;
using MedApp.Application.Patients.Queries.GetPatientLabels;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;

namespace MedApp.UnitTests.Application.Patients.Queries.GetPatientLabels;

/// <summary>Label lookups take at most 20 IDs, so they can't be used to list records.</summary>
[TestFixture]
public sealed class GetPatientLabelsValidatorTests
{
    private static List<Guid> Ids(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    [TestCase(0)]
    [TestCase(GetPatientLabelsValidator.MaxIds)]
    public void Validate_UpToTheLimit_IsValid(int count)
    {
        new GetPatientLabelsValidator().Validate(new GetPatientLabelsQuery(Ids(count))).IsValid.Should().BeTrue();
        new GetPatientProcedureLabelsValidator().Validate(new GetPatientProcedureLabelsQuery(Ids(count))).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_OverTheLimit_IsInvalid()
    {
        new GetPatientLabelsValidator().Validate(new GetPatientLabelsQuery(Ids(GetPatientLabelsValidator.MaxIds + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Ids").Which.ErrorMessage.Should().Contain("at most 20 patients");
        new GetPatientProcedureLabelsValidator().Validate(new GetPatientProcedureLabelsQuery(Ids(GetPatientLabelsValidator.MaxIds + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Ids").Which.ErrorMessage.Should().Contain("at most 20 procedures");
    }
}
