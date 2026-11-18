namespace MedApp.Contracts.Calendar.Requests;

public sealed class CreateClosedDayRequest
{
    /// <summary>A UK date, today or later; one closed day per date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>At most 100 characters, e.g. "Staff training day". About the clinic, never a patient.</summary>
    public string Name { get; set; } = string.Empty;
}
