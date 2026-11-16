using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetStaffWeekTemplate;

/// <summary>A member of staff's default week template now.</summary>
public sealed record GetStaffWeekTemplateQuery(Guid StaffUserId) : IRequest<StaffWeekTemplateResponse>;
