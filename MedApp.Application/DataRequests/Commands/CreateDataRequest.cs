using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.DataRequests;
using MedApp.Domain.DataRequests;
using MediatR;

namespace MedApp.Application.DataRequests.Commands;

/// <summary>Logs a patient's data request as received (ManagePrivacy).</summary>
public sealed record CreateDataRequestCommand(
    Guid PatientId,
    DateOnly ReceivedOn,
    bool RequestedByPatient,
    string? RequesterName,
    string? RequesterRelationship,
    PathwayActor Actor) : IRequest<DataRequestResponse>;

public sealed class CreateDataRequestValidator : AbstractValidator<CreateDataRequestCommand>
{
    public CreateDataRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Patient id is required.");
        RuleFor(x => x.ReceivedOn).NotEmpty().WithMessage("Give the date the request was received.");

        When(x => !x.RequestedByPatient, () =>
        {
            RuleFor(x => x.RequesterName)
                .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Give the name of who asked on the patient's behalf.")
                .MaximumLength(DataRequest.MaxRequesterNameLength)
                .WithMessage($"The name can be at most {DataRequest.MaxRequesterNameLength} characters.");
            RuleFor(x => x.RequesterRelationship)
                .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Give their relationship to the patient, or their authority to ask.")
                .MaximumLength(DataRequest.MaxRelationshipLength)
                .WithMessage($"The relationship can be at most {DataRequest.MaxRelationshipLength} characters.");
        });
    }
}

/// <summary>
/// Logs the request with its due date (one calendar month from receipt). 404 for no such patient; 409 for an anonymised
/// record. Allowed while the record is restricted: restriction doesn't remove the patient's right of access.
/// </summary>
public sealed class CreateDataRequestHandler(
    IPatientRepository patients,
    IDataRequestRepository repository,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<CreateDataRequestCommand, DataRequestResponse>
{
    public async Task<DataRequestResponse> Handle(CreateDataRequestCommand request, CancellationToken cancellationToken)
    {
        var patient = await patients.GetByIdAsync(request.PatientId, cancellationToken)
                      ?? throw new NotFoundException($"Patient '{request.PatientId}' was not found.");
        if (PatientRules.IsAnonymised(patient))
            throw new ConflictException(DataRequestRules.AnonymisedMessage);

        var today = UkTime.Today(timeProvider);
        if (DataRequestRules.WhyReceivedOnInvalid(request.ReceivedOn, today) is { } bad)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.ReceivedOn), bad)]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dataRequest = new DataRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            ReceivedOn = request.ReceivedOn,
            RequestedByPatient = request.RequestedByPatient,
            RequesterName = request.RequestedByPatient ? null : request.RequesterName!.Trim(),
            RequesterRelationship = request.RequestedByPatient ? null : request.RequesterRelationship!.Trim(),
            DueOn = DataRequestRules.DueOn(request.ReceivedOn, 0),
            Status = DataRequestStatus.Open,
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId,
            CreatedByUsername = request.Actor.Username,
            LastUpdatedUtc = now,
            Version = 1
        };

        await repository.AddAsync(dataRequest, DataRequestChanges.Entry(activityLog, dataRequest, DataRequestChange.Recorded), cancellationToken);

        dataRequest.Patient = patient;
        return DataRequestMapper.ToResponse(dataRequest, request.Actor, today);
    }
}
