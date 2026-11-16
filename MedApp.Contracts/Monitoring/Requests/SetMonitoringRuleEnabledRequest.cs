namespace MedApp.Contracts.Monitoring.Requests;

/// <summary>Switches a rule on or off, and nothing else.</summary>
public sealed class SetMonitoringRuleEnabledRequest
{
    public bool IsEnabled { get; set; }
}
