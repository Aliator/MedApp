namespace MedApp.Application.Attachments.Common;

/// <summary>The file is over <see cref="AttachmentLimits.MaxFileBytes"/> (413).</summary>
public sealed class FileTooLargeException()
    : Exception($"The file is larger than {AttachmentLimits.MaxFileSizeText}, the most that can be attached.");

/// <summary>The file's contents aren't one of the types that can be attached, or don't match its name (415).</summary>
public sealed class UnsupportedFileTypeException(string message) : Exception(message);

/// <summary>The malware scan found something in the file, so it wasn't stored (422).</summary>
public sealed class MalwareDetectedException()
    : Exception("The file failed its malware scan, so it wasn't attached.");

/// <summary>No file storage is set up for this deployment, so files can't be uploaded or downloaded (503).</summary>
public sealed class FileStorageUnavailableException()
    : Exception("File storage isn't set up, so files can't be attached or downloaded.");
