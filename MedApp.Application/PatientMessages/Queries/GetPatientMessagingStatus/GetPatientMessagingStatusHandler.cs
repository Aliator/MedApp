using MedApp.Application.PatientMessages.Common;
using MedApp.Contracts.PatientMessages.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.PatientMessages.Queries.GetPatientMessagingStatus;

public sealed class GetPatientMessagingStatusHandler(
    IOptions<PatientMessageOptions> options,
    IOptions<ReminderOptions> reminderOptions)
    : IRequestHandler<GetPatientMessagingStatusQuery, PatientMessagingStatusResponse>
{
    public Task<PatientMessagingStatusResponse> Handle(GetPatientMessagingStatusQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new PatientMessagingStatusResponse
        {
            Mode = options.Value.Mode.ToString(),
            IsOn = options.Value.IsOn,
            SendsMissedAppointmentMessages = options.Value.IsOn && reminderOptions.Value.MissedMessage,
            ReminderDaysBefore = reminderOptions.Value.DaysBefore,
        });
}
