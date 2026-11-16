using MedApp.Contracts.Demographics.Responses;
using MediatR;

namespace MedApp.Application.Demographics.Queries.GetDemographicsStatus;

/// <summary>Whether a demographics lookup is set up, so the patient forms know whether to offer one.</summary>
public sealed record GetDemographicsStatusQuery : IRequest<DemographicsStatusResponse>;
