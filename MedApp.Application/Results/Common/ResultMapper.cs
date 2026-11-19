using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Results.Responses;
using MedApp.Domain.Attachments;
using MedApp.Domain.Results;

namespace MedApp.Application.Results.Common;

/// <summary>Results as the API returns them: dates, names and statuses; nothing read from the report.</summary>
public static class ResultMapper
{
    /// <summary>
    /// A result's page: the result is loaded with its patient, pathway and specimen (with its type);
    /// <paramref name="usernames"/> has everyone it names (anyone missing is a "Deleted user"); <paramref name="report"/>
    /// is its file, or null once deleted; <paramref name="responsibleCanSignOff"/> says whether its responsible clinician
    /// still holds SignOffResults. The <c>Can...</c> flags follow <paramref name="viewer"/>'s permissions.
    /// </summary>
    public static ResultResponse ToResponse(
        Result result,
        IReadOnlyDictionary<Guid, string> usernames,
        Attachment? report,
        bool responsibleCanSignOff,
        PathwayActor viewer,
        DateTime nowUtc)
    {
        var canSignOff = viewer.Has(Permission.SignOffResults);
        var canProgress = ResultWorkflow.WhyCantProgress(result) is null;

        return new ResultResponse
        {
            Id = result.Id,
            PatientId = result.PatientId,
            Patient = PathwayMapper.ToPatient(result.Patient),
            PatientFlags = result.Patient is null ? null : PatientMapper.ToFlags(result.Patient),
            PathwayId = result.PathwayId,
            PathwayTitle = result.Pathway?.Title,
            SpecimenId = result.SpecimenId,
            SpecimenNumber = result.Specimen?.Number,
            SpecimenTypeName = result.Specimen?.SpecimenType?.Name,
            Source = result.Source.ToString(),
            Title = result.Title,
            ReportAttachmentId = result.ReportAttachmentId,
            ReportFileName = report is { DeletedAtUtc: null } ? report.FileName : null,
            ReportHasThumbnail = report is { DeletedAtUtc: null, HasThumbnail: true },
            LabCaseId = result.LabCaseId,
            LabCaseNumber = result.LabCase?.Number,
            ReportText = result.ReportText,
            ReportedAtUtc = result.ReportedAtUtc,
            ReportedByUsername = Username(usernames, result.ReportedByUserId),
            AgeDays = ResultWorkflow.AgeDays(result, nowUtc),
            ResponsibleUserId = result.ResponsibleUserId,
            ResponsibleUsername = Username(usernames, result.ResponsibleUserId),
            NeedsClinician = result.IsAwaitingSignOff && !responsibleCanSignOff,
            Status = result.Status.ToString(),
            IsAwaitingSignOff = result.IsAwaitingSignOff,
            SeenAtUtc = result.SeenAtUtc,
            SeenByUsername = Username(usernames, result.SeenByUserId),
            SignedOffAtUtc = result.SignedOffAtUtc,
            SignedOffByUsername = Username(usernames, result.SignedOffByUserId),
            Action = result.Action?.ToString(),
            ActionDetail = result.ActionDetail,
            EnteredInErrorAtUtc = result.EnteredInErrorAtUtc,
            EnteredInErrorByUsername = Username(usernames, result.EnteredInErrorByUserId),
            ErrorDetail = result.ErrorDetail,
            Version = result.Version,
            CanMarkSeen = canSignOff && ResultWorkflow.WhyCantMarkSeen(result) is null,
            CanSignOff = canSignOff && canProgress,
            CanReassign = canSignOff && canProgress,
            CanMarkEnteredInError = ResultWorkflow.MayMarkEnteredInError(result, viewer, nowUtc)
                                    && ResultWorkflow.WhyCantMarkEnteredInError(result) is null,
            ReadOnlyReason = ReadOnlyReason(result),
        };
    }

    /// <summary>
    /// A result in a list, with its patient and pathway: dates, ages, names and statuses only.
    /// <paramref name="signOffHolders"/> are the users who hold SignOffResults now (for Needs a clinician).
    /// </summary>
    public static ResultSummaryResponse ToSummary(
        Result result, IReadOnlyDictionary<Guid, string> usernames, IReadOnlySet<Guid> signOffHolders, DateTime nowUtc) => new()
    {
        Id = result.Id,
        PatientId = result.PatientId,
        PatientName = result.Patient is null ? string.Empty : PatientMapper.FullName(result.Patient),
        PatientNhsNumber = result.Patient?.NhsNumber,
        PatientDateOfBirth = result.Patient?.DateOfBirth ?? default,
        PathwayId = result.PathwayId,
        PathwayTitle = result.Pathway?.Title,
        Source = result.Source.ToString(),
        Title = result.Title,
        SpecimenNumber = result.Specimen?.Number,
        ReportedAtUtc = result.ReportedAtUtc,
        AgeDays = ResultWorkflow.AgeDays(result, nowUtc),
        ResponsibleUsername = Username(usernames, result.ResponsibleUserId),
        NeedsClinician = result.IsAwaitingSignOff && !signOffHolders.Contains(result.ResponsibleUserId),
        Status = result.Status.ToString(),
        IsAwaitingSignOff = result.IsAwaitingSignOff,
        SignedOffAtUtc = result.SignedOffAtUtc,
        SignedOffByUsername = Username(usernames, result.SignedOffByUserId),
        Action = result.Action?.ToString(),
    };

    /// <summary>Everyone a result names, for one username lookup.</summary>
    public static IEnumerable<Guid> PeopleIn(Result result) =>
        new[]
        {
            result.ResponsibleUserId, result.ReportedByUserId, result.SeenByUserId, result.SignedOffByUserId,
            result.EnteredInErrorByUserId
        }.OfType<Guid>().Distinct();

    /// <summary>Why the result can't change now: the record's anonymisation, or its being closed.</summary>
    public static string? ReadOnlyReason(Result result) =>
        PatientRules.IsAnonymised(result.Patient) ? PatientRules.AnonymisedMessage
        : result.Status == ResultStatus.SignedOff ? "This result has been signed off. Anything later goes in a pathway note."
        : result.Status == ResultStatus.EnteredInError ? "This result was entered in error."
        : null;

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : PathwayMapper.DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
