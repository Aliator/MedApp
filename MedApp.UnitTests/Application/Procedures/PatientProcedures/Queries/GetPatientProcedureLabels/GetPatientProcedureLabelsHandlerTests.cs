using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;

/// <summary>The side nav's pinned procedure labels: title and patient name only, in the order asked, and nothing recorded.</summary>
[TestFixture]
public sealed class GetPatientProcedureLabelsHandlerTests : ProcedureHandlerTestBase
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
            .ReturnsAsync([new ProcedureLabel(Knee, "Total knee replacement", "Freya", "Ahmed"), new ProcedureLabel(Scope, "Bronchoscopy", "Priya", "Ali")]);
        using var cts = new CancellationTokenSource();

        var labels = await Fixture.Create<GetPatientProcedureLabelsHandler>().Handle(new GetPatientProcedureLabelsQuery([Scope, Gone, Knee, Scope]), cts.Token);

        labels.Should().Equal(
            new PatientProcedureLabelResponse(Scope, "Bronchoscopy", "Priya Ali"),
            new PatientProcedureLabelResponse(Knee, "Total knee replacement", "Freya Ahmed"));
        Repository.Verify(r => r.GetLabelsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { Scope, Gone, Knee })), cts.Token), Times.Once);
        activity.Recorded.Should().BeEmpty();
        activity.Views.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_NoIds_ReturnsNone_WithoutAskingTheDatabase()
    {
        var labels = await Fixture.Create<GetPatientProcedureLabelsHandler>().Handle(new GetPatientProcedureLabelsQuery([]), CancellationToken.None);

        labels.Should().BeEmpty();
        Repository.Verify(r => r.GetLabelsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void ToLabel_JoinsThePatientsName()
    {
        ProcedureMapper.ToLabel(new ProcedureLabel(Knee, "Knee", "Freya", "Ahmed"))
            .Should().Be(new PatientProcedureLabelResponse(Knee, "Knee", "Freya Ahmed"));
    }
}
