using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Shared;

public partial class LoadingState : ComponentBase
{
    [Parameter] public string Message { get; set; } = "Loading...";
}