using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Shared.Components.Summaries;

public partial class PatientSummary
{
    [Parameter] public string? FirstName { get; set; }
    [Parameter] public string? LastName { get; set; }
    [Parameter] public DateOnly? DateOfBirth { get; set; }
    [Parameter] public string? Email { get; set; }
    [Parameter] public bool ShowDateOfBirth { get; set; } = true;
    [Parameter] public bool ShowEmail { get; set; } = true;
    [Parameter] public bool IsDanger { get; set; }
    [Parameter] public string DateFormat { get; set; } = "MMMM dd, yyyy";
    [Parameter] public RenderFragment? AdditionalItems { get; set; }

    private string GetFullName()
    {
        var parts = new[] { FirstName, LastName }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts);
    }
}