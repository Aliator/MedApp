namespace MedApp.Application.Clinics.Common;

/// <summary>The limits on clinic sessions and patterns (centre plan decisions 123 to 127).</summary>
public static class ClinicLimits
{
    /// <summary>The shortest slot, in minutes.</summary>
    public const int MinSlotMinutes = 5;

    /// <summary>The longest slot, in minutes.</summary>
    public const int MaxSlotMinutes = 120;

    /// <summary>A cancellation's reason, at most this long (as an appointment's).</summary>
    public const int MaxCancelReasonLength = 500;

    /// <summary>A pattern repeats every 1 to this many weeks.</summary>
    public const int MaxEveryWeeks = 4;

    /// <summary>Create sessions makes them for 1 to this many weeks ahead.</summary>
    public const int MaxWeeksAhead = 13;

    /// <summary>The longest range of dates one list of sessions covers.</summary>
    public const int MaxListDays = 62;
}
