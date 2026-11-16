namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Lets at most <see cref="MaxConcurrent"/> uploads at a time, in each copy of the API, do the work whose memory grows
/// with a hostile file: working out its type (which reads a Word document's table of contents) and making a thumbnail
/// (which decodes an image). Each is bounded on its own (<see cref="FileTypeSniffer.MaxZipEntries"/>, the thumbnail
/// generator's source cap); this keeps a burst of uploads from adding them up.
/// </summary>
public static class FileWorkLimiter
{
    public const int MaxConcurrent = 2;

    private static readonly SemaphoreSlim Gate = new(MaxConcurrent, MaxConcurrent);

    /// <summary>Runs <paramref name="work"/> once one of the places is free.</summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await work();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Runs <paramref name="work"/>, which is synchronous, once one of the places is free.</summary>
    public static Task<T> RunAsync<T>(Func<T> work, CancellationToken ct) =>
        RunAsync(() => Task.FromResult(work()), ct);

    /// <summary>How many places are free now (for tests).</summary>
    public static int Available => Gate.CurrentCount;
}
