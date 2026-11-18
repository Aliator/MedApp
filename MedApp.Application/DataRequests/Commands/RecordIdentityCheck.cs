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

/// <summary>Records how and when the requester's identity was checked (ManagePrivacy). Recording it again replaces it.</summary>
public sealed record RecordIdentityCheckCommand(
    Guid DataRequestId,
    DateOnly CheckedOn,
    string Method,
    string? Detail,
    int ExpectedVersion,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class RecordIdentityCheckValidator : AbstractValidator<RecordIdentityCheckCommand>
{
    public static readonly string MethodMessage =
        $"Method must be one of: {string.Join(", ", Enum.GetNames<IdentityCheckMethod>())}.";

    public RecordIdentityCheckValidator()
    {
        RuleFor(x => x.DataRequestId).NotEmpty().WithMessage("Data request id is required.");
        RuleFor(x => x.CheckedOn).NotEmpty().WithMessage("Give the date the identity was checked.");
        RuleFor(x => x.Method).Must(m => PathwayEnums.TryParse<IdentityCheckMethod>(m, out _)).WithMessage(MethodMessage);
        RuleFor(x => x.Detail)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .When(x => PathwayEnums.TryParse<IdentityCheckMethod>(x.Method, out var m) && m == IdentityCheckMethod.Other)
            .WithMessage("Say how the identity was checked.");
        RuleFor(x => x.Detail).MaximumLength(DataRequest.MaxDetailLength)
            .WithMessage($"The detail can be at most {DataRequest.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Expected version is required.");
    }
}

public sealed class RecordIdentityCheckHandler(
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<RecordIdentityCheckCommand, DataRequestResponse>
{
    public async Task<DataRequestResponse> Handle(RecordIdentityCheckCommand request, CancellationToken cancellationToken)
    {
        DataRequestRules.EnsureCanHandle(request.Actor);
        var dataRequest = await repository.GetForUpdateAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");
        DataRequestRules.EnsureCanChange(dataRequest, request.ExpectedVersion);

        var today = UkTime.Today(timeProvider);
        if (DataRequestRules.WhyEventDateInvalid(dataRequest, request.CheckedOn, today, "checked") is { } bad)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.CheckedOn), bad)]);

        dataRequest.IdentityCheckedOn = request.CheckedOn;
        dataRequest.IdentityCheckMethod = PathwayEnums.Parse<IdentityCheckMethod>(request.Method);
        dataRequest.IdentityCheckDetail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        dataRequest.IdentityCheckedByUserId = request.Actor.UserId;
        dataRequest.IdentityCheckedByUsername = request.Actor.Username;
        DataRequestChanges.Touch(dataRequest, timeProvider.GetUtcNow().UtcDateTime);

        await repository.SaveAsync(DataRequestChanges.Entry(activityLog, dataRequest, DataRequestChange.IdentityChecked), cancellationToken);
        return DataRequestMapper.ToResponse(dataRequest, request.Actor, today);
    }
}
