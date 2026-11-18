namespace MedApp.Contracts.Results.Requests;

/// <summary>Marking an attached file as a result from elsewhere (<c>POST api/results/external</c>).</summary>
public sealed class RecordExternalResultRequest
{
    /// <summary>The attached file that is the report (on the patient, one of their pathways or a stage).</summary>
    public Guid AttachmentId { get; set; }

    /// <summary>What it is ("CT chest report from another hospital"), 200 characters.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The patient's pathway it belongs to, if any; left out, the file's own pathway (if it has one).</summary>
    public Guid? PathwayId { get; set; }

    /// <summary>The clinician who must sign it off (a holder of SignOffResults).</summary>
    public Guid ResponsibleUserId { get; set; }
}

/// <summary>Marking a result seen (<c>POST api/results/{id}/seen</c>).</summary>
public sealed class ResultVersionRequest
{
    /// <summary>The version of the result the caller saw: a 409 if it's changed since.</summary>
    public int ExpectedVersion { get; set; }
}

/// <summary>Signing a result off (<c>POST api/results/{id}/sign-off</c>).</summary>
public sealed class SignOffResultRequest
{
    /// <summary>
    /// What was done, chosen by the clinician: <c>NoFurtherAction</c>, <c>PatientInformed</c>,
    /// <c>AppointmentArranged</c>, <c>ForMdtDiscussion</c>, <c>ReferredOn</c> or <c>Other</c>.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The clinician's detail, 1,000 characters; required for Other.</summary>
    public string? Detail { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Giving a result to another responsible clinician, or taking it over (<c>POST api/results/{id}/reassign</c>).</summary>
public sealed class ReassignResultRequest
{
    /// <summary>The new responsible clinician (a holder of SignOffResults): the caller's own ID to take it over.</summary>
    public Guid ResponsibleUserId { get; set; }

    public int ExpectedVersion { get; set; }
}

/// <summary>Marking a result entered in error (<c>POST api/results/{id}/entered-in-error</c>).</summary>
public sealed class MarkResultEnteredInErrorRequest
{
    /// <summary>What was wrong (the wrong patient, the wrong specimen), 500 characters.</summary>
    public string Detail { get; set; } = string.Empty;

    public int ExpectedVersion { get; set; }
}

/// <summary>A page of results (<c>GET api/results/search</c>).</summary>
public sealed class SearchResultsRequest
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>
    /// <c>AwaitingMine</c> (awaiting the caller's sign-off), <c>Awaiting</c> (awaiting sign-off, everyone's),
    /// <c>NeedsClinician</c> (awaiting, whose responsible clinician can no longer sign off), each oldest report first;
    /// <c>SignedOff</c>, newest signed off first; or <c>All</c> (the default), newest report first.
    /// </summary>
    public string? View { get; set; }

    /// <summary>Only results on pathways of this specialty.</summary>
    public Guid? SpecialtyId { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? PatientId { get; set; }
}
