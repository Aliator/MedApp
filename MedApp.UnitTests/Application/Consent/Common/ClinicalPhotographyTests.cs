using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Consent.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consent.Common;

/// <summary>Clinical photos and their consent (MC27): the pictures that can be one, and the photographs stage's gate.</summary>
[TestFixture]
public sealed class ClinicalPhotographyTests
{
    private static ConsentRecord Record(PatientPathway pathway, string formName, ConsentStatus status, int stageOrder = 1) => new()
    {
        Id = Guid.NewGuid(),
        PathwayId = pathway.Id,
        StageId = pathway.StageAt(stageOrder).Id,
        FormName = formName,
        Status = status,
    };

    [TestCase("image/png", true)]
    [TestCase("image/jpeg", true)]
    [TestCase("image/heic", true)]
    [TestCase("image/heif", true)]
    [TestCase("application/pdf", false)]
    [TestCase("text/plain; charset=utf-8", false)]
    public void CanBeClinicalPhoto_OnlyAPictureOnAPathway(string contentType, bool expected)
    {
        var onPathway = CreateAttachment(pathwayId: Guid.NewGuid(), contentType: contentType);

        ClinicalPhotography.CanBeClinicalPhoto(onPathway).Should().Be(expected);
        ClinicalPhotography.CanBeClinicalPhoto(CreateAttachment(contentType: contentType)).Should().BeFalse("a patient's own file isn't on a pathway");
        ClinicalPhotography.CanBeClinicalPhoto(CreateAttachment(pathwayId: Guid.NewGuid(), deleted: true, contentType: contentType))
            .Should().BeFalse();
    }

    [Test]
    public void HasValidConsent_AValidRecordOnThePhotoForm_AnyVersionAndCase()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);

        ClinicalPhotography.HasValidConsent([Record(pathway, " CLINICAL photography ", ConsentStatus.Valid)]).Should().BeTrue();
        ClinicalPhotography.HasValidConsent([Record(pathway, "Clinical photography", ConsentStatus.Withdrawn)]).Should().BeFalse();
        ClinicalPhotography.HasValidConsent([Record(pathway, "Consent for a skin procedure", ConsentStatus.Valid)]).Should().BeFalse();
        ClinicalPhotography.HasValidConsent([]).Should().BeFalse();
    }

    [Test]
    public void WhyConsentMissing_PhotographsStage_NeedsThePhotoConsentAnywhereOnThePathway()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var stage = pathway.StageAt(2);
        stage.Name = "Take photographs";
        stage.NeedsConsent = true;

        StageGates.WhyConsentMissing(stage, []).Should().Be(ClinicalPhotography.StageConsentMissingMessage);
        StageGates.WhyConsentMissing(stage, [Record(pathway, "Consent for a skin procedure", ConsentStatus.Valid, stageOrder: 2)])
            .Should().Be(ClinicalPhotography.StageConsentMissingMessage, "a consent on another form isn't photo consent");
        StageGates.WhyConsentMissing(stage, [Record(pathway, "Clinical photography", ConsentStatus.Withdrawn)])
            .Should().EndWith(ClinicalPhotography.StageConsentWithdrawnSuffix);
        StageGates.WhyConsentMissing(stage, [Record(pathway, "Clinical photography", ConsentStatus.Valid, stageOrder: 1)])
            .Should().BeNull("photo consent recorded on another stage of the pathway counts");
    }

    [Test]
    public void WhyConsentMissing_AnyOtherStage_StillNeedsItsOwnRecord()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var stage = pathway.StageAt(2);
        stage.NeedsConsent = true;

        StageGates.WhyConsentMissing(stage, [Record(pathway, "Clinical photography", ConsentStatus.Valid, stageOrder: 1)])
            .Should().Be(StageGates.ConsentMissingMessage);
        StageGates.WhyConsentMissing(stage, [Record(pathway, "Anything", ConsentStatus.Valid, stageOrder: 2)]).Should().BeNull();
    }

    [Test]
    public void IsPhotoStage_ByItsName()
    {
        var stage = CreatePathway().StageAt(1);
        ClinicalPhotography.IsPhotoStage(stage).Should().BeFalse();
        stage.Name = "Clinical photographs";
        ClinicalPhotography.IsPhotoStage(stage).Should().BeTrue();
    }

    [Test]
    public void Mapper_OffersMarkingOnlyToThoseWhoAttachFiles()
    {
        var picture = CreateAttachment(pathwayId: Guid.NewGuid(), contentType: "image/png");
        picture.IsClinicalPhoto = true;
        var row = new AttachmentRow(picture, PathwayTitle, null);

        var response = AttachmentMapper.ToResponse(row, Staff.UserId, canCorrectRecords: false, canAttachFiles: true);
        response.IsClinicalPhoto.Should().BeTrue();
        response.CanMarkClinicalPhoto.Should().BeTrue();
        AttachmentMapper.ToResponse(row, Staff.UserId, canCorrectRecords: false, canAttachFiles: false)
            .CanMarkClinicalPhoto.Should().BeFalse();
    }
}
