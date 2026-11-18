namespace MedApp.Domain.Patients;

/// <summary>
/// How a patient wants appointment reminders: none until they're asked. Stored by number, so never renumber it. Patient
/// reminders (Phase 4) send them; until then it's always <see cref="None"/>.
/// </summary>
public enum ReminderChannel
{
    None = 0,
    Text = 1,
    Email = 2,
    TextAndEmail = 3
}
