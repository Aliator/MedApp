namespace MedApp.Domain.PatientMessages;

/// <summary>How a message to a patient goes. Stored by number, so never renumber these.</summary>
public enum PatientMessageChannel
{
    Text = 0,
    Email = 1
}
