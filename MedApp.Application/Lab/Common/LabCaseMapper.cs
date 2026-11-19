using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Lab.Common;

/// <summary>Lab cases as the API returns them (the light demo's <c>caseResponse</c> and <c>caseSummary</c>).</summary>
public static class LabCaseMapper
{
    /// <summary>Everyone a case names, for one username lookup.</summary>
    public static IEnumerable<Guid> PeopleIn(LabCase labCase) =>
        new Guid?[]
            {
                labCase.OpenedByUserId, labCase.ReportedByUserId, labCase.RepeatRequestedByUserId, labCase.EnteredInErrorByUserId,
                labCase.ReportSentByUserId, labCase.ClosedByUserId
            }
            .Concat(labCase.Steps.SelectMany(s => new Guid?[] { s.ByUserId, s.EnteredInErrorByUserId }))
            .Concat(labCase.SurgeryDetails.Select(d => (Guid?)d.AddedByUserId))
            .Concat(labCase.Specimens.SelectMany(SpecimenMapper.PeopleIn).Select(id => (Guid?)id))
            .OfType<Guid>()
            .Distinct();

    /// <summary>Where it came from, in words: its clinic's name, or its hospital's (MC18).</summary>
    public static string? SourceName(LabCase labCase) =>
        labCase.Source == SpecimenSource.Hospital ? HospitalName(labCase) ?? SpecimenMapper.UnknownHospital : labCase.RequestingTeam?.Name;

    /// <summary>The hospital's name, for a hospital case; null for a clinic's.</summary>
    public static string? HospitalName(LabCase labCase) =>
        labCase.HospitalId is null ? null : labCase.Hospital?.Name ?? labCase.HospitalRequest?.Hospital?.Name ?? SpecimenMapper.UnknownHospital;

    /// <summary>A hospital case's report as it went back (MC20), or null before it was sent.</summary>
    public static LabCaseHospitalReportResponse? ToHospitalReport(LabCase labCase, IReadOnlyDictionary<Guid, string> usernames) =>
        labCase.ReportSentAtUtc is not { } sentAtUtc
            ? null
            : new LabCaseHospitalReportResponse
            {
                Status = "Final",
                LetterKind = LabCaseRules.LabReportLetterKind,
                HospitalId = labCase.HospitalId ?? Guid.Empty,
                HospitalName = HospitalName(labCase),
                AddressedTo = labCase.ReportAddressedTo,
                DeliveryMethod = labCase.ReportDeliveryMethod?.ToString(),
                IssuedAtUtc = labCase.ReportedAtUtc ?? sentAtUtc,
                SentAtUtc = sentAtUtc,
                SentByUsername = Username(usernames, labCase.ReportSentByUserId),
            };

