namespace MedApp.Contracts.Attachments.Requests;

/// <summary>Marks a picture on a pathway as a clinical photo, or unmarks it (MC27).</summary>
public sealed class SetClinicalPhotoRequest
{
    public bool IsClinicalPhoto { get; set; }
}
