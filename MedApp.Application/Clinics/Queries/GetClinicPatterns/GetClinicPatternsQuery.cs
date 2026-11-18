using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Queries.GetClinicPatterns;

/// <summary>Every clinic pattern, by day of the week then start time.</summary>
public sealed record GetClinicPatternsQuery : IRequest<IReadOnlyList<ClinicPatternResponse>>;
