using MedApp.Domain.Appointments;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// An appointment history entry on the timeline. <paramref name="ProcedureExists"/> says whether the procedure it was
/// for still exists (false for one not for a procedure), and <paramref name="CurrentProcedureTitle"/> is its title now.
/// </summary>
public sealed record AppointmentTimelineRow(AppointmentEvent Event, bool ProcedureExists, string? CurrentProcedureTitle)
{
    /// <summary>Whether staff may see it: it's not for a procedure, or its procedure still exists.</summary>
    public bool IsVisibleToStaff => Event.ProcedureId is null || ProcedureExists;
}
