namespace MedApp.E2ETests.Common;

/// <summary>
/// What a test made, to delete when it ends, pass or fail: the clean-ups run newest first (a procedure before its
/// patient and template, a template before its stages). Every one runs even if another fails; the failures are
/// reported together.
/// </summary>
public sealed class E2EData
{
    private readonly Stack<(string What, Func<Task> CleanUp)> _cleanUps = new();

    /// <summary>A synthetic name no other test or run uses, e.g. "E2E Patient 3f9c1a2b".</summary>
    public static string UniqueName(string kind) => $"E2E {kind} {Guid.NewGuid().ToString("N")[..8]}";

    public void OnCleanUp(string what, Func<Task> cleanUp) => _cleanUps.Push((what, cleanUp));

    public async Task CleanUpAsync()
    {
        var failures = new List<string>();
        while (_cleanUps.TryPop(out var item))
        {
            try
            {
                await item.CleanUp();
            }
            catch (Exception exception)
            {
                failures.Add($"{item.What}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
            Assert.Fail("Couldn't clean up after the test: " + string.Join("; ", failures));
    }
}
