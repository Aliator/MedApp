using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Fhir.Queries.GetFhirPatient;
using MedApp.Application.Fhir.Queries.SearchFhirPatients;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Fhir;

/// <summary>Reading and searching patients through the FHIR API, and what they record in the activity log.</summary>
[TestFixture]
public sealed class FhirPatientQueriesTests
{
    private const string BaseUrl = "https://host/api/fhir";
    private const string NhsIdentifier = $"https://fhir.nhs.uk/Id/nhs-number|{NhsNumber}";

    private Mock<IPatientRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;
    private Patient _patient = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IPatientRepository>();
        _activity = new RecordingActivityLog();
        _patient = PatientsTestConstants.CreateValidPatient();
        _patient.NhsNumber = NhsNumber;
        _repository.Setup(r => r.GetByIdAsync(_patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
        _repository.Setup(r => r.GetByNhsNumberAsync(NhsNumber, It.IsAny<CancellationToken>())).ReturnsAsync(_patient);
    }

    private GetFhirPatientHandler Read() => new(_repository.Object, _activity);

    private SearchFhirPatientsHandler Search() => new(_repository.Object, _activity, new FixedTimeProvider(ActivityTestConstants.Now));

    [Test]
    public async Task Read_KnownPatient_ReturnsThem_AndRecordsAView()
    {
        using var cts = new CancellationTokenSource();

        var resource = await Read().Handle(new GetFhirPatientQuery(_patient.Id.ToString()), cts.Token);

        resource.Id.Should().Be(_patient.Id.ToString());
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.PatientViewed, new List<Guid> { _patient.Id }, (Guid?)null));
        _repository.Verify(r => r.GetByIdAsync(_patient.Id, cts.Token));
    }

    [TestCase("not-a-guid")]
    [TestCase("00000000-0000-0000-0000-000000000001")]
    public async Task Read_UnknownId_ThrowsNotFound_AndRecordsNothing(string id)
    {
        var act = () => Read().Handle(new GetFhirPatientQuery(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{id}*");
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Search_ByNhsNumber_ReturnsABundleOfThem_AndRecordsTheSearchWithoutTheNumber()
    {
        var bundle = await Search().Handle(new SearchFhirPatientsQuery(NhsIdentifier, BaseUrl + "/"), CancellationToken.None);

        bundle.Total.Should().Be(1);
        bundle.Entry!.Single().FullUrl.Should().Be($"{BaseUrl}/Patient/{_patient.Id}");
        bundle.Link!.Single().Url.Should().Be($"{BaseUrl}/Patient?identifier={Uri.EscapeDataString(NhsIdentifier)}");
        var search = _activity.Recorded.Should().ContainSingle().Subject;
        search.Action.Should().Be(ActivityAction.PatientsSearched);
        search.Details.Should().Be("""{"filters":["nhsNumber"],"resultCount":1,"via":"fhir"}""");
        _activity.Views.Should().ContainSingle().Which.Should().BeEquivalentTo((ActivityAction.PatientListed, new List<Guid> { _patient.Id }, (Guid?)null));
    }

    [Test]
    public async Task Search_NumberWithoutASystem_MatchesTheNhsNumber()
    {
        var bundle = await Search().Handle(new SearchFhirPatientsQuery(NhsNumber, BaseUrl), CancellationToken.None);

        bundle.Total.Should().Be(1);
    }

    [Test]
    public async Task Search_NobodyHasTheNumber_ReturnsAnEmptyBundle()
    {
        var bundle = await Search().Handle(
            new SearchFhirPatientsQuery($"https://fhir.nhs.uk/Id/nhs-number|{OtherNhsNumber}", BaseUrl), CancellationToken.None);

        bundle.Total.Should().Be(0);
        bundle.Entry.Should().BeNull();
        _activity.Recorded.Single().Details.Should().Contain("\"resultCount\":0");
    }

    [Test]
    public async Task Search_AnotherSystem_MatchesNothing_WithoutLookingItUp()
    {
        var bundle = await Search().Handle(new SearchFhirPatientsQuery("https://example.org/mrn|123", BaseUrl), CancellationToken.None);

        bundle.Total.Should().Be(0);
        _repository.Verify(r => r.GetByNhsNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(null, SearchFhirPatientsValidator.IdentifierRequired)]
    [TestCase(" ", SearchFhirPatientsValidator.IdentifierRequired)]
    [TestCase("https://fhir.nhs.uk/Id/nhs-number|", SearchFhirPatientsValidator.IdentifierRequired)]
    [TestCase("https://fhir.nhs.uk/Id/nhs-number|9990000019", NhsNumberRules.WrongCheckDigit)]
    [TestCase("12345", NhsNumberRules.NotTenDigits)]
    public void Validate_MissingOrInvalidIdentifier_FailsOnIdentifier(string? identifier, string message)
    {
        var result = new SearchFhirPatientsValidator().Validate(new SearchFhirPatientsQuery(identifier, BaseUrl));

        result.Errors.Should().ContainSingle().Which.Should().Match<FluentValidation.Results.ValidationFailure>(
            e => e.PropertyName == "identifier" && e.ErrorMessage == message);
    }

    [TestCase(NhsIdentifier)]
    [TestCase(NhsNumber)]
    [TestCase("https://example.org/mrn|not-checked")]
    public void Validate_NhsNumberOrAnotherSystem_Passes(string identifier) =>
        new SearchFhirPatientsValidator().Validate(new SearchFhirPatientsQuery(identifier, BaseUrl)).IsValid.Should().BeTrue();
}
