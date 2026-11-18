using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;

/// <summary>The side nav's pinned pathway labels: title and patient name only, in the order asked, and nothing recorded.</summary>
[TestFixture]
public sealed class GetPatientPathwayLabelsHandlerTests : PathwayHandlerTestBase
{
    private static readonly Guid Knee = Guid.Parse("c0000000-0000-4000-8000-000000000001");
    private static readonly Guid Scope = Guid.Parse("c0000000-0000-4000-8000-000000000002");
    private static readonly Guid Gone = Guid.Parse("c0000000-0000-4000-8000-000000000009");

    [Test]
    public async Task Handle_Ids_ReturnsTheirLabelsInOrder_LeavingOutGoneOnesAndRepeats_AndRecordsNothing()
    {
        var activity = new RecordingActivityLog();
        Fixture.Inject<IActivityLog>(activity);
        Repository
            .Setup(r => r.GetLabelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PathwayLabel(Knee, "Total knee replacement", "Freya", "Ahmed"), new PathwayLabel(Scope, "Bronchoscopy", "Priya", "Ali")]);
        using var cts = new CancellationTokenSource();

        var labels = await Fixture.Create<GetPatientPathwayLabelsHandler>().Handle(new GetPatientPathwayLabelsQuery([Scope, Gone, Knee, Scope]), cts.Token);

        labels.Should().Equal(
            new PatientPathwayLabelResponse(Scope, "Bronchoscopy", "Priya Ali"),
            new PatientPathwayLabelResponse(Knee, "Total knee replacement", "Freya Ahmed"));
        Repository.Verify(r => r.GetLabelsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { Scope, Gone, Knee })), cts.Token), Times.Once);
        activity.Recorded.Should().BeEmpty();
        activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NoIds_ReturnsNone_WithoutAskingTheDatabase()
    {
        var labels = await Fixture.Create<GetPatientPathwayLabelsHandler>().Handle(new GetPatientPathwayLabelsQuery([]), CancellationToken.None);

        labels.Should().BeEmpty();
        Repository.Verify(r => r.GetLabelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void ToLabel_JoinsThePatientsName()
    {
        PathwayMapper.ToLabel(new PathwayLabel(Knee, "Knee", "Freya", "Ahmed"))
            .Should().Be(new PatientPathwayLabelResponse(Knee, "Knee", "Freya Ahmed"));
    }
}
