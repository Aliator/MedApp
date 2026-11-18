using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.PatientMessages.Common;
using MedApp.Application.PatientMessages.Repositories;
using MedApp.Contracts.PatientMessages.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.PatientMessages.Queries.GetAppointmentMessages;

/// <summary>
/// The messages about an appointment (404 for an unknown one). They hold the patient's mobile or email, so reading them is
/// recorded as viewing the patient (once per person every few minutes).
/// </summary>
public sealed class GetAppointmentMessagesHandler(
    IAppointmentRepository appointments,
    IPatientMessageRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog)
    : IRequestHandler<GetAppointmentMessagesQuery, List<PatientMessageResponse>>
{
    public async Task<List<PatientMessageResponse>> Handle(GetAppointmentMessagesQuery request, CancellationToken cancellationToken)
    {
        var appointment = await appointments.GetByIdAsync(request.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' was not found.");

        var messages = await repository.GetForAppointmentAsync(appointment.Id, cancellationToken);
        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [appointment.PatientId], cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(
            messages.Select(m => m.RequestedByUserId).OfType<Guid>().Distinct(), cancellationToken);
        return messages.Select(m => PatientMessageMapper.ToResponse(m, usernames)).ToList();
    }
}
