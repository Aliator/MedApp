namespace MedApp.Application.Appointments.Common;

/// <summary>The lengths of an appointment's free text, as the database stores them.</summary>
public static class AppointmentLimits
{
    public const int MaxNotesLength = 2000;
    public const int MaxCancelReasonLength = 500;
}
