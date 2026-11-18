namespace MedApp.Domain.Specimens;

/// <summary>Why the lab rejected a specimen: a fixed choice, with a detail. Stored as a number, so values never change.</summary>
public enum SpecimenRejectionReason
{
    Unlabelled = 0,
    Mislabelled = 1,
    Leaked = 2,
    InsufficientSample = 3,
    WrongContainer = 4,
    Other = 5
}
