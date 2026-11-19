using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Commands.SetClinicalPhoto;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Consent.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Commands.SetClinicalPhoto;

[TestFixture]
public sealed class SetClinicalPhotoHandlerTests : AttachmentHandlerTestBase
{
    private PatientPathway _pathway = null!;

    [SetUp]
    public void SetUp()
    {
        _pathway = GivenPathway(CreatePathway(PatientPathwayStatus.InProgress));
        Attachments.Setup(a => a.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private SetClinicalPhotoHandler Handler => Fixture.Create<SetClinicalPhotoHandler>();

    private static ConsentRecord PhotoConsent(PatientPathway pathway, ConsentStatus status = ConsentStatus.Valid) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = pathway.PatientId,
        PathwayId = pathway.Id,
        StageId = pathway.StageAt(1).Id,
        FormName = " clinical PHOTOGRAPHY ",
        FormVersion = "1.0",
        Status = status,
    };

    private void GivenConsents(params ConsentRecord[] records) =>
        Consents.Setup(c => c.GetForPathwayAsync(_pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(records);

    private MedApp.Domain.Attachments.Attachment Picture(string contentType = "image/jpeg", int? stageOrder = null) =>
        GivenAttachment(CreateAttachment(pathwayId: _pathway.Id, stageId: stageOrder is { } o ? _pathway.StageAt(o).Id : null,
            contentType: contentType));

    [Test]
    public async Task Handle_PictureWithValidPhotoConsent_MarksItAndSaves()
    {
        var photo = Picture();
        GivenConsents(PhotoConsent(_pathway));
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, Staff, false), cts.Token);

        photo.IsClinicalPhoto.Should().BeTrue();
        response.IsClinicalPhoto.Should().BeTrue();
        response.CanMarkClinicalPhoto.Should().BeTrue();
        Attachments.Verify(a => a.SaveAsync(cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoValidPhotoConsent_RefusesToMark()
    {
        var photo = Picture();
        GivenConsents(PhotoConsent(_pathway, ConsentStatus.Withdrawn));

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicalPhotography.NoConsentMessage);
        photo.IsClinicalPhoto.Should().BeFalse();
        Attachments.Verify(a => a.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Unmarking_NeedsNoConsent()
    {
        var photo = Picture(stageOrder: null);
        photo.IsClinicalPhoto = true;

        await Handler.Handle(new SetClinicalPhotoCommand(photo.Id, false, Staff, false), CancellationToken.None);

        photo.IsClinicalPhoto.Should().BeFalse();
        Attachments.Verify(a => a.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_AlreadyMarked_ChangesNothing()
    {
        var photo = Picture();
        photo.IsClinicalPhoto = true;

        var response = await Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, Staff, false), CancellationToken.None);

        response.IsClinicalPhoto.Should().BeTrue();
        Attachments.Verify(a => a.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_APdf_IsNoPhoto()
    {
        var pdf = Picture("application/pdf");

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(pdf.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicalPhotography.NotAPictureMessage);
    }

    [Test]
    public async Task Handle_APatientsOwnPicture_IsNoClinicalPhoto()
    {
        var picture = GivenAttachment(CreateAttachment(contentType: "image/png"));

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(picture.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(ClinicalPhotography.NotAPictureMessage);
    }

    [Test]
    public async Task Handle_DeletedOrMissing_IsNotFound()
    {
        var deleted = GivenAttachment(CreateAttachment(pathwayId: _pathway.Id, deleted: true, contentType: "image/png"));

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(deleted.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(Guid.NewGuid(), true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_RestrictedRecord_Refuses()
    {
        var photo = Picture();
        GivenConsents(PhotoConsent(_pathway));
        _pathway.Patient.RestrictedAtUtc = Now;

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task Handle_CancelledPathway_Refuses()
    {
        var photo = Picture(stageOrder: null);
        _pathway.Status = PatientPathwayStatus.Cancelled;
        GivenConsents(PhotoConsent(_pathway));

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, Staff, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>();
        photo.IsClinicalPhoto.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ThePathwayIsAnotherTeams_Refuses()
    {
        var walls = new TeamWallsTestBoundary();
        var otherTeam = Guid.NewGuid();
        _pathway.TeamId = otherTeam;
        walls.TeamNames[otherTeam] = "Gynaecology";
        // The viewer's team reads it (say, as the lab) but doesn't own it.
        var myTeam = Guid.NewGuid();
        walls.Grants.Add(PathwayAccess.StageOwner(_pathway.Id, _pathway.PatientId, myTeam, Now));
        Fixture.Inject(walls.Boundary);
        var photo = Picture();
        photo.TeamId = otherTeam;
        GivenConsents(PhotoConsent(_pathway));
        var viewer = Staff with { Scope = TeamWallsTestBoundary.StaffIn(Staff.UserId, myTeam) };

        await FluentActions.Awaiting(() => Handler.Handle(new SetClinicalPhotoCommand(photo.Id, true, viewer, false), CancellationToken.None))
            .Should().ThrowAsync<ConflictException>().WithMessage(TeamWalls.NotYourTeams("Gynaecology"));
        photo.IsClinicalPhoto.Should().BeFalse();
    }
}
