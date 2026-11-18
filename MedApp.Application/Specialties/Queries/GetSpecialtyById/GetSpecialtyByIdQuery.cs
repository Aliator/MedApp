using MedApp.Contracts.Specialties.Responses;
using MediatR;

namespace MedApp.Application.Specialties.Queries.GetSpecialtyById;

public sealed record GetSpecialtyByIdQuery(Guid SpecialtyId) : IRequest<SpecialtyResponse>;
