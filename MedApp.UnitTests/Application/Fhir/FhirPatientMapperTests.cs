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
    public void ToResource_ContactDetailsAndPractice_AreInUkCoresShape()
    {
        var practice = PracticesTestConstants.CreatePractice();
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.AddressLines = $"{PracticesTestConstants.AddressLine1}\n{PracticesTestConstants.AddressLine2}";
        patient.Postcode = PracticesTestConstants.Postcode;
        patient.MobilePhone = PracticesTestConstants.Mobile;
        patient.PracticeId = practice.Id;

        var resource = FhirPatientMapper.ToResource(patient, practice);

        resource.Address.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Use = "home",
            Line = new[] { PracticesTestConstants.AddressLine1, PracticesTestConstants.AddressLine2 },
            PostalCode = PracticesTestConstants.Postcode
        });
        resource.Telecom.Should().BeEquivalentTo(new object[]
        {
            new { System = "phone", Value = PracticesTestConstants.Mobile, Use = "mobile" },
            new { System = "email", Value = patient.Email, Use = (string?)null }
        }, options => options.WithStrictOrdering());
        resource.GeneralPractitioner.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Type = "Organization",
            Identifier = new { System = FhirPatientMapper.OdsOrganisationCodeSystem, Value = PracticesTestConstants.OdsCode },
            Display = PracticesTestConstants.PracticeName
        });
    }

    [Test]
    public void ToResource_PostcodeOnly_PracticeWithoutACode_OrAnotherPractice()
    {
        var practice = PracticesTestConstants.CreatePractice(odsCode: null);
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.Postcode = PracticesTestConstants.Postcode;
        patient.PracticeId = practice.Id;

        var resource = FhirPatientMapper.ToResource(patient, practice);

        resource.Address.Should().ContainSingle().Which.Line.Should().BeNull();
        resource.GeneralPractitioner.Should().ContainSingle().Which.Identifier.Should().BeNull();
        FhirPatientMapper.ToResource(patient, PracticesTestConstants.CreatePractice()).GeneralPractitioner.Should().BeNull();
        FhirPatientMapper.ToResource(patient).GeneralPractitioner.Should().BeNull();
    }

    [Test]
    public void ToResource_Anonymised_GivesNoContactDetailsOrPractice()
    {
        var practice = PracticesTestConstants.CreatePractice();
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.Postcode = PracticesTestConstants.Postcode;
        patient.PracticeId = practice.Id;
        patient.AnonymisedAtUtc = PracticesTestConstants.NowUtc;

        var resource = FhirPatientMapper.ToResource(patient, practice);

        resource.Address.Should().BeNull();
        resource.Telecom.Should().BeNull();
        resource.GeneralPractitioner.Should().BeNull();
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

    [TestCase("https://fhir.nhs.uk/Id/nhs-number|9990000018", "https://fhir.nhs.uk/Id/nhs-number", "9990000018", "9990000018")]
    [TestCase("https://fhir.nhs.uk/Id/nhs-number|999 000 0018", "https://fhir.nhs.uk/Id/nhs-number", "999 000 0018", "9990000018")]
    [TestCase("9990000018", null, "9990000018", "9990000018")]
    [TestCase("MRN123", null, "MRN123", null)]
    [TestCase("9990000019", null, "9990000019", null)]
    [TestCase("1111111111", null, "1111111111", null)]
    [TestCase(" https://example.org/mrn | 123 ", "https://example.org/mrn", "123", null)]
    [TestCase("|9990000018", "", "9990000018", null)]
    public void FhirIdentifierSearch_Parse_SplitsSystemAndValue_AndSaysWhichNhsNumberToMatch(
        string token, string? system, string value, string? nhsNumber)
    {
        var search = FhirIdentifierSearch.Parse(token);

        search.System.Should().Be(system);
        search.Value.Should().Be(value);
        search.NhsNumberToMatch.Should().Be(nhsNumber);
        search.NamesNhsNumberSystem.Should().Be(system == FhirPatientMapper.NhsNumberSystem);
    }

    [Test]
    public void ToResource_AnonymisedPatient_SaysSoInFhirsTerms_WithoutItsPlaceholders()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.FirstName = "Anonymised";
        patient.LastName = "Patient";
        patient.Email = "anonymised@anonymised.invalid";
        patient.DateOfBirth = new DateOnly(1980, 1, 1);
        patient.AnonymisedAtUtc = ActivityTestConstants.Now;

        var resource = FhirPatientMapper.ToResource(patient);

        resource.Active.Should().BeFalse();
        resource.Name.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Use = "anonymous",
            Text = "Anonymised",
            Family = (string?)null,
            Given = (List<string>?)null
        });
        resource.BirthDate.Should().Be("1980");
        resource.Telecom.Should().BeNull();
        resource.Identifier.Should().BeNull();
        resource.Meta!.Profile.Should().Equal(FhirPatientMapper.UkCorePatientProfile);
    }

    [Test]
    public void ToResource_ActivePatient_IsActive_WithoutTags()
    {
        var resource = FhirPatientMapper.ToResource(PatientsTestConstants.CreateValidPatient());

        resource.Active.Should().BeTrue();
        resource.Meta!.Tag.Should().BeNull();
    }

    [Test]
    public void ToResource_RestrictedAndDisputed_AreTagged()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = ActivityTestConstants.Now;
        patient.DisputedAtUtc = ActivityTestConstants.Now;

        var tags = FhirPatientMapper.ToResource(patient).Meta!.Tag;

        tags.Should().BeEquivalentTo(new[]
        {
            new { System = FhirPatientMapper.RecordFlagSystem, Code = "restricted", Display = FhirPatientMapper.RestrictedDisplay },
            new { System = FhirPatientMapper.RecordFlagSystem, Code = "disputed", Display = FhirPatientMapper.DisputedDisplay }
        });
    }

    [Test]
    public void ToResource_DisputedOnly_HasOneTag()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.DisputedAtUtc = ActivityTestConstants.Now;

        FhirPatientMapper.ToResource(patient).Meta!.Tag.Should().ContainSingle().Which.Code.Should().Be("disputed");
    }

    [Test]
    public void Capability_Statement_IsPatientReadAndSearchByIdentifier_InJson()
    {
        var statement = FhirCapability.Statement(ActivityTestConstants.Now);

        statement.ResourceType.Should().Be("CapabilityStatement");
        statement.FhirVersion.Should().Be("4.0.1");
        statement.Format.Should().Equal("json");
        statement.Date.Should().Be("2026-11-16T09:30:00.000Z");
        var patient = statement.Rest.Single().Resource.Single();
        patient.Type.Should().Be("Patient");
        patient.Profile.Should().Be(FhirPatientMapper.UkCorePatientProfile);
        patient.Interaction.Select(i => i.Code).Should().Equal("read", "search-type");
        patient.SearchParam!.Single().Should().BeEquivalentTo(new { Name = "identifier", Type = "token" });
    }
}