    public static LabCaseResponse ToResponse(
        LabCase labCase,
        IReadOnlyDictionary<Guid, string> usernames,
        PathwayActor viewer,
        Guid? labTeamId,
        DateTime nowUtc)
    {
        var next = LabCaseRules.Next(labCase);
        var reason = LabCaseRules.ReadOnlyReason(labCase, viewer, labTeamId);
        var canWork = reason is null;
        var latest = LabCaseRules.LatestLive(labCase);
        var stage = labCase.Pathway?.Stages.FirstOrDefault(s => s.Id == labCase.StageId);

        return new LabCaseResponse
        {
            Id = labCase.Id,
            Number = labCase.Number,
            Status = labCase.Status.ToString(),
            Source = labCase.Source.ToString(),
            LabTestId = labCase.LabTestId,
            LabTestName = labCase.LabTest?.Name,
            LabTestKind = labCase.LabTest?.Kind.ToString(),
            PatientId = labCase.PatientId,
            Patient = PathwayMapper.ToPatient(labCase.Patient),
            PatientFlags = labCase.Patient is null ? null : PatientMapper.ToFlags(labCase.Patient),
            RequestingTeamId = labCase.RequestingTeamId,
            RequestingTeamName = labCase.RequestingTeam?.Name,
            HospitalId = labCase.HospitalId,
            HospitalName = HospitalName(labCase),
            HospitalRequestId = labCase.HospitalRequestId,
            PathwayId = labCase.PathwayId,
            PathwayTitle = labCase.Pathway?.Title,
            StageId = labCase.StageId,
            StageName = stage?.Name,
            OpenedAtUtc = labCase.OpenedAtUtc,
            OpenedByUsername = Username(usernames, labCase.OpenedByUserId),
            ReceivedAtUtc = labCase.ReceivedAtUtc,
            Clock = LabClock.Of(labCase, nowUtc),
            Steps = labCase.Steps
                .OrderBy(s => s.Sequence)
                .Select(s => new LabCaseStepResponse
                {
                    Id = s.Id,
                    Kind = s.Kind.ToString(),
                    Name = LabCaseRules.StepName(labCase, s),
                    TestStepId = s.TestStepId,
                    StepKind = labCase.LabTest?.Steps.FirstOrDefault(t => t.Id == s.TestStepId)?.Kind.ToString(),
                    Choice = s.Choice?.ToString(),
                    Outcome = s.Outcome?.ToString(),
                    Detail = s.Detail,
                    AtUtc = s.AtUtc,
                    ByUsername = Username(usernames, s.ByUserId),
                    IsEnteredInError = s.IsEnteredInError,
                    EnteredInErrorAtUtc = s.EnteredInErrorAtUtc,
                    EnteredInErrorByUsername = Username(usernames, s.EnteredInErrorByUserId),
                    ErrorDetail = s.ErrorDetail,
                    CanMarkEnteredInError = canWork
                                            && s.Kind != LabCaseStepKind.Received
                                            && !s.IsEnteredInError
                                            && latest?.Id == s.Id
                                            && s.ByUserId == viewer.UserId,
                })
                .ToList(),
            Next = next is null ? null : ToNext(next),
            Samples = labCase.Specimens
                .OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Number)
                .Select(s => SpecimenMapper.ToSummary(s, nowUtc))
                .ToList(),
            ReportText = labCase.ReportText,
            ReportedAtUtc = labCase.ReportedAtUtc,
            ReportedByUsername = Username(usernames, labCase.ReportedByUserId),
            ResultId = labCase.ResultId,
            RepeatDetail = labCase.RepeatDetail,
            RepeatRequestedAtUtc = labCase.RepeatRequestedAtUtc,
            RepeatRequestedByUsername = Username(usernames, labCase.RepeatRequestedByUserId),
            ErrorDetail = labCase.ErrorDetail,
            EnteredInErrorAtUtc = labCase.EnteredInErrorAtUtc,
            EnteredInErrorByUsername = Username(usernames, labCase.EnteredInErrorByUserId),
            HospitalReport = ToHospitalReport(labCase, usernames),
            SurgeryDetails = labCase.SurgeryDetails
                .OrderBy(d => d.AddedAtUtc).ThenBy(d => d.Id)
                .Select(d => new LabCaseSurgeryDetailsResponse
                {
                    Id = d.Id,
                    SurgeryOn = d.SurgeryOn,
                    Procedure = d.Procedure,
                    Outcome = d.Outcome,
                    NoSurgeryExpected = d.NoSurgeryExpected,
                    Detail = d.Detail,
                    AddedAtUtc = d.AddedAtUtc,
                    AddedByUsername = Username(usernames, d.AddedByUserId),
                })
                .ToList(),
            ClosedAtUtc = labCase.ClosedAtUtc,
            ClosedByUsername = Username(usernames, labCase.ClosedByUserId),
            Version = labCase.Version,
            CanRecord = canWork && next is { Kind: LabNextKind.Step or LabNextKind.Choice },
            CanAuthorise = canWork && next?.Kind == LabNextKind.Report && viewer.Has(Permission.AuthoriseLabReports),
            CanMarkEnteredInError = LabCaseRules.MayMarkEnteredInError(labCase, viewer, labTeamId, nowUtc),
            CanAddSurgeryDetails = LabCaseRules.MayAddSurgeryDetails(labCase, viewer, labTeamId),
            ReadOnlyReason = reason,
        };
    }

    public static LabCaseNextResponse ToNext(LabNext next) => new()
    {
        Kind = next.Kind.ToString(),
        Name = next.Name,
        TestStepId = next.Kind == LabNextKind.Step ? next.TestStep?.Id : null,
        StepKind = next.Kind == LabNextKind.Step ? next.TestStep?.Kind.ToString() : null,
        Choice = next is { Kind: LabNextKind.Choice, Choice: { } choice }
            ? new LabCaseChoiceResponse
            {
                Choice = choice.ToString(),
                Question = next.Name,
                Outcomes = (next.Outcomes ?? []).Select(o => o.ToString()).ToList(),
            }
            : null,
    };

    public static LabCaseSummaryResponse ToSummary(LabCase labCase, DateTime nowUtc)
    {
        var next = LabCaseRules.Next(labCase);
        return new LabCaseSummaryResponse
        {
            Id = labCase.Id,
            Number = labCase.Number,
            Status = labCase.Status.ToString(),
            Source = labCase.Source.ToString(),
            SourceName = SourceName(labCase),
            PatientId = labCase.PatientId,
            PatientName = labCase.Patient is null ? string.Empty : PatientMapper.FullName(labCase.Patient),
            PatientNhsNumber = labCase.Patient?.NhsNumber,
            LabTestName = labCase.LabTest?.Name,
            NextName = next?.Name,
            NextKind = next?.Kind.ToString(),
            OpenedAtUtc = labCase.OpenedAtUtc,
            ReportedAtUtc = labCase.ReportedAtUtc,
            ReportSentAtUtc = labCase.ReportSentAtUtc,
            Clock = LabClock.Of(labCase, nowUtc),
        };
    }

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? usernames.TryGetValue(id, out var name) ? name : PathwayMapper.DeletedUser : null;
}
