using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.DataRequests;
using MedApp.Domain.DataRequests;
using MediatR;

namespace MedApp.Application.DataRequests.Commands;

/// <summary>An item to withhold: its kind's name, its ID and why.</summary>
public sealed record WithheldItemInput(string Kind, Guid ItemId, string Reason);

/// <summary>
/// The clinician review (decision 161, SignOffResults as the proxy for a clinician): what's withheld from the pack under
/// the serious harm test, each with why; empty when nothing is. Replaces any earlier review; who reviewed and when are
/// kept on the request.
/// </summary>
public sealed record RecordDataRequestReviewCommand(
    Guid DataRequestId,
    IReadOnlyList<WithheldItemInput> Withheld,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class RecordDataRequestReviewValidator : AbstractValidator<RecordDataRequestReviewCommand>
{
    /// <summary>The most items one review can withhold.</summary>
    public const int MaxWithheld = 500;

    public static readonly string KindMessage =
        $"Kind must be one of: {string.Join(", ", Enum.GetNames<DataRequestItemKind>())}.";

    public RecordDataRequestReviewValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.Withheld).NotNull().Must(w => w.Count <= MaxWithheld)
            .WithMessage($"A review can withhold at most {MaxWithheld} items.");
        RuleFor(x => x.Withheld)
            .Must(w => w.Select(i => (i.Kind, i.ItemId)).Distinct().Count() == w.Count)
            .WithMessage("Each item can be withheld only once.");
        RuleForEach(x => x.Withheld).ChildRules(item =>
        {
            item.RuleFor(i => i.Kind).Must(k => PathwayEnums.TryParse<DataRequestItemKind>(k, out _)).WithMessage(KindMessage);
            item.RuleFor(i => i.ItemId).NotEmpty().WithMessage("Item id is required.");
            item.RuleFor(i => i.Reason)
                .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Give the reason it's withheld.")
                .MaximumLength(DataRequest.MaxDetailLength)
                .WithMessage($"The reason can be at most {DataRequest.MaxDetailLength} characters.");
        });
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

/// <summary>
/// Records the review. 404 for no such request; 409 when closed, anonymised or changed meanwhile; 400 for an item that
/// isn't in the patient's record (each must be one of the review's items).
/// </summary>
public sealed class RecordDataRequestReviewHandler(
    IDataRequestRepository repository,
    IPatientRecordRepository records,
    IIdentityReadService identity,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<RecordDataRequestReviewCommand, DataRequestResponse>
{
    public async Task<DataRequestResponse> Handle(RecordDataRequestReviewCommand request, CancellationToken cancellationToken)
    {
        var dataRequest = await repository.GetForUpdateAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");
        DataRequestRules.EnsureCanChange(dataRequest, request.ExpectedVersion);

        // Only items the record holds, so a review can't name another patient's.
        var record = await records.GetAsync(dataRequest.PatientId, forUpdate: false, cancellationToken)
                     ?? throw new NotFoundException($"Patient '{dataRequest.PatientId}' was not found.");
        var usernames = await identity.GetUsernamesByIdAsync(PatientExportMapper.UserIds(record), cancellationToken);
        var export = PatientExportMapper.ToResponse(record, usernames, timeProvider.GetUtcNow().UtcDateTime, request.Actor.Username);
        var known = DataRequestReviewItems.From(export, dataRequest)
            .Select(i => (Kind: Enum.Parse<DataRequestItemKind>(i.Kind), i.ItemId))
            .ToHashSet();

        var withheld = request.Withheld
            .Select(w => (Kind: PathwayEnums.Parse<DataRequestItemKind>(w.Kind), w.ItemId, Reason: w.Reason.Trim()))
            .ToList();
        var unknown = withheld.FindIndex(w => !known.Contains((w.Kind, w.ItemId)));
        if (unknown >= 0)
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    $"Withheld[{unknown}].ItemId", "That item isn't in this patient's record.")
            ]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        dataRequest.WithheldItems.Clear();
        dataRequest.WithheldItems.AddRange(withheld.Select(w => new DataRequestWithheldItem
        {
            Id = Guid.NewGuid(),
            DataRequestId = dataRequest.Id,
            Kind = w.Kind,
            ItemId = w.ItemId,
            Reason = w.Reason
        }));
        dataRequest.ReviewedAtUtc = now;
        dataRequest.ReviewedByUserId = request.Actor.UserId;
        dataRequest.ReviewedByUsername = request.Actor.Username;
        DataRequestChanges.Touch(dataRequest, now);

        await repository.SaveAsync(DataRequestChanges.Entry(activityLog, dataRequest, DataRequestChange.Reviewed), cancellationToken);
        return DataRequestMapper.ToResponse(dataRequest, request.Actor, UkTime.Today(timeProvider));
    }
}
