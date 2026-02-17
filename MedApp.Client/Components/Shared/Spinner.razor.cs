using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class Spinner : ComponentBase
{
    [Parameter] public SpinnerSize Size { get; set; } = SpinnerSize.Default;
    [Parameter] public SpinnerColor Color { get; set; } = SpinnerColor.Primary;
    [Parameter] public int? CustomSize { get; set; }

    private string SizeClass => Size switch
    {
        SpinnerSize.Small => "spinner-small",
        SpinnerSize.Large => "spinner-large",
        _ => ""
    };

    private string ColorClass => Color switch
    {
        SpinnerColor.White => "spinner-white",
        _ => ""
    };

    private string? CustomStyle => CustomSize.HasValue 
        ? $"width: {CustomSize}px; height: {CustomSize}px;" 
        : null;
}

public enum SpinnerSize
{
    Small,
    Default,
    Large
}

public enum SpinnerColor
{
    Primary,
    White
}