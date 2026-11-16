using FluentAssertions;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

/// <summary>The patient search's NHS number filter.</summary>
[TestFixture]
public sealed class PatientSearchNhsNumberTests
{
    private static Patient WithNumber(string lastName, string? nhsNumber)
    {
        var patient = PatientsTestConstants.CreatePatient("Synthetic", lastName, new DateOnly(1980, 1, 1), $"{lastName}@example.com");
        patient.NhsNumber = nhsNumber;
        return patient;
    }

    private static readonly Patient First = WithNumber("First", NhsNumber);
    private static readonly Patient Second = WithNumber("Second", OtherNhsNumber);
    private static readonly Patient NoNumber = WithNumber("None", null);

    private static IQueryable<Patient> Patients() => new[] { First, Second, NoNumber }.AsQueryable();

    private static SearchPatientsQuery Query(string? nhsNumber) => PatientsTestConstants.CreateSearchQuery() with { NhsNumber = nhsNumber };

    [Test]
    public void Filter_WholeNumberWithSpaces_MatchesThatPatient()
    {
        Patients().Filter(Query(NhsNumberWithSpaces)).Should().ContainSingle().Which.Should().Be(First);
    }

    [Test]
    public void Filter_PartOfANumber_MatchesEveryNumberContainingIt_NeverOneWithout()
    {
        Patients().Filter(Query("000 00")).Should().BeEquivalentTo([First, Second]);
        Patients().Filter(Query("0026")).Should().ContainSingle().Which.Should().Be(Second);
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void Filter_NoNumber_IsIgnored(string? nhsNumber) => Patients().Filter(Query(nhsNumber)).Should().HaveCount(3);

    [Test]
    public void FiltersUsed_NhsNumber_IsNamed()
    {
        PatientSearch.FiltersUsed(Query(NhsNumber)).Should().Equal("nhsNumber");
        PatientSearch.FiltersUsed(Query(" ")).Should().BeEmpty();
    }

    [TestCase(NhsNumber, true)]
    [TestCase("999 000", true)]
    [TestCase(" ", true)]
    [TestCase("99900000181", false)]
    [TestCase("999-000", false)]
    public void Validate_NhsNumberFilter_IsUpToTenDigits(string nhsNumber, bool isValid)
    {
        var result = new SearchPatientsValidator().Validate(Query(nhsNumber));

        result.IsValid.Should().Be(isValid);
        if (!isValid)
            result.Errors.Should().ContainSingle(e => e.PropertyName == "NhsNumber");
    }
}
