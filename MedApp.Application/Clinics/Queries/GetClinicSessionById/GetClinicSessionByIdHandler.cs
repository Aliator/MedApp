using MedApp.Application.Activity.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Clinics.Queries.GetClinicSessionById;

public sealed class GetClinicSessionByIdHandler(
    IClinicRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetClinicSessionByIdQuery, ClinicSessionDetailResponse>
{
    public async Task<ClinicSessionDetailResponse> Handle(GetClinicSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionAsync(request.SessionId, cancellationToken)
                      ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");
        var appointments = await repository.GetSessionAppointmentsAsync(session.Id, cancellationToken);

        // Its slots name the patients booked into them: a read of their records (it fails closed).
        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, appointments.Select(a => a.PatientId), cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync([session.StaffUserId], cancellationToken);
        return ClinicMapper.ToDetailResponse(
            session,
            usernames.GetValueOrDefault(session.StaffUserId) ?? string.Empty,
            appointments,
            timeProvider.GetUtcNow().UtcDateTime,
            request.Actor.CanManageClinics);
    }
}
