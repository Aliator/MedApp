using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;

public sealed class GetPatientProcedureByIdHandler(
    IPatientProcedureRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetPatientProcedureByIdQuery, PatientProcedureResponse>
{
    public async Task<PatientProcedureResponse> Handle(GetPatientProcedureByIdQuery request, CancellationToken cancellationToken)
    {
        var procedure = await repository.GetByIdAsync(request.Id, cancellationToken)
                        ?? throw new NotFoundException($"Procedure '{request.Id}' was not found.");

        return await ProcedureMapper.ToResponseAsync(
            procedure, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
