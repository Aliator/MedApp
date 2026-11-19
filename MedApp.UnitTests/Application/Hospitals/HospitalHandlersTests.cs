using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Hospitals.Common;
using MedApp.Application.Hospitals.Repositories;
using MedApp.Application.Hospitals.Setup;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Letters;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Hospitals;

/// <summary>Hospitals' setup (MC18): CRUD, archive and restore, and their checks.</summary>
[TestFixture]
public sealed class HospitalHandlersTests
{
    private Mock<IHospitalRepository> _repository = null!;
    private HospitalHandlers _handlers = null!;
    private Hospital _hospital = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IHospitalRepository>();
        _hospital = HospitalsTestConstants.Hospital();
        _repository.Setup(r => r.GetAsync(_hospital.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_hospital);
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        _handlers = new HospitalHandlers(_repository.Object, new FixedTimeProvider(PathwaysTestConstants.Now));
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task Create_TrimsTheDetails_CapitalisesTheOdsCode_AndSaves()
    {
        Hospital? added = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<Hospital>(), _cts.Token)).Callback<Hospital, CancellationToken>((h, _) => added = h);

        var response = await _handlers.Handle(
            new CreateHospitalCommand(" Hillcrest (test) ", " rxx02 ", "  ", "Post", 14), _cts.Token);

        added.Should().NotBeNull();
        response.Should().BeEquivalentTo(new
        {
            Name = "Hillcrest (test)", OdsCode = "RXX02", Contact = (string?)null, ReportDeliveryMethod = "Post",
            DefaultDeadlineDays = 14, IsArchived = false, CanBeDeleted = true, ExpectedVersion = 1,
        });
    }

    [Test]
    public async Task Create_ANameOrOdsCodeTaken_IsA409()
    {
        _repository.Setup(r => r.NameExistsAsync("Taken (test)", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await FluentActions.Awaiting(() => _handlers.Handle(new CreateHospitalCommand("Taken (test)", "RXX03", null, "Post", 10), _cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage("There's already a hospital called 'Taken (test)'.");

        _repository.Setup(r => r.OdsCodeExistsAsync("RXX01", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        (await FluentActions.Awaiting(() => _handlers.Handle(new CreateHospitalCommand("New (test)", "rxx01", null, "Post", 10), _cts.Token))
            .Should().ThrowAsync<ConflictException>()).WithMessage("There's already a hospital with the ODS code 'RXX01'.");
    }

    [Test]
    public async Task Update_ChangesIt_AndMovesItsVersionOn()
    {
        var response = await _handlers.Handle(
            new UpdateHospitalCommand(_hospital.Id, "Riverside (test)", "RXX01", "Lab office", "EmailOutsideMedApp", 7), _cts.Token);

        _hospital.ReportDeliveryMethod.Should().Be(LetterDispatchMethod.EmailOutsideMedApp);
        response.ExpectedVersion.Should().Be(2);
        response.DefaultDeadlineDays.Should().Be(7);
        _repository.Verify(r => r.UpdateAsync(_hospital, _cts.Token), Times.Once);
    }

    [Test]
    public async Task ArchiveAndRestore_FlipIt()
    {
        (await _handlers.Handle(new SetHospitalArchivedCommand(_hospital.Id, true), _cts.Token)).IsArchived.Should().BeTrue();
        (await _handlers.Handle(new SetHospitalArchivedCommand(_hospital.Id, false), _cts.Token)).IsArchived.Should().BeFalse();
        _repository.Verify(r => r.UpdateAsync(_hospital, _cts.Token), Times.Exactly(2));
    }

    [Test]
    public async Task Delete_OneThatSentRequests_IsA409_AnUnknownOneA404()
    {
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { _hospital.Id });
        (await FluentActions.Awaiting(() => _handlers.Handle(new DeleteHospitalCommand(_hospital.Id), _cts.Token))
            .Should().ThrowAsync<ConflictException>())
            .WithMessage($"'{HospitalsTestConstants.HospitalName}' has sent requests, so it can't be deleted. Archive it instead.");

        await FluentActions.Awaiting(() => _handlers.Handle(new DeleteHospitalCommand(Guid.NewGuid()), _cts.Token))
            .Should().ThrowAsync<NotFoundException>();
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAll_SaysWhichCanBeDeleted()
    {
        var other = HospitalsTestConstants.Hospital();
        _repository.Setup(r => r.GetAllAsync(true, _cts.Token)).ReturnsAsync([_hospital, other]);
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), _cts.Token))
            .ReturnsAsync(new HashSet<Guid> { _hospital.Id });

        var hospitals = await _handlers.Handle(new GetHospitalsQuery(true), _cts.Token);

        hospitals.Select(h => h.CanBeDeleted).Should().Equal(false, true);
    }

    [TestCase(" ", "RXX01", "Post", 10, HospitalRules.NameMissing)]
    [TestCase("Name", "R1", "Post", 10, HospitalRules.OdsCodeInvalid)]
    [TestCase("Name", "RX-01", "Post", 10, HospitalRules.OdsCodeInvalid)]
    [TestCase("Name", "RXX01", "Fax", 10, HospitalRules.DeliveryMethodInvalid)]
    [TestCase("Name", "RXX01", "Post", 0, "The default deadline must be 1 to 90 days.")]
    [TestCase("Name", "RXX01", "Post", 91, "The default deadline must be 1 to 90 days.")]
    public void Validator_RefusesBadDetails(string name, string ods, string method, int days, string message) =>
        new CreateHospitalValidator().Validate(new CreateHospitalCommand(name, ods, null, method, days)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == message);

    [Test]
    public void Validator_AcceptsGoodDetails_AndLimitsTheContact()
    {
        new UpdateHospitalValidator().Validate(new UpdateHospitalCommand(Guid.NewGuid(), "Name", "rxx01", "Office", "Other", 90))
            .IsValid.Should().BeTrue();
        new CreateHospitalValidator().Validate(new CreateHospitalCommand("Name", "RXX01", new string('x', 501), "Post", 10)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == HospitalRules.ContactTooLong);
    }

    [Test]
    public void Mapper_GivesItsFields()
    {
        HospitalMapper.ToResponse(_hospital, inUse: true).Should().BeEquivalentTo(new
        {
            _hospital.Id, Name = HospitalsTestConstants.HospitalName, OdsCode = "RXX01", ReportDeliveryMethod = "SecureEmail",
            DefaultDeadlineDays = 10, IsArchived = false, CanBeDeleted = false, ExpectedVersion = 1,
        });
    }
}
