using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Consent.Common;

/// <summary>
/// Clinical photos and the consent they need (MC27 of <c>docs/multi-clinic-plan.md</c>; the light demo's
/// <c>consent.ts</c>): a picture on a pathway may be marked as a clinical photo only while the pathway has a Valid
/// consent record on the form named <see cref="FormName"/> (any version); a stage that takes photographs and needs
/// consent waits for the same. MedApp records the clinician's statement; it never assesses the consent, and never
/// analyses a photo.
/// </summary>
public static class ClinicalPhotography
{
    /// <summary>The consent form whose Valid record on a pathway is its photo consent.</summary>
    public const string FormName = "Clinical photography";

    public const string NotAPictureMessage =
        "Only a picture (PNG, JPEG, HEIC or HEIF) attached to a pathway or one of its stages can be marked as a clinical photo.";

    public const string NoConsentMessage =
        "Record the patient's consent to clinical photography on the " + FormName + " form, from the pathway's Consent card, " +
        "before marking a picture as a clinical photo.";

    public const string StageConsentMissingMessage =
        "This stage needs the patient's consent to clinical photography before it can be finished: record it on the " +
        FormName + " form from the pathway's Consent card.";

    public const string StageConsentWithdrawnSuffix = " The photo consent recorded has been withdrawn.";

    /// <summary>Whether a file is a picture (a PNG, JPEG, HEIC or HEIF image), so it can be a clinical photo.</summary>
    public static bool IsPicture(Attachment attachment) =>
        AttachmentFileTypes.FromContentType(attachment.ContentType) is { } type
        && (type == AttachmentFileTypes.Png || type == AttachmentFileTypes.Jpeg
            || type == AttachmentFileTypes.Heic || type == AttachmentFileTypes.Heif);

    /// <summary>Whether a file could be marked as a clinical photo: a live picture on a pathway (or one of its stages).</summary>
    public static bool CanBeClinicalPhoto(Attachment attachment) =>
        !attachment.IsDeleted && attachment.PathwayId is not null && IsPicture(attachment);

    /// <summary>A record on the photo consent form (any version), by name, ignoring case and spaces around it.</summary>
    public static bool IsPhotoConsent(ConsentRecord record) =>
        string.Equals(record.FormName.Trim(), FormName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether these records (a pathway's) hold a Valid photo consent.</summary>
    public static bool HasValidConsent(IEnumerable<ConsentRecord> records) =>
        records.Any(r => IsPhotoConsent(r) && r.Status == ConsentStatus.Valid);

    /// <summary>A stage that takes clinical photographs (the Dermatology templates' Take photographs), by its name.</summary>
    public static bool IsPhotoStage(PatientPathwayStage stage) =>
        stage.Name.Contains("photograph", StringComparison.OrdinalIgnoreCase);
}
