using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.NoteDeletionRequests.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;

/// <summary>
/// A request to delete a note, with the note's text and the patient. It shows patient data, so reading it is recorded in
/// the activity log: as a view of the pathway, or of the patient once the pathway has been deleted (once per person
/// every few minutes).
/// </summary>
public sealed class GetNoteDeletionRequestHandler(
    INoteDeletionRequestRepository requests,
    IPatientPathwayRepository pathways,
    IPatientRepository patients,
    IIdentityReadService identity,
    IActivityLog activityLog)
    : IRequestHandler<GetNoteDeletionRequestQuery, NoteDeletionRequestResponse>
{
    public async Task<NoteDeletionRequestResponse> Handle(GetNoteDeletionRequestQuery request, CancellationToken cancellationToken)
    {
        var deletionRequest = await requests.GetByIdAsync(request.Id, cancellationToken)
                              ?? throw new NotFoundException($"Note deletion request '{request.Id}' was not found.");

        var pathway = await pathways.GetByIdAsync(deletionRequest.PatientPathwayId, cancellationToken);
        var patient = pathway?.Patient ?? await patients.GetByIdAsync(deletionRequest.PatientId, cancellationToken);

        if (pathway is not null)
            await activityLog.RecordViewsAsync(ActivityAction.PathwayViewed, [pathway.PatientId], cancellationToken, pathway.Id);
        else if (patient is not null)
            await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [patient.Id], cancellationToken);

        var usernames = await identity.GetUsernamesByIdAsync(
            NoteDeletionRequestMapper.UserIds(deletionRequest, pathway), cancellationToken);
        return NoteDeletionRequestMapper.ToResponse(deletionRequest, pathway, patient, usernames);
    }
}
