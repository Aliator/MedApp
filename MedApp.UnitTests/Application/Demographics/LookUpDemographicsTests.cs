using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Demographics.Common;
using MedApp.Application.Demographics.Queries.GetDemographicsStatus;
using MedApp.Application.Demographics.Queries.LookUpDemographics;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Activity;
using MedApp.Infrastructure.Demographics;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Demographics;

/// <summary>The demographics (PDS) lookup: off by default, the offline stub's synthetic people, and the activity log.</summary>
[TestFixture]
public sealed class LookUpDemographicsTests
{
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp() => _activity = new RecordingActivityLog();

    private LookUpDemographicsHandler Handler(IPersonalDemographicsService service) => new(service, _activity);

    [Test]
    public async Task Handle_KnownNumberWithSpaces_ReturnsThePerson_AndRecordsFoundWithoutTheNumber()
    {
        using var cts = new CancellationTokenSource();

        var person = await Handler(new OfflinePersonalDemographicsService()).Handle(new LookUpDemographicsQuery(NhsNumberWithSpaces), cts.Token);

        person.Should().BeEquivalentTo(new
        {
            NhsNumber,
            FirstName = "Ada",
            LastName = "Synthetic",
            DateOfBirth = new DateOnly(1980, 5, 17),
            Source = OfflinePersonalDemographicsService.SourceName
        });
        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.DemographicsLookedUp);
        entry.Details.Should().Be("""{"found":true}""").And.NotContain(NhsNumber);
        entry.PatientId.Should().BeNull();
        _activity.Tokens.Should().Equal(cts.Token);
    }

    [Test]
    public async Task Handle_UnknownNumber_ThrowsNotFound_AndRecordsNotFound()
    {
        var act = () => Handler(new OfflinePersonalDemographicsService()).Handle(new LookUpDemographicsQuery("9990000050"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(LookUpDemographicsHandler.NotFound);
        _activity.Recorded.Should().ContainSingle().Which.Details.Should().Be("""{"found":false}""");
    }

    [Test]
    public async Task Handle_NoLookupSetUp_ThrowsNotFound_AndRecordsNothing()
    {
        var act = () => Handler(new NoPersonalDemographicsService()).Handle(new LookUpDemographicsQuery(NhsNumber), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(LookUpDemographicsHandler.NotSetUp);
        _activity.Recorded.Should().BeEmpty();
    }

    [TestCase("", LookUpDemographicsValidator.Required)]
    [TestCase(WrongCheckDigit, NhsNumberRules.WrongCheckDigit)]
    [TestCase(NineDigits, NhsNumberRules.NotTenDigits)]
    public void Validate_MissingOrInvalidNumber_FailsOnce(string number, string message) =>
        new LookUpDemographicsValidator().Validate(new LookUpDemographicsQuery(number))
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);

    [Test]
    public void Validate_ValidNumber_Passes() =>
        new LookUpDemographicsValidator().Validate(new LookUpDemographicsQuery(NhsNumberWithSpaces)).IsValid.Should().BeTrue();

    [Test]
    public async Task GetStatus_SaysWhetherALookupIsSetUp()
    {
        var on = await new GetDemographicsStatusHandler(new OfflinePersonalDemographicsService())
            .Handle(new GetDemographicsStatusQuery(), CancellationToken.None);
        var off = await new GetDemographicsStatusHandler(new NoPersonalDemographicsService())
            .Handle(new GetDemographicsStatusQuery(), CancellationToken.None);

        on.IsAvailable.Should().BeTrue();
        on.Source.Should().Be(OfflinePersonalDemographicsService.SourceName);
        off.IsAvailable.Should().BeFalse();
        off.Source.Should().BeNull();
    }

    [Test]
    public void OfflineStub_KnowsOnlyValidSyntheticTestNumbers()
    {
        OfflinePersonalDemographicsService.People.Should().NotBeEmpty().And.AllSatisfy(p =>
        {
            p.NhsNumber.Should().StartWith("999");
            MedApp.Domain.Patients.NhsNumbers.IsValid(p.NhsNumber).Should().BeTrue();
        });
    }

    [Test]
    public async Task NoLookup_FindsNobody()
    {
        var none = new NoPersonalDemographicsService();

        none.IsAvailable.Should().BeFalse();
        none.Source.Should().BeNull();
        (await none.LookUpAsync(NhsNumber, CancellationToken.None)).Should().BeNull();
    }
}
