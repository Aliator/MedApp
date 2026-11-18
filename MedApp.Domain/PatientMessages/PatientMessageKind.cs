namespace MedApp.Domain.PatientMessages;

/// <summary>What a message to a patient is about. Stored by number, so never renumber these.</summary>
public enum PatientMessageKind
{
    /// <summary>A reminder of a Booked appointment, sent by the <c>patient-reminders</c> job before it.</summary>
    AppointmentReminder = 0,

    /// <summary>A Booked appointment was moved, and staff chose to tell the patient.</summary>
    AppointmentMoved = 1,

    /// <summary>A Booked appointment (or its clinic session) was cancelled, and staff chose to tell the patient.</summary>
    AppointmentCancelled = 2,

    /// <summary>The patient was marked Did not attend: asked to call, the next working day (when switched on).</summary>
    MissedAppointment = 3
}
