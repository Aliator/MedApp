using MedApp.Application.Calendar.Common;

namespace MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;

public sealed class CreateMeetingValidator(WorkingDays workingDays) : MeetingValidation<CreateMeetingCommand>(workingDays);
