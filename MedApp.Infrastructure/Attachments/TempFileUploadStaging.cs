using MedApp.Application.Attachments.Common;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Holds each upload in a temporary file while it's checked: in the system's temp folder (in Kubernetes, the pod's
/// <c>/tmp</c>, a disk-backed emptyDir, as the root filesystem is read-only), readable only by the API's user, and
/// deleted as soon as its stream is disposed, or by the system if the process dies first.
/// </summary>
public sealed class TempFileUploadStaging : IUploadStaging
{
    public Stream Create()
    {
        var path = Path.Combine(Path.GetTempPath(), $"medapp-upload-{Guid.NewGuid():N}.tmp");
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

        return new FileStream(path, options);
    }
}
