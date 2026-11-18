using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.DataRequests;
using MedApp.Domain.Activity;
using MedApp.Domain.DataRequests;
using MediatR;

namespace MedApp.Application.DataRequests.Queries;

/// <summary>One data request, for holders of ManagePrivacy (403 otherwise). Recorded as viewing the patient.</summary>
public sealed record GetDataRequestQuery(Guid DataRequestId, PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class GetDataRequestHandler(
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetDataRequestQuery, DataRequestResponse>
{
    public async Task<DataRequestResponse> Handle(GetDataRequestQuery request, CancellationToken cancellationToken)
    {
        DataRequestRules.EnsureCanHandle(request.Actor);
        var dataRequest = await repository.GetAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");

        await activityLog.RecordViewAsync(
            ActivityAction.PatientViewed, dataRequest.PatientId, new { dataRequestId = dataRequest.Id }, cancellationToken);

        return DataRequestMapper.ToResponse(dataRequest, request.Actor, UkTime.Today(timeProvider));
    }
}

/// <summary>
/// The items of the record the review may withhold, with their text (ManagePrivacy; 403 otherwise).
/// Recorded as viewing the patient.
/// </summary>
public sealed record GetDataRequestReviewItemsQuery(Guid DataRequestId, PathwayActor Actor)
    : IRequest<IReadOnlyList<DataRequestReviewItemResponse>>;

public sealed class GetDataRequestReviewItemsHandler(
    IDataRequestRepository repository,
    IPatientRecordRepository records,
    IIdentityReadService identity,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetDataRequestReviewItemsQuery, IReadOnlyList<DataRequestReviewItemResponse>>
{
    public async Task<IReadOnlyList<DataRequestReviewItemResponse>> Handle(
        GetDataRequestReviewItemsQuery request, CancellationToken cancellationToken)
    {
        DataRequestRules.EnsureCanHandle(request.Actor);
        var dataRequest = await repository.GetAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");
        var record = await records.GetAsync(dataRequest.PatientId, forUpdate: false, cancellationToken)
                     ?? throw new NotFoundException($"Patient '{dataRequest.PatientId}' was not found.");

        await activityLog.RecordViewAsync(
            ActivityAction.PatientViewed, dataRequest.PatientId, new { dataRequestId = dataRequest.Id, review = true }, cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(PatientExportMapper.UserIds(record), cancellationToken);
        var export = PatientExportMapper.ToResponse(
            record, usernames, timeProvider.GetUtcNow().UtcDateTime, request.Actor.Username);
        return DataRequestReviewItems.From(export, dataRequest);
    }
}

/// <summary>
/// The data requests list (ManagePrivacy; 403 otherwise): a status, one patient's, or the open ones
/// not yet reviewed. Records each patient listed.
/// </summary>
public sealed record SearchDataRequestsQuery(
    int Page,
    int PageSize,
    string? Status,
    Guid? PatientId,
    bool AwaitingReview,
    PathwayActor Actor) : IRequest<PagedResponse<DataRequestSummaryResponse>>;

public sealed class SearchDataRequestsValidator : AbstractValidator<SearchDataRequestsQuery>
{
    public static readonly string StatusMessage =
        $"Status must be one of: {string.Join(", ", Enum.GetNames<DataRequestStatus>())}.";

    public SearchDataRequestsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
        RuleFor(x => x.Status)
            .Must(s => PathwayEnums.TryParse<DataRequestStatus>(s, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage(StatusMessage);
    }
}

public sealed class SearchDataRequestsHandler(
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<SearchDataRequestsQuery, PagedResponse<DataRequestSummaryResponse>>
{
    public async Task<PagedResponse<DataRequestSummaryResponse>> Handle(
        SearchDataRequestsQuery request, CancellationToken cancellationToken)
    {
        DataRequestRules.EnsureCanHandle(request.Actor);

        var status = string.IsNullOrWhiteSpace(request.Status) ? (DataRequestStatus?)null : PathwayEnums.Parse<DataRequestStatus>(request.Status);
        var today = UkTime.Today(timeProvider);
        var result = await repository.SearchAsync(
            new DataRequestFilter(status, request.PatientId, request.AwaitingReview), request.Page, request.PageSize,
            cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(r => r.PatientId).Distinct(), cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, r => DataRequestMapper.ToSummary(r, today));
    }
}
