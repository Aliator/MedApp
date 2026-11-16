namespace MedApp.Application.Patients.Common;

/// <summary>
/// A patient who might be past their retention period, with what <see cref="RetentionSchedule"/> needs to tell.
/// <paramref name="LastProcedureChangedUtc"/> is null for a patient with no procedures.
/// </summary>
public sealed record RetentionCandidate(
    Guid PatientId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    DateTime LastUpdatedUtc,
    DateTime? LastProcedureChangedUtc,
    DateOnly? KeptUntil,
    string? KeptReason);
