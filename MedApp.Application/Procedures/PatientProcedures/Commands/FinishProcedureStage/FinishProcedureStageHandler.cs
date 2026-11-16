using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

public sealed class FinishProcedureStageHandler(
    IPatientProcedureRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<FinishProcedureStageCommand, PatientProcedureResponse>
{
    public const string ReasonRequired = "A reason is required to finish a stage without its visit.";

    public async Task<PatientProcedureResponse> Handle(FinishProcedureStageCommand request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetForUpdateOrThrowAsync(request.ProcedureId, cancellationToken);
        var stage = procedure.GetStageOrThrow(request.StageId);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new ProcedureChange(procedure, request.Actor, now);

        if (ProcedureWorkflow.WhyStageCantBeEdited(procedure, stage) is { } reason)
            throw new ConflictException(reason);

        // The finished stage's appointments, and the next stage's (which becomes current and may need its visit asked for).
        var next = procedure.Stages
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault(s => s.StageOrder > stage.StageOrder && !s.IsCompleted);
        var appointments = await appointmentRepository.GetForStagesForUpdateAsync(
            next is null ? [stage.Id] : [stage.Id, next.Id], cancellationToken);
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();

        // A Required visit: only an admin can finish without it, on purpose and with a reason.
        if (ProcedureWorkflow.WhyStageNeedsVisit(stage, stageAppointments) is { } needsVisit)
        {
            if (!request.IsAdmin || !request.WithoutVisit)
                throw new ConflictException(needsVisit);

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ValidationException([new ValidationFailure(nameof(request.Reason), ReasonRequired)]);
        }

        var withoutVisit = ProcedureWorkflow.FinishesWithoutVisit(stage, stageAppointments)
            ? new FinishedWithoutVisit(stage.VisitRequirement, string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim())
            : null;

        change.FinishStage(stage, withoutVisit);

        // Its visit no longer needs booking. Booked ones stay: someone may need to tell the patient.
        foreach (var appointment in stageAppointments)
            change.CancelRequestedVisit(appointment, ProcedureChange.StageFinishedReason);

        StageVisits.RequestForCurrentStage(change, appointments);

        await repository.SaveAsync(change, cancellationToken);

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken);
    }
}
