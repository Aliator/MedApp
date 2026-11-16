namespace MedApp.Application.Attachments.Common;

/// <summary>The file is over <see cref="AttachmentLimits.MaxFileBytes"/> (413).</summary>
public sealed class FileTooLargeException()
    : Exception($"The file is larger than {AttachmentLimits.MaxFileSizeText}, the most that can be attached.");

/// <summary>The file's contents aren't one of the types that can be attached, or don't match its name (415).</summary>
public sealed class UnsupportedFileTypeException(string message) : Exception(message);

/// <summary>The malware scan found something in the file, so it wasn't stored (422).</summary>
public sealed class MalwareDetectedException()
    : Exception("The file failed its malware scan, so it wasn't attached.");

/// <summary>File storage is set up but isn't answering, or failed (503). Uploads and downloads can be tried again later.</summary>
public sealed class StorageUnavailableException(Exception inner)
    : Exception("File storage isn't available just now, so the file couldn't be stored or read. Try again in a few minutes.", inner);

/// <summary>No file storage is set up for this deployment, so files can't be uploaded or downloaded (503).</summary>
public sealed class FileStorageUnavailableException()
    : Exception("File storage isn't set up, so files can't be attached or downloaded.");
