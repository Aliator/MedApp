namespace MedApp.DevMcp.Tools;

public sealed partial class TestStaffTools
{
    /// <summary>The roles to give and take away so an account holds exactly <paramref name="wanted"/> (names ignore case).</summary>
    public static (IReadOnlyList<string> Add, IReadOnlyList<string> Remove) RoleChanges(IEnumerable<string> current, IEnumerable<string> wanted)
    {
        var have = current.ToList();
        var want = wanted.ToList();
        return (
            [.. want.Except(have, StringComparer.OrdinalIgnoreCase)],
            [.. have.Except(want, StringComparer.OrdinalIgnoreCase)]);
    }
}
