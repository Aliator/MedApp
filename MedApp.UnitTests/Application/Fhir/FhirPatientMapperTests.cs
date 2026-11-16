using FluentAssertions;
using MedApp.Application.Fhir.Common;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Fhir;

[TestFixture]
public sealed class FhirPatientMapperTests
{
    [Test]
    public void ToResource_PatientWithANumber_IsAUkCorePatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.NhsNumber = NhsNumber;
        patient.LastUpdated = new DateTime(2026, 11, 16, 9, 30, 0, 123, DateTimeKind.Utc);

        var resource = FhirPatientMapper.ToResource(patient);

        resource.ResourceType.Should().Be("Patient");
        resource.Id.Should().Be(patient.Id.ToString());
        resource.Meta!.Profile.Should().Equal(FhirPatientMapper.UkCorePatientProfile);
        resource.Meta.LastUpdated.Should().Be("2026-11-16T09:30:00.123Z");
        var identifier = resource.Identifier.Should().ContainSingle().Subject;
        identifier.System.Should().Be("https://fhir.nhs.uk/Id/nhs-number");
        identifier.Value.Should().Be(NhsNumber);
        var status = identifier.Extension.Should().ContainSingle().Subject;
        status.Url.Should().Be(FhirPatientMapper.NhsNumberVerificationStatusExtension);
        status.ValueCodeableConcept!.Coding.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            System = FhirPatientMapper.NhsNumberVerificationStatusSystem,
            Code = "02",
            Display = "Number present but not traced"
        });
        resource.Name.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Use = "official",
            Family = patient.LastName,
            Given = new[] { patient.FirstName }
        });
        resource.Telecom.Should().ContainSingle().Which.Should().BeEquivalentTo(new { System = "email", Value = patient.Email });
        resource.BirthDate.Should().Be(patient.DateOfBirth.ToString("yyyy-MM-dd"));
    }

    [Test]
    public void ToResource_NoNumberOrEmail_LeavesThemOut()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.Email = "";
        patient.FirstName = " ";

        var resource = FhirPatientMapper.ToResource(patient);

        resource.Identifier.Should().BeNull();
        resource.Telecom.Should().BeNull();
        resource.Name![0].Given.Should().BeNull();
    }

    [Test]
    public void ToSearchSet_Matches_AreEntriesWithFullUrls()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        var bundle = FhirPatientMapper.ToSearchSet([patient], "https://host/api/fhir/", "https://host/self", ActivityTestConstants.Now);

        bundle.ResourceType.Should().Be("Bundle");
        bundle.Type.Should().Be("searchset");
        bundle.Total.Should().Be(1);
        bundle.Timestamp.Should().Be("2026-11-16T09:30:00.000Z");
        bundle.Link.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Relation = "self", Url = "https://host/self" });
        var entry = bundle.Entry.Should().ContainSingle().Subject;
        entry.FullUrl.Should().Be($"https://host/api/fhir/Patient/{patient.Id}");
        entry.Resource!.Id.Should().Be(patient.Id.ToString());
        entry.Search!.Mode.Should().Be("match");
    }

    [Test]
    public void ToSearchSet_NoMatches_HasNoEntries()
    {
        var bundle = FhirPatientMapper.ToSearchSet([], "https://host/api/fhir", "https://host/self", ActivityTestConstants.Now);

        bundle.Total.Should().Be(0);
        bundle.Entry.Should().BeNull("FHIR JSON never has an empty list");
    }

    [TestCase("https://fhir.nhs.uk/Id/nhs-number|9990000018", "https://fhir.nhs.uk/Id/nhs-number", "9990000018", true)]
    [TestCase("9990000018", null, "9990000018", true)]
    [TestCase(" https://example.org/mrn | 123 ", "https://example.org/mrn", "123", false)]
    [TestCase("|9990000018", "", "9990000018", false)]
    public void FhirIdentifierSearch_Parse_SplitsSystemAndValue(string token, string? system, string value, bool isNhs)
    {
        var search = FhirIdentifierSearch.Parse(token);

        search.System.Should().Be(system);
        search.Value.Should().Be(value);
        search.IsForNhsNumber.Should().Be(isNhs);
    }
}
