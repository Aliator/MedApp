using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

/// <summary>A procedure in full. Opening it is recorded in the activity log, against its patient (once per person every few minutes).</summary>
public sealed class GetPatientProcedureByIdHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientProcedureByIdQuery, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(GetPatientProcedureByIdQuery request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException($"Procedure '{request.Id}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.ProcedureViewed, [procedure.PatientId], cancellationToken, procedure.Id);

        return await ProcedureMapper.ToResponseAsync(
            procedure, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
