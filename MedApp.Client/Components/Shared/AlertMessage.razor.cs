using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class AlertMessage
{
    [Parameter] public string? Message { get; set; }
    [Parameter] public List<string> ValidationErrors { get; set; } = new();
    [Parameter] public AlertType Type { get; set; } = AlertType.Error;

    private string GetAlertClass() => Type switch
    {
        AlertType.Success => "alert-success",
        AlertType.Warning => "alert-warning",
        AlertType.Info => "alert-info",
        _ => "alert-error"
    };

    public enum AlertType
    {
        Error,
        Success,
        Warning,
        Info
    }
}