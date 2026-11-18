namespace MedApp.Domain.Specialties;

/// <summary>
/// A member of staff belonging to a specialty: admins set them on the Users page, any number each. Triage will use it
/// (a referral is triaged by someone of its specialty); it changes nothing about what anyone can see. Goes with the
/// account.
/// </summary>
public sealed class StaffSpecialty
{
    public Guid UserId { get; init; }

    public Guid SpecialtyId { get; init; }
    public Specialty Specialty { get; init; } = null!;
}
