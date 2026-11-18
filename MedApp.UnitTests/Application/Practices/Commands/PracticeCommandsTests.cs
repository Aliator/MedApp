using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Practices.Commands.DeletePractice;
using MedApp.Application.Practices.Commands.SetPracticeArchived;
using MedApp.Application.Practices.Commands.UpdatePractice;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Practices;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Practices.Commands;

[TestFixture]
public sealed class PracticeCommandsTests
{
    private readonly FixedTimeProvider _clock = new(NowUtc);
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp() => _cts = new CancellationTokenSource();

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static void InUse(Mock<IPracticeRepository> repository, Practice practice, PracticeUsage usage) =>
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PracticeUsage> { [practice.Id] = usage });

    [Test]
    public async Task Create_Valid_AddsItTidiedAtVersionOne()
    {
        var repository = RepositoryWith();
        Practice? added = null;
        repository.Setup(r => r.AddAsync(It.IsAny<Practice>(), It.IsAny<CancellationToken>()))
            .Callback((Practice p, CancellationToken _) => added = p);

        var response = await new CreatePracticeHandler(repository.Object, _clock).Handle(
            new CreatePracticeCommand($"  {PracticeName} ", " z999 01 ", [AddressLine1, " ", AddressLine2], PostcodeAsTyped,
                "  01632   960000 ", $" {SecureEmail} "),
            _cts.Token);

        added.Should().NotBeNull();
        added!.Name.Should().Be(PracticeName);
        added.OdsCode.Should().Be(OdsCode);
        added.AddressLines.Should().Be($"{AddressLine1}\n{AddressLine2}");
        added.Postcode.Should().Be(Postcode);
        added.Phone.Should().Be(Phone);
        added.SecureEmail.Should().Be(SecureEmail);
        added.Version.Should().Be(1);
        added.CreatedAt.Should().Be(NowUtc);
        response.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        response.CanBeDeleted.Should().BeTrue();
        repository.Verify(r => r.AddAsync(added, _cts.Token));
        repository.Verify(r => r.OdsCodeExistsAsync(OdsCode, null, _cts.Token));
    }

    [Test]
    public async Task Create_OdsCodeTaken_IsAConflict()
    {
        var repository = RepositoryWith();
        repository.Setup(r => r.OdsCodeExistsAsync(OdsCode, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => new CreatePracticeHandler(repository.Object, _clock).Handle(
            new CreatePracticeCommand(PracticeName, OdsCode, null, null, null, null), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.OdsCodeTakenMessage(OdsCode));
        repository.Verify(r => r.AddAsync(It.IsAny<Practice>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Create_NoOdsCode_DoesNotLookOneUp()
    {
        var repository = RepositoryWith();

        var response = await new CreatePracticeHandler(repository.Object, _clock).Handle(
            new CreatePracticeCommand(PracticeName, " ", null, null, null, ""), _cts.Token);

        response.OdsCode.Should().BeNull();
        response.SecureEmail.Should().BeNull();
        response.AddressLines.Should().BeEmpty();
        repository.Verify(r => r.OdsCodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_Changed_SavesFromTheVersionSeenAndMovesItOn()
    {
        var practice = CreatePractice(version: 3);
        var repository = RepositoryWith(practice);

        var response = await new UpdatePracticeHandler(repository.Object, _clock).Handle(
            new UpdatePracticeCommand(practice.Id, OtherPracticeName, OtherOdsCode, [AddressLine1], Postcode, Phone, null, 3),
            _cts.Token);

        practice.Name.Should().Be(OtherPracticeName);
        practice.OdsCode.Should().Be(OtherOdsCode);
        practice.AddressLines.Should().Be(AddressLine1);
        practice.Version.Should().Be(4);
        practice.LastUpdated.Should().Be(NowUtc);
        response.Version.Should().Be(4);
        repository.Verify(r => r.UpdateAsync(practice, 3, _cts.Token));
        repository.Verify(r => r.OdsCodeExistsAsync(OtherOdsCode, practice.Id, _cts.Token));
    }

    [Test]
    public async Task Update_NothingChanged_SavesNothing()
    {
        var practice = CreatePractice();
        var repository = RepositoryWith(practice);

        await new UpdatePracticeHandler(repository.Object, _clock).Handle(
            new UpdatePracticeCommand(practice.Id, PracticeName, OdsCode, [AddressLine1, AddressLine2], Postcode, Phone, null, 3),
            _cts.Token);

        practice.Version.Should().Be(3);
        repository.Verify(r => r.UpdateAsync(It.IsAny<Practice>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_SameOdsCode_DoesNotCheckItAgain()
    {
        var practice = CreatePractice();
        var repository = RepositoryWith(practice);

        await new UpdatePracticeHandler(repository.Object, _clock).Handle(
            new UpdatePracticeCommand(practice.Id, OtherPracticeName, OdsCode, null, null, null, null, 3), _cts.Token);

        repository.Verify(r => r.OdsCodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.UpdateAsync(practice, 3, _cts.Token));
    }

    [Test]
    public async Task Update_OdsCodeTaken_IsAConflict()
    {
        var practice = CreatePractice();
        var repository = RepositoryWith(practice);
        repository.Setup(r => r.OdsCodeExistsAsync(OtherOdsCode, practice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => new UpdatePracticeHandler(repository.Object, _clock).Handle(
            new UpdatePracticeCommand(practice.Id, PracticeName, OtherOdsCode, null, null, null, null, 3), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.OdsCodeTakenMessage(OtherOdsCode));
    }

    [Test]
    public async Task Update_StaleVersion_IsAConflict()
    {
        var practice = CreatePractice(version: 5);
        var repository = RepositoryWith(practice);

        var act = () => new UpdatePracticeHandler(repository.Object, _clock).Handle(
            new UpdatePracticeCommand(practice.Id, OtherPracticeName, null, null, null, null, null, 4), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Update_Unknown_IsNotFound()
    {
        var act = () => new UpdatePracticeHandler(RepositoryWith().Object, _clock).Handle(
            new UpdatePracticeCommand(Guid.NewGuid(), PracticeName, null, null, null, null, null, 1), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SetArchived_Changed_SavesIt(bool archive)
    {
        var practice = CreatePractice(isArchived: !archive);
        var repository = RepositoryWith(practice);

        var response = await new SetPracticeArchivedHandler(repository.Object, _clock)
            .Handle(new SetPracticeArchivedCommand(practice.Id, archive, 3), _cts.Token);

        response.IsArchived.Should().Be(archive);
        practice.Version.Should().Be(4);
        repository.Verify(r => r.UpdateAsync(practice, 3, _cts.Token));
    }

    [Test]
    public async Task SetArchived_AlreadySo_SavesNothing()
    {
        var practice = CreatePractice(isArchived: true);
        var repository = RepositoryWith(practice);

        await new SetPracticeArchivedHandler(repository.Object, _clock)
            .Handle(new SetPracticeArchivedCommand(practice.Id, true, 3), _cts.Token);

        repository.Verify(r => r.UpdateAsync(It.IsAny<Practice>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SetArchived_StaleVersion_IsAConflict()
    {
        var practice = CreatePractice();

        var act = () => new SetPracticeArchivedHandler(RepositoryWith(practice).Object, _clock)
            .Handle(new SetPracticeArchivedCommand(practice.Id, true, 2), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Delete_Unused_DeletesIt()
    {
        var practice = CreatePractice();
        var repository = RepositoryWith(practice);

        await new DeletePracticeHandler(repository.Object).Handle(new DeletePracticeCommand(practice.Id), _cts.Token);

        repository.Verify(r => r.DeleteAsync(practice, _cts.Token));
    }

    [TestCase(2, 0, "2 patients")]
    [TestCase(1, 0, "1 patient")]
    [TestCase(0, 1, "1 referral")]
    [TestCase(1, 3, "1 patient and 3 referrals")]
    public async Task Delete_InUse_IsAConflictSayingWhatHasIt(int patients, int referrals, string what)
    {
        var practice = CreatePractice();
        var repository = RepositoryWith(practice);
        InUse(repository, practice, new PracticeUsage(patients, referrals));

        var act = () => new DeletePracticeHandler(repository.Object).Handle(new DeletePracticeCommand(practice.Id), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{PracticeName} has {what}, so it can't be deleted. Archive it instead.");
        repository.Verify(r => r.DeleteAsync(It.IsAny<Practice>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
