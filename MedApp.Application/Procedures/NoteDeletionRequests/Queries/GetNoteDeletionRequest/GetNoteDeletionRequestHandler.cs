using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Procedures.NoteDeletionRequests.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Queries.GetNoteDeletionRequest;

/// <summary>
/// A request to delete a note, with the note's text and the patient. It shows patient data, so reading it is recorded in
/// the activity log: as a view of the procedure, or of the patient once the procedure has been deleted (once per person
/// every few minutes).
/// </summary>
public sealed class GetNoteDeletionRequestHandler(
    INoteDeletionRequestRepository requests,
    IPatientProcedureRepository procedures,
    IPatientRepository patients,
    IIdentityReadService identity,
    IActivityLog activityLog)
    : IRequestHandler<GetNoteDeletionRequestQuery, NoteDeletionRequestResponse>
{
    public async Task<NoteDeletionRequestResponse> Handle(GetNoteDeletionRequestQuery request, CancellationToken cancellationToken)
    {
        var deletionRequest = await requests.GetByIdAsync(request.Id, cancellationToken)
                              ?? throw new NotFoundException($"Note deletion request '{request.Id}' was not found.");

        var procedure = await procedures.GetByIdAsync(deletionRequest.PatientProcedureId, cancellationToken);
        var patient = procedure?.Patient ?? await patients.GetByIdAsync(deletionRequest.PatientId, cancellationToken);

        if (procedure is not null)
            await activityLog.RecordViewsAsync(ActivityAction.ProcedureViewed, [procedure.PatientId], cancellationToken, procedure.Id);
        else if (patient is not null)
            await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [patient.Id], cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            NoteDeletionRequestMapper.UserIds(deletionRequest, procedure), cancellationToken);
        return NoteDeletionRequestMapper.ToResponse(deletionRequest, procedure, patient, usernames);
    }
}
