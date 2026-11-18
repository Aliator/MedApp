using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.DataRequests;
using MedApp.Domain.DataRequests;
using MediatR;

namespace MedApp.Application.DataRequests.Commands;

/// <summary>
/// Records that the response was sent, on a UK date and how (ManagePrivacy). Only once the pack could be made (identity
/// checked and reviewed: 409 otherwise), so nothing is recorded as answered without those controls (DPIA risk 37).
/// </summary>
public sealed record RecordDataRequestSentCommand(
    Guid DataRequestId,
    DateOnly SentOn,
    string Method,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

/// <summary>Refuses the request, with the reason given to the requester (ManagePrivacy).</summary>
public sealed record RefuseDataRequestCommand(
    Guid DataRequestId,
    string Reason,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

/// <summary>Records that the requester withdrew the request (ManagePrivacy).</summary>
public sealed record WithdrawDataRequestCommand(
    Guid DataRequestId,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class RecordDataRequestSentValidator : AbstractValidator<RecordDataRequestSentCommand>
{
    public static readonly string MethodMessage =
        $"Method must be one of: {string.Join(", ", Enum.GetNames<DataRequestSendMethod>())}.";

    public RecordDataRequestSentValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.SentOn).NotEmpty().WithMessage("Give the date the response was sent.");
        RuleFor(x => x.Method).Must(m => PathwayEnums.TryParse<DataRequestSendMethod>(m, out _)).WithMessage(MethodMessage);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

public sealed class RefuseDataRequestValidator : AbstractValidator<RefuseDataRequestCommand>
{
    public RefuseDataRequestValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Give the reason for refusing.")
            .MaximumLength(DataRequest.MaxDetailLength)
            .WithMessage($"The reason can be at most {DataRequest.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

public sealed class WithdrawDataRequestValidator : AbstractValidator<WithdrawDataRequestCommand>
{
    public WithdrawDataRequestValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

/// <summary>Closes a data request: sent, refused or withdrawn. 404 for none; 409 when closed, anonymised or changed meanwhile.</summary>
public sealed class CloseDataRequestHandler(
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<RecordDataRequestSentCommand, DataRequestResponse>,
      IRequestHandler<RefuseDataRequestCommand, DataRequestResponse>,
      IRequestHandler<WithdrawDataRequestCommand, DataRequestResponse>
{
    public Task<DataRequestResponse> Handle(RecordDataRequestSentCommand request, CancellationToken cancellationToken) =>
        CloseAsync(request.DataRequestId, request.ExpectedVersion, request.Actor, DataRequestChange.Sent, (dataRequest, today) =>
        {
            if (DataRequestRules.WhyNoPack(dataRequest) is { } why)
                throw new ConflictException(why);
            if (DataRequestRules.WhyEventDateInvalid(dataRequest, request.SentOn, today, "sent") is { } bad)
                throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.SentOn), bad)]);

            dataRequest.Status = DataRequestStatus.Sent;
            dataRequest.SentOn = request.SentOn;
            dataRequest.SendMethod = PathwayEnums.Parse<DataRequestSendMethod>(request.Method);
        }, cancellationToken);

    public Task<DataRequestResponse> Handle(RefuseDataRequestCommand request, CancellationToken cancellationToken) =>
        CloseAsync(request.DataRequestId, request.ExpectedVersion, request.Actor, DataRequestChange.Refused, (dataRequest, _) =>
        {
            dataRequest.Status = DataRequestStatus.Refused;
            dataRequest.RefusalReason = request.Reason.Trim();
        }, cancellationToken);

    public Task<DataRequestResponse> Handle(WithdrawDataRequestCommand request, CancellationToken cancellationToken) =>
        CloseAsync(request.DataRequestId, request.ExpectedVersion, request.Actor, DataRequestChange.Withdrawn, (dataRequest, _) =>
            dataRequest.Status = DataRequestStatus.Withdrawn, cancellationToken);

    private async Task<DataRequestResponse> CloseAsync(
        Guid id, int expectedVersion, PathwayActor actor, DataRequestChange change, Action<DataRequest, DateOnly> close,
        CancellationToken cancellationToken)
    {
        var dataRequest = await repository.GetForUpdateAsync(id, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{id}' was not found.");
        DataRequestRules.EnsureCanChange(dataRequest, expectedVersion);

        var today = UkTime.Today(timeProvider);
        close(dataRequest, today);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        dataRequest.ClosedAtUtc = now;
        dataRequest.ClosedByUserId = actor.UserId;
        dataRequest.ClosedByUsername = actor.Username;
        DataRequestChanges.Touch(dataRequest, now);

        await repository.SaveAsync(DataRequestChanges.Entry(activityLog, dataRequest, change), cancellationToken);
        return DataRequestMapper.ToResponse(dataRequest, actor, today);
    }
}
