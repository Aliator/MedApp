using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// At start-up, deletes staged uploads a process left behind (<see cref="TempFileUploadStaging"/>) in the temp folder:
/// whole copies of patients' files that would otherwise sit there outside retention and anonymisation. On Linux a
/// staged file's name is deleted the moment it's opened, so any found are left over; on Windows only those over an
/// hour old are deleted, since another process on the same machine may be using one (and a file in use can't be).
/// </summary>
public sealed class StaleUploadSweeper(ILogger<StaleUploadSweeper> logger) : IHostedService
{
    /// <summary>How old a staged file must be before it's swept on Windows.</summary>
    public static readonly TimeSpan WindowsMinimumAge = TimeSpan.FromHours(1);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var swept = Sweep(Path.GetTempPath(), DateTime.UtcNow, OperatingSystem.IsWindows() ? WindowsMinimumAge : TimeSpan.Zero);
        if (swept > 0)
            logger.LogWarning("Deleted {Count} staged upload(s) left in the temp folder by an earlier process.", swept);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Deletes the staged uploads in <paramref name="folder"/> at least <paramref name="minimumAge"/> old; how many.</summary>
    public static int Sweep(string folder, DateTime nowUtc, TimeSpan minimumAge)
    {
        var swept = 0;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, $"{TempFileUploadStaging.Prefix}*.tmp");
        }
        catch (IOException)
        {
            return 0;
        }

        foreach (var file in files)
        {
            try
            {
                if (nowUtc - File.GetLastWriteTimeUtc(file) < minimumAge)
                    continue;
                File.Delete(file);
                swept++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // In use (Windows) or not ours: left alone.
            }
        }

        return swept;
    }
}
