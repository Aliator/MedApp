using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Layout;

public partial class MainLayout
{
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool IsLoginPage =>
        Nav.ToBaseRelativePath(Nav.Uri).StartsWith("login");
}