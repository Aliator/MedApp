namespace MedApp.Contracts.Specimens.Requests;

/// <summary>Recording a specimen taken at a stage of a pathway (<c>POST api/pathways/{id}/specimens</c>).</summary>
public sealed class RecordSpecimenRequest
{
    /// <summary>The stage that took it: the pathway's current stage (or one unlocked by an admin).</summary>
    public Guid StageId { get; set; }

    public Guid SpecimenTypeId { get; set; }

    public Guid LaboratoryId { get; set; }

    /// <summary>When it was taken: now if left out, or up to 7 days back, never in the future.</summary>
    public DateTime? TakenAtUtc { get; set; }

    /// <summary>Where on the patient it was taken from ("left forearm"), 200 characters, optional.</summary>
    public string? Site { get; set; }

    /// <summary>Notes for the lab, 1,000 characters, optional.</summary>
    public string? RequestNotes { get; set; }

    /// <summary>The clinician who asked for it and will get its result.</summary>
    public Guid RequestingUserId { get; set; }
}

/// <summary>Correcting a specimen's details before it's sent (<c>PUT api/specimens/{id}</c>): every field is replaced.</summary>
public sealed class EditSpecimenRequest
{
    public Guid SpecimenTypeId { get; set; }

    public Guid LaboratoryId { get; set; }

    /// <summary>When it was taken: up to 7 days back, never in the future.</summary>
    public DateTime TakenAtUtc { get; set; }

    public string? Site { get; set; }

    public string? RequestNotes { get; set; }

    public Guid RequestingUserId { get; set; }

    /// <summary>The specimen's <c>version</c> the change was made from: a 409 if someone has changed it since.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>Recording a specimen sent to the lab, or received by it.</summary>
public sealed class SpecimenStepRequest
{
    /// <summary>When: now if left out, or up to 7 days back, never in the future and never before the step before.</summary>
    public DateTime? AtUtc { get; set; }

    /// <summary>Sent: the courier's or batch's reference. Received: the lab's own reference. 50 characters, optional.</summary>
    public string? Reference { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Recording that the lab rejected a specimen.</summary>
public sealed class RejectSpecimenRequest
{
    /// <summary>Unlabelled, Mislabelled, Leaked, InsufficientSample, WrongContainer or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>What was wrong, 500 characters; required for Other.</summary>
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Marking a specimen lost, or entered in error.</summary>
public sealed class CloseSpecimenRequest
{
    /// <summary>What happened, 500 characters, required.</summary>
    public string Detail { get; set; } = string.Empty;

    public int ExpectedVersion { get; set; }
}

/// <summary>Finding a specimen by its number, typed or scanned (in the body, never the URL).</summary>
public sealed class FindSpecimenRequest
{
    /// <summary>The specimen number, e.g. <c>MA26-000123</c>; case and spaces don't matter.</summary>
    public string Number { get; set; } = string.Empty;
}

/// <summary>Query string for <c>GET api/specimens/search</c>.</summary>
public sealed class SearchSpecimensRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// The lab's worklist views, oldest first: <c>ToSend</c> (taken, not sent), <c>Expected</c> (sent, not received),
    /// <c>InLab</c> (received, not reported), <c>Overdue</c> (open past the type's expected days),
    /// <c>RejectedAndLost</c> (the last 30 days), <c>Reported</c> (the last 7 days); or <c>All</c> (the default), newest
    /// taken first.
    /// </summary>
    public string? View { get; set; }

    public Guid? LaboratoryId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? PatientId { get; set; }
}

/// <summary>
/// Body of <c>POST api/specimens/search</c>: the Lab page's worklist, searched and sorted. A body, not a query string,
/// because the search may be a specimen number, which stays out of URLs (and so out of request logs).
/// </summary>
public sealed class SpecimenWorklistRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>As <see cref="SearchSpecimensRequest.View"/>.</summary>
    public string? View { get; set; }

    public Guid? LaboratoryId { get; set; }

    /// <summary>
    /// Text in the specimen's number (spaces ignored), the patient's name or NHS number, or the specimen type's name,
    /// ignoring case. At most 200 characters.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// <c>number</c>, <c>patient</c>, <c>lab</c>, <c>status</c> or <c>time</c> (the view's time: taken, sent,
    /// received, expected by, closed or reported). Without one, the view's own order.
    /// </summary>
    public string? Sort { get; set; }

    public bool Ascending { get; set; } = true;
}

/// <summary>Adding or changing a specimen type.</summary>
public sealed class SpecimenTypeRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Days from taken to reported the lab is expected to take, 1 to 90.</summary>
    public int ExpectedTurnaroundDays { get; set; }
}

/// <summary>Adding or changing a lab.</summary>
public sealed class LaboratoryRequest
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether the lab's own staff use MedApp to receive and report specimens.</summary>
    public bool UsesMedApp { get; set; }
}
