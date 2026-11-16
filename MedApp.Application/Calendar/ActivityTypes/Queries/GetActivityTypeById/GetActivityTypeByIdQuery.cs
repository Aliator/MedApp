using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;

public sealed record GetActivityTypeByIdQuery(Guid ActivityTypeId) : IRequest<ActivityTypeResponse>;
