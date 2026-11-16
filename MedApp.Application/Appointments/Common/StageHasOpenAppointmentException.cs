namespace MedApp.Application.Appointments.Common;

/// <summary>
/// Adding an appointment would have given its stage a second open (Requested or Booked) one: the database's unique
/// index refused it, because another request added one at the same moment, after this one checked. The handler turns
/// it into the same 409 as its own check, pointing to the one there is.
/// </summary>
public sealed class StageHasOpenAppointmentException(Guid stageId)
    : Exception($"Stage '{stageId}' already has an open appointment.")
{
    public Guid StageId { get; } = stageId;
}
