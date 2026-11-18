using MedApp.Domain.Appointments;

namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// An appointment history entry on the timeline. <paramref name="PathwayExists"/> says whether the pathway it was
/// for still exists (false for one not for a pathway), and <paramref name="CurrentPathwayTitle"/> is its title now.
/// </summary>
public sealed record AppointmentTimelineRow(AppointmentEvent Event, bool PathwayExists, string? CurrentPathwayTitle)
{
    /// <summary>Whether staff may see it: it's not for a pathway, or its pathway still exists.</summary>
    public bool IsVisibleToStaff => Event.PathwayId is null || PathwayExists;
}
