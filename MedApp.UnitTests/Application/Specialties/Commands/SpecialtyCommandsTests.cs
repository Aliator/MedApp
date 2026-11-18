using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specialties.Commands.CreateSpecialty;
using MedApp.Application.Specialties.Commands.DeleteSpecialty;
using MedApp.Application.Specialties.Commands.SetSpecialtyArchived;
using MedApp.Application.Specialties.Commands.UpdateSpecialty;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Specialties.Commands;

/// <summary>Adding, renaming, archiving, restoring and deleting specialties.</summary>
[TestFixture]
public sealed class SpecialtyCommandsTests
{
    private IFixture _fixture = null!;
    private Mock<ISpecialtyRepository> _repository = null!;
    private Specialty _specialty = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _specialty = CreateSpecialty();
        _repository = RepositoryWith(_specialty);
        _fixture.Inject(_repository.Object);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(NowUtc));
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private void GivenUsage(SpecialtyUsage usage) =>
        _repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SpecialtyUsage> { [_specialty.Id] = usage });

    // Create

    [Test]
    public async Task Create_NewName_AddsItTrimmedAtVersionOne()
    {
        Specialty? added = null;
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()))
            .Callback((Specialty s, CancellationToken _) => added = s);

        var response = await _fixture.Create<CreateSpecialtyHandler>()
            .Handle(new CreateSpecialtyCommand($"  {OtherSpecialtyName} "), _cts.Token);

        added!.Name.Should().Be(OtherSpecialtyName);
        added.Version.Should().Be(1);
        added.IsArchived.Should().BeFalse();
        added.CreatedAt.Should().Be(NowUtc);
        added.LastUpdated.Should().Be(NowUtc);
        response.Id.Should().Be(added.Id);
        response.CanBeDeleted.Should().BeTrue();
        _repository.Verify(r => r.AddAsync(added, _cts.Token));
    }

    [Test]
    public async Task Create_NameTaken_ThrowsConflictWithoutAdding()
    {
        _repository.Setup(r => r.NameExistsAsync(SpecialtyName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<CreateSpecialtyHandler>().Handle(new CreateSpecialtyCommand(SpecialtyName), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecialtyRules.NameTakenMessage(SpecialtyName));
        _repository.Verify(r => r.AddAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Update

    [Test]
    public async Task Update_NewName_SavesItFromTheVersionReadAndMovesTheVersionOn()
    {
        GivenUsage(new SpecialtyUsage(2, 5, 1));

        var response = await _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, OtherSpecialtyName, 3), _cts.Token);

        _repository.Verify(r => r.UpdateAsync(
            It.Is<Specialty>(s => s.Name == OtherSpecialtyName && s.Version == 4 && s.LastUpdated == NowUtc), 3, _cts.Token));
        response.Name.Should().Be(OtherSpecialtyName);
        response.Version.Should().Be(4);
        response.TemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Create_WithoutTargets_TakesTheDefaults_OrThoseGiven()
    {
        var added = new List<Specialty>();
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()))
            .Callback((Specialty s, CancellationToken _) => added.Add(s));
        var handler = _fixture.Create<CreateSpecialtyHandler>();

        var defaults = await handler.Handle(new CreateSpecialtyCommand(OtherSpecialtyName), _cts.Token);
        var chosen = await handler.Handle(new CreateSpecialtyCommand("Synthetic biomechanics", 7, 21, 84, 180), _cts.Token);

        defaults.Should().BeEquivalentTo(new { TargetDaysUrgent = 14, TargetDaysHigh = 28, TargetDaysMedium = 126, TargetDaysLow = 126 });
        chosen.Should().BeEquivalentTo(new { TargetDaysUrgent = 7, TargetDaysHigh = 21, TargetDaysMedium = 84, TargetDaysLow = 180 });
        added[1].TargetDaysLow.Should().Be(180);
    }

    [Test]
    public async Task Update_TargetsOnly_SavesThemAndLeavesTheOthersAsTheyWere()
    {
        var response = await _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, SpecialtyName, 3, TargetDaysUrgent: 10, TargetDaysLow: 200), _cts.Token);

        _repository.Verify(r => r.UpdateAsync(
            It.Is<Specialty>(s => s.TargetDaysUrgent == 10 && s.TargetDaysHigh == 28 && s.TargetDaysMedium == 126
                                  && s.TargetDaysLow == 200 && s.Version == 4), 3, _cts.Token));
        _repository.Verify(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        response.TargetDaysUrgent.Should().Be(10);
    }

    [Test]
    public async Task Update_SameTargets_SavesNothing()
    {
        await _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, SpecialtyName, 3, 14, 28, 126, 126), _cts.Token);

        _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialty>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_SameName_SavesNothing()
    {
        var response = await _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, SpecialtyName, 3), _cts.Token);

        response.Version.Should().Be(3);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialty>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_FromAnOlderVersion_ThrowsConflictWithoutSaving()
    {
        var act = () => _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, OtherSpecialtyName, 2), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecialtyRules.ChangedMeanwhileMessage);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialty>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_NameTaken_ThrowsConflict()
    {
        _repository.Setup(r => r.NameExistsAsync(OtherSpecialtyName, _specialty.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(_specialty.Id, OtherSpecialtyName, 3), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecialtyRules.NameTakenMessage(OtherSpecialtyName));
    }

    [Test]
    public async Task Update_Unknown_ThrowsNotFound()
    {
        var act = () => _fixture.Create<UpdateSpecialtyHandler>()
            .Handle(new UpdateSpecialtyCommand(Guid.NewGuid(), OtherSpecialtyName, 3), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Archive and restore

    [TestCase(true)]
    [TestCase(false)]
    public async Task SetArchived_Changed_SavesFromTheVersionRead(bool archive)
    {
        _specialty.IsArchived = !archive;

        var response = await _fixture.Create<SetSpecialtyArchivedHandler>()
            .Handle(new SetSpecialtyArchivedCommand(_specialty.Id, archive, 3), _cts.Token);

        response.IsArchived.Should().Be(archive);
        response.Version.Should().Be(4);
        _repository.Verify(r => r.UpdateAsync(It.Is<Specialty>(s => s.IsArchived == archive && s.LastUpdated == NowUtc), 3, _cts.Token));
    }

    [Test]
    public async Task SetArchived_AlreadySo_SavesNothing()
    {
        var response = await _fixture.Create<SetSpecialtyArchivedHandler>()
            .Handle(new SetSpecialtyArchivedCommand(_specialty.Id, false, 3), _cts.Token);

        response.Version.Should().Be(3);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Specialty>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SetArchived_FromAnOlderVersion_ThrowsConflict()
    {
        var act = () => _fixture.Create<SetSpecialtyArchivedHandler>()
            .Handle(new SetSpecialtyArchivedCommand(_specialty.Id, true, 1), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecialtyRules.ChangedMeanwhileMessage);
    }

    // Delete

    [Test]
    public async Task Delete_NothingHasIt_DeletesIt()
    {
        await _fixture.Create<DeleteSpecialtyHandler>().Handle(new DeleteSpecialtyCommand(_specialty.Id), _cts.Token);

        _repository.Verify(r => r.DeleteAsync(_specialty, _cts.Token));
    }

    [TestCase(1, 0, 0, "1 pathway template")]
    [TestCase(0, 3, 0, "3 pathways")]
    [TestCase(0, 0, 1, "1 member of staff")]
    [TestCase(2, 0, 4, "2 pathway templates and 4 members of staff")]
    [TestCase(1, 1, 2, "1 pathway template, 1 pathway and 2 members of staff")]
    public async Task Delete_InUse_ThrowsConflictSayingWhatHasIt(int templates, int pathways, int members, string what)
    {
        GivenUsage(new SpecialtyUsage(templates, pathways, members));

        var act = () => _fixture.Create<DeleteSpecialtyHandler>().Handle(new DeleteSpecialtyCommand(_specialty.Id), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"The '{SpecialtyName}' specialty has {what}, so it can't be deleted. Archive it instead.");
        _repository.Verify(r => r.DeleteAsync(It.IsAny<Specialty>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Delete_Unknown_ThrowsNotFound()
    {
        var act = () => _fixture.Create<DeleteSpecialtyHandler>().Handle(new DeleteSpecialtyCommand(Guid.NewGuid()), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
