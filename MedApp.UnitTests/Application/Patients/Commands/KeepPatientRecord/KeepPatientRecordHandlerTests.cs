using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Commands.KeepPatientRecord;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Commands.KeepPatientRecord;

[TestFixture]
public sealed class KeepPatientRecordHandlerTests
{
    private static readonly DateOnly KeepUntil = new(2027, 11, 16);

    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRepository>();
        _fixture.Inject(_repository.Object);
        _fixture.Inject<IActivityLog>(new RecordingActivityLog());
    }

    private KeepPatientRecordHandler Handler => _fixture.Create<KeepPatientRecordHandler>();

    [Test]
    public async Task Handle_UnknownPatient_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Patient?)null);

        var act = () => Handler.Handle(new KeepPatientRecordCommand(Guid.NewGuid(), KeepUntil, "Why"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Anonymised_ThrowsConflict()
    {
        var patient = PrivacyTestConstants.CreateAnonymisedPatient();
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);

        var act = () => Handler.Handle(new KeepPatientRecordCommand(patient.Id, KeepUntil, "Why"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_Kept_SetsTheReviewDateAndReason_AndRecordsThem()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();
        _repository.Setup(r => r.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new KeepPatientRecordCommand(patient.Id, KeepUntil, " Legal hold "), cts.Token);

        patient.RetentionKeptUntil.Should().Be(KeepUntil);
        patient.RetentionKeptReason.Should().Be("Legal hold");
        _repository.Verify(r => r.UpdateAsync(
            patient,
            It.Is<ActivityEntry>(e => e.Action == ActivityAction.RetentionKept && e.PatientId == patient.Id
                                      && e.Details == """{"reason":"Legal hold","keepUntil":"2027-11-16"}"""),
            cts.Token), Times.Once);
    }
}
