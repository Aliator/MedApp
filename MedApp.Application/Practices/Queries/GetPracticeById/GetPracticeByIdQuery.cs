using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Queries.GetPracticeById;

public sealed record GetPracticeByIdQuery(Guid PracticeId) : IRequest<PracticeResponse>;
