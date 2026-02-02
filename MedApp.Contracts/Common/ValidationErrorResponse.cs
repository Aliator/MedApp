namespace MedApp.Contracts.Common;

public sealed class ValidationErrorResponse
{
    public string? Title { get; set; }

    public Dictionary<string, string[]> Errors { get; set; }
        = new Dictionary<string, string[]>();
}