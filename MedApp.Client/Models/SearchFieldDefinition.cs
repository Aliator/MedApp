namespace MedApp.Client.Models;

public class SearchFieldDefinition
{
    public string Label { get; init; } = string.Empty;
    public string Placeholder { get; init; } = string.Empty;
    public SearchFieldType Type { get; init; } = SearchFieldType.Text;
    public string Value { get; set; } = string.Empty;
    public PartialDate PartialDateValue { get; set; } = PartialDate.Empty;
}