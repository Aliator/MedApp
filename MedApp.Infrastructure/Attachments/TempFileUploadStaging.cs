using MedApp.Application.Attachments.Common;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Holds each upload in a temporary file while it's checked: in the system's temp folder (in Kubernetes, the pod's
/// <c>/tmp</c>, a disk-backed emptyDir that outlives a container restart, as the root filesystem is read-only),
/// readable only by the API's user.
/// <list type="bullet">
/// <item>On Linux (the image) its name is deleted as soon as it's opened: the open handle keeps the bytes readable and
/// writable until the stream is disposed, and if the process dies first the kernel frees them, so nothing is left.</item>
/// <item>On Windows (development) an open file can't be deleted, so it's deleted when the stream is disposed
/// (<see cref="FileOptions.DeleteOnClose"/>); a process killed first leaves it, for <see cref="StaleUploadSweeper"/>.</item>
/// </list>
/// </summary>
public sealed class TempFileUploadStaging : IUploadStaging
{
    /// <summary>The start of every staged file's name (<see cref="StaleUploadSweeper"/> looks for them).</summary>
    public const string Prefix = "medapp-upload-";

    public Stream Create()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Prefix}{Guid.NewGuid():N}.tmp");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            BufferSize = 81920,
        };

        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        var stream = new FileStream(path, options);
        if (!OperatingSystem.IsWindows())
            File.Delete(path);
        return stream;
    }
}
