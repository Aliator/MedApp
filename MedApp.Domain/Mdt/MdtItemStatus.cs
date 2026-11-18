namespace MedApp.Domain.Mdt;

/// <summary>Where an MDT item is. Stored as a number, so values never change.</summary>
public enum MdtItemStatus
{
    /// <summary>On a meeting's list, waiting to be discussed (or, its meeting cancelled, waiting to be listed again).</summary>
    Listed = 0,

    /// <summary>The team discussed it, and its outcome is recorded. Final.</summary>
    Discussed = 1,

    /// <summary>Moved to a later meeting, still to be discussed.</summary>
    Deferred = 2,

    /// <summary>Taken off the list without being discussed. Final.</summary>
    Withdrawn = 3
}
