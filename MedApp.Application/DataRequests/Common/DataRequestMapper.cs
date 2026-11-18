using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.DataRequests;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>Data requests as responses: in full, with what the caller may do, and as list rows.</summary>
public static class DataRequestMapper
{
    public static DataRequestResponse ToResponse(DataRequest request, PathwayActor actor, DateOnly today)
    {
        var canChange = DataRequestRules.WhyCantChange(request) is null;
        var manages = actor.Has(Permission.ManagePrivacy);
        var whyNoPack = DataRequestRules.WhyNoPack(request);

        return new DataRequestResponse
        {
            Id = request.Id,
            PatientId = request.PatientId,
            PatientName = PatientMapper.FullName(request.Patient),
            ReceivedOn = request.ReceivedOn,
            RequestedByPatient = request.RequestedByPatient,
            RequesterName = request.RequesterName,
            RequesterRelationship = request.RequesterRelationship,
            IdentityCheckedOn = request.IdentityCheckedOn,
            IdentityCheckedByUsername = request.IdentityCheckedByUsername,
            IdentityCheckMethod = request.IdentityCheckMethod?.ToString(),
            IdentityCheckDetail = request.IdentityCheckDetail,
            FirstDueOn = DataRequestRules.FirstDueOn(request),
            DueOn = request.DueOn,
            ExtensionMonths = request.ExtensionMonths,
            ExtensionReason = request.ExtensionReason,
            Status = request.Status.ToString(),
            RefusalReason = request.RefusalReason,
            SentOn = request.SentOn,
            SendMethod = request.SendMethod?.ToString(),
            ClosedAtUtc = request.ClosedAtUtc,
            ClosedByUsername = request.ClosedByUsername,
            ReviewedAtUtc = request.ReviewedAtUtc,
            ReviewedByUsername = request.ReviewedByUsername,
            Withheld = request.WithheldItems
                .OrderBy(w => w.Kind).ThenBy(w => w.ItemId)
                .Select(w => new WithheldItemResponse { Kind = w.Kind.ToString(), ItemId = w.ItemId, Reason = w.Reason })
                .ToList(),
            CreatedAtUtc = request.CreatedAtUtc,
            CreatedByUsername = request.CreatedByUsername,
            LastUpdatedUtc = request.LastUpdatedUtc,
            Version = request.Version,
            IsOverdue = DataRequestRules.IsOverdue(request, today),
            CanManage = manages && canChange,
            CanReview = manages && canChange,
            CanMakePack = manages && whyNoPack is null,
            PackBlockedReason = manages && canChange ? whyNoPack : null
        };
    }

    public static DataRequestSummaryResponse ToSummary(DataRequest request, DateOnly today) => new()
    {
        Id = request.Id,
        PatientId = request.PatientId,
        PatientName = PatientMapper.FullName(request.Patient),
        ReceivedOn = request.ReceivedOn,
        DueOn = request.DueOn,
        Status = request.Status.ToString(),
        RequestedByPatient = request.RequestedByPatient,
        IsIdentityChecked = request.IsIdentityChecked,
        IsReviewed = request.IsReviewed,
        IsOverdue = DataRequestRules.IsOverdue(request, today)
    };
}
