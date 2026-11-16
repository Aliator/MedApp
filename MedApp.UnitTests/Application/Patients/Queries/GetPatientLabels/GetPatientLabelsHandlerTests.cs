using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Queries.GetPatientLabels;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.GetPatientLabels;

/// <summary>The side nav's pin labels: names only, in the order asked, and nothing recorded.</summary>
[TestFixture]
public sealed class GetPatientLabelsHandlerTests
{
    private static readonly Guid First = Guid.Parse("a0000000-0000-4000-8000-000000000001");
    private static readonly Guid Second = Guid.Parse("a0000000-0000-4000-8000-000000000002");
    private static readonly Guid Gone = Guid.Parse("a0000000-0000-4000-8000-000000000009");

    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _activity = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(_activity);
    }

    [Test]
    public async Task Handle_Ids_ReturnsTheirNamesInOrder_LeavingOutGoneOnesAndRepeats_AndRecordsNothing()
    {
        _repository
            .Setup(r => r.GetNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [First] = "Freya Ahmed", [Second] = "Priya Ali" });
        using var cts = new CancellationTokenSource();

        var labels = await _fixture.Create<GetPatientLabelsHandler>().Handle(new GetPatientLabelsQuery([Second, Gone, First, Second]), cts.Token);

        labels.Should().Equal(new PatientLabelResponse(Second, "Priya Ali"), new PatientLabelResponse(First, "Freya Ahmed"));
        _repository.Verify(r => r.GetNamesAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { Second, Gone, First })), cts.Token), Times.Once);
        _activity.Recorded.Should().BeEmpty();
        _activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NoIds_ReturnsNone_WithoutAskingTheDatabase()
    {
        var labels = await _fixture.Create<GetPatientLabelsHandler>().Handle(new GetPatientLabelsQuery([]), CancellationToken.None);

        labels.Should().BeEmpty();
        _repository.VerifyNoOtherCalls();
    }
}
