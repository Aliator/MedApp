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
/// Extends the due date to 1 or 2 more months in all (ManagePrivacy), with the reason given to the requester; only within
/// the first month (409 after). Extending again replaces the extension.
/// </summary>
public sealed record ExtendDataRequestCommand(
    Guid DataRequestId,
    int Months,
    string Reason,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class ExtendDataRequestValidator : AbstractValidator<ExtendDataRequestCommand>
{
    public ExtendDataRequestValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.Months).InclusiveBetween(1, DataRequest.MaxExtensionMonths)
            .WithMessage($"An extension is 1 or {DataRequest.MaxExtensionMonths} more months.");
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Give the reason for the extension.")
            .MaximumLength(DataRequest.MaxDetailLength)
            .WithMessage($"The reason can be at most {DataRequest.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

public sealed class ExtendDataRequestHandler(
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<ExtendDataRequestCommand, DataRequestResponse>
{
    public async Task<DataRequestResponse> Handle(ExtendDataRequestCommand request, CancellationToken cancellationToken)
    {
        var dataRequest = await repository.GetForUpdateAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");
        DataRequestRules.EnsureCanChange(dataRequest, request.ExpectedVersion);

        var today = UkTime.Today(timeProvider);
        if (DataRequestRules.WhyCantExtend(dataRequest, today) is { } why)
            throw new ConflictException(why);

        dataRequest.ExtensionMonths = request.Months;
        dataRequest.ExtensionReason = request.Reason.Trim();
        dataRequest.DueOn = DataRequestRules.DueOn(dataRequest.ReceivedOn, request.Months);
        DataRequestChanges.Touch(dataRequest, timeProvider.GetUtcNow().UtcDateTime);

        await repository.SaveAsync(DataRequestChanges.Entry(activityLog, dataRequest, DataRequestChange.Extended), cancellationToken);
        return DataRequestMapper.ToResponse(dataRequest, request.Actor, today);
    }
}
