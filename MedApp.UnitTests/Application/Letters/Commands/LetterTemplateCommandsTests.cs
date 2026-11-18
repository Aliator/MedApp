using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Letters.Commands.SetLetterTemplateArchived;
using MedApp.Application.Letters.Commands.UpdateLetterTemplate;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Letters;
using MedApp.Domain.Specialties;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Letters.Commands;

[TestFixture]
public sealed class LetterTemplateCommandsTests
{
    private readonly FixedTimeProvider _clock = new(MedApp.UnitTests.Common.Constants.LettersTestConstants.NowUtc);
    private static readonly DateTime Now = MedApp.UnitTests.Common.Constants.LettersTestConstants.NowUtc;
    private CancellationTokenSource _cts = null!;
    private Mock<ILetterTemplateRepository> _repository = null!;
    private Mock<ISpecialtyRepository> _specialties = null!;
    private Specialty _specialty = null!;
    private Specialty _archived = null!;

    [SetUp]
    public void SetUp()
    {
        _cts = new CancellationTokenSource();
        _repository = new Mock<ILetterTemplateRepository>();
        _specialty = CreateSpecialty();
        _archived = CreateSpecialty(OtherSpecialtyName, isArchived: true);
        _specialties = new Mock<ISpecialtyRepository>();
        _specialties.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                new[] { _specialty, _archived }.Where(s => ids.Contains(s.Id)).ToList());
        _specialties.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new[] { _specialty, _archived }.FirstOrDefault(s => s.Id == id));
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private CreateLetterTemplateHandler CreateHandler() => new(_repository.Object, _specialties.Object, _clock);
    private UpdateLetterTemplateHandler UpdateHandler() => new(_repository.Object, _specialties.Object, _clock);
    private SetLetterTemplateArchivedHandler ArchiveHandler() => new(_repository.Object, _specialties.Object, _clock);

    private LetterTemplate Stored(LetterTemplate template)
    {
        _repository.Setup(r => r.GetAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        return template;
    }

    [Test]
    public async Task Create_Valid_AddsItTidiedAtVersionOne()
    {
        LetterTemplate? added = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<LetterTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((LetterTemplate t, CancellationToken _) => added = t);

        var response = await CreateHandler().Handle(
            new CreateLetterTemplateCommand($" {TemplateName} ", "dischargeSummary", _specialty.Id, "Centre\r\n", BodySkeleton + "\n\n", Closing),
            _cts.Token);

        added.Should().NotBeNull();
        added!.Name.Should().Be(TemplateName);
        added.Kind.Should().Be(LetterKind.DischargeSummary);
        added.SpecialtyId.Should().Be(_specialty.Id);
        added.Letterhead.Should().Be("Centre");
        added.BodySkeleton.Should().Be(BodySkeleton);
        added.Version.Should().Be(1);
        added.CreatedAt.Should().Be(Now);
        added.LastUpdated.Should().Be(Now);
        response.SpecialtyName.Should().Be(SpecialtyName);
        _repository.Verify(r => r.NameExistsAsync(TemplateName, _specialty.Id, null, _cts.Token));
        _repository.Verify(r => r.AddAsync(added, _cts.Token));
    }

    [Test]
    public async Task Create_ForEverySpecialty_LooksUpNoSpecialty()
    {
        var response = await CreateHandler().Handle(
            new CreateLetterTemplateCommand(TemplateName, "Other", null, "", "", ""), _cts.Token);

        response.SpecialtyId.Should().BeNull();
        response.SpecialtyName.Should().BeNull();
        _specialties.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Create_NameTaken_IsAConflict()
    {
        _repository.Setup(r => r.NameExistsAsync(TemplateName, _specialty.Id, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => CreateHandler().Handle(
            new CreateLetterTemplateCommand(TemplateName, "Other", _specialty.Id, "", "", ""), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(LetterTemplateRules.NameTakenMessage(TemplateName, SpecialtyName));
        _repository.Verify(r => r.AddAsync(It.IsAny<LetterTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Create_ArchivedSpecialty_IsAConflict()
    {
        var act = () => CreateHandler().Handle(
            new CreateLetterTemplateCommand(TemplateName, "Other", _archived.Id, "", "", ""), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Create_UnknownSpecialty_IsNotFound()
    {
        var act = () => CreateHandler().Handle(
            new CreateLetterTemplateCommand(TemplateName, "Other", Guid.NewGuid(), "", "", ""), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Update_Changed_SavesTheNextVersionFromTheOneRead()
    {
        var template = Stored(CreateTemplate(specialtyId: _specialty.Id, version: 3));

        var response = await UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(template.Id, OtherTemplateName, "AppointmentLetter", null, "New head", "New body", "Bye", 3),
            _cts.Token);

        template.Name.Should().Be(OtherTemplateName);
        template.Kind.Should().Be(LetterKind.AppointmentLetter);
        template.SpecialtyId.Should().BeNull();
        template.Letterhead.Should().Be("New head");
        template.BodySkeleton.Should().Be("New body");
        template.Closing.Should().Be("Bye");
        template.Version.Should().Be(4);
        template.LastUpdated.Should().Be(Now);
        response.Version.Should().Be(4);
        _repository.Verify(r => r.NameExistsAsync(OtherTemplateName, null, template.Id, _cts.Token));
        _repository.Verify(r => r.UpdateAsync(template, 3, _cts.Token));
    }

    [Test]
    public async Task Update_NothingChanged_SavesNothing()
    {
        var template = Stored(CreateTemplate(specialtyId: _specialty.Id));

        var response = await UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(template.Id, TemplateName, "ClinicLetter", _specialty.Id, Letterhead, BodySkeleton, Closing, 3),
            _cts.Token);

        response.SpecialtyName.Should().Be(SpecialtyName);
        template.Version.Should().Be(3);
        _repository.Verify(r => r.UpdateAsync(It.IsAny<LetterTemplate>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Update_TextOnly_DoesNotCheckTheName()
    {
        var template = Stored(CreateTemplate());

        await UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(template.Id, TemplateName.ToUpperInvariant(), "ClinicLetter", null, "Other head", BodySkeleton, Closing, 3),
            _cts.Token);

        _repository.Verify(r => r.NameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.UpdateAsync(template, 3, _cts.Token));
    }

    [Test]
    public async Task Update_StaleVersion_IsAConflict()
    {
        var template = Stored(CreateTemplate(version: 5));

        var act = () => UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(template.Id, TemplateName, "Other", null, "", "", "", 4), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LetterTemplateRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task Update_NameTakenInTheNewSpecialty_IsAConflict()
    {
        var template = Stored(CreateTemplate());
        _repository.Setup(r => r.NameExistsAsync(TemplateName, _specialty.Id, template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(template.Id, TemplateName, "ClinicLetter", _specialty.Id, Letterhead, BodySkeleton, Closing, 3),
            _cts.Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage(LetterTemplateRules.NameTakenMessage(TemplateName, SpecialtyName));
    }

    [Test]
    public async Task Update_KeepingAnArchivedSpecialty_IsAllowed_ButChoosingOneIsNot()
    {
        var keeps = Stored(CreateTemplate(specialtyId: _archived.Id));
        var response = await UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(keeps.Id, TemplateName, "ClinicLetter", _archived.Id, "Changed", BodySkeleton, Closing, 3),
            _cts.Token);
        response.SpecialtyName.Should().Be(OtherSpecialtyName);

        var moves = Stored(CreateTemplate());
        var act = () => UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(moves.Id, TemplateName, "ClinicLetter", _archived.Id, Letterhead, BodySkeleton, Closing, 3),
            _cts.Token);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Update_Unknown_IsNotFound()
    {
        var act = () => UpdateHandler().Handle(
            new UpdateLetterTemplateCommand(Guid.NewGuid(), TemplateName, "Other", null, "", "", "", 1), _cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SetArchived_Changed_SavesTheNextVersion(bool archive)
    {
        var template = Stored(CreateTemplate(specialtyId: _specialty.Id, isArchived: !archive));

        var response = await ArchiveHandler().Handle(new SetLetterTemplateArchivedCommand(template.Id, archive, 3), _cts.Token);

        template.IsArchived.Should().Be(archive);
        template.Version.Should().Be(4);
        template.LastUpdated.Should().Be(Now);
        response.SpecialtyName.Should().Be(SpecialtyName);
        _repository.Verify(r => r.UpdateAsync(template, 3, _cts.Token));
    }

    [Test]
    public async Task SetArchived_AlreadySo_SavesNothing()
    {
        var template = Stored(CreateTemplate(isArchived: true));

        var response = await ArchiveHandler().Handle(new SetLetterTemplateArchivedCommand(template.Id, true, 3), _cts.Token);

        response.IsArchived.Should().BeTrue();
        response.SpecialtyName.Should().BeNull();
        _repository.Verify(r => r.UpdateAsync(It.IsAny<LetterTemplate>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task SetArchived_StaleVersion_IsAConflict()
    {
        var template = Stored(CreateTemplate());

        var act = () => ArchiveHandler().Handle(new SetLetterTemplateArchivedCommand(template.Id, true, 2), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
