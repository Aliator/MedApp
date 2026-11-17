using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;

public sealed class FinishProcedureStageHandler(
    IPatientProcedureRepository repository,
    IAppointmentRepository appointmentRepository,
    IIdentityReadService identityReadService,
    IProcedureMonitor procedureMonitor,
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
        var next = StageFinishing.NextStage(procedure, stage);
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

        StageFinishing.Finish(change, stage, appointments, request.Reason);

        if (change.HasChanges)
        {
            await repository.SaveAsync(change, cancellationToken);
            await procedureMonitor.NotifyAsync(change, cancellationToken);
        }

        return await ProcedureMapper.ToResponseAsync(procedure, identityReadService, now, cancellationToken, viewerId: request.Actor.UserId);
    }
}
