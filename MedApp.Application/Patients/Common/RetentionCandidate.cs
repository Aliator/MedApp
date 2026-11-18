namespace MedApp.Application.Patients.Common;

/// <summary>
/// A patient who might be past their retention period, with what <see cref="RetentionSchedule"/> needs to tell.
/// <paramref name="LastPathwayChangedUtc"/> is null for a patient with no pathways, and
/// <paramref name="LastAuditUtc"/> (the latest pathway audit entry about them, which outlives a deleted pathway)
/// for one whose pathways were never audited. <paramref name="LastReferralChangeUtc"/> is the latest change to any
/// of their referrals (a patient with only a rejected referral has a conclusion of treatment from its rejection).
/// </summary>
public sealed record RetentionCandidate(
    Guid PatientId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    DateTime LastUpdatedUtc,
    DateTime? LastPathwayChangedUtc,
    DateOnly? KeptUntil,
    string? KeptReason,
    DateTime? LastAuditUtc = null,
    DateTime? LastReferralChangeUtc = null);
