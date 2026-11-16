using MediatR;

namespace MedApp.Application.Patients.Commands.KeepPatientRecord;

/// <summary>
/// Keeps a record past its retention period (admins only), with a reason, until <paramref name="KeepUntil"/>, when it
/// comes back for review.
/// </summary>
public sealed record KeepPatientRecordCommand(
    Guid PatientId,
    DateOnly KeepUntil,
    string Reason
) : IRequest;
