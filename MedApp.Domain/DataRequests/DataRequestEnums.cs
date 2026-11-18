namespace MedApp.Domain.DataRequests;

/// <summary>Where a data request stands. Stored by number: never renumber.</summary>
public enum DataRequestStatus
{
    /// <summary>Received and not yet answered.</summary>
    Open = 0,

    /// <summary>The response was sent (when and how are on the request).</summary>
    Sent = 1,

    /// <summary>Refused, with a reason (manifestly unfounded or excessive, for example).</summary>
    Refused = 2,

    /// <summary>The person withdrew it.</summary>
    Withdrawn = 3,
}

/// <summary>How the requester's identity was checked. Stored by number: never renumber.</summary>
public enum IdentityCheckMethod
{
    InPerson = 0,
    Documents = 1,
    KnownToService = 2,

    /// <summary>Another way, described in the check's detail.</summary>
    Other = 3,
}

/// <summary>How the response was sent. Stored by number: never renumber.</summary>
public enum DataRequestSendMethod
{
    Post = 0,
    HandedOver = 1,
    SecureEmail = 2,
    Email = 3,
    Other = 4,
}

/// <summary>What kind of item the clinician review withheld. Stored by number: never renumber.</summary>
public enum DataRequestItemKind
{
    /// <summary>A note on a pathway (every version of it).</summary>
    Note = 0,

    /// <summary>An attached file: left out of the pack's files, and its details out of the report.</summary>
    File = 1,

    Letter = 2,
    Result = 3,
    MdtItem = 4,
    Referral = 5,
}
