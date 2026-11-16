using MedApp.Contracts.Demographics.Responses;
using MediatR;

namespace MedApp.Application.Demographics.Queries.LookUpDemographics;

/// <summary>Looks someone up by NHS number (spaces allowed), to fill in a patient's name and date of birth.</summary>
public sealed record LookUpDemographicsQuery(string NhsNumber) : IRequest<DemographicsLookupResponse>;
