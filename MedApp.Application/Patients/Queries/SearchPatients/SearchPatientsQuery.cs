using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.SearchPatients;

/// <summary>One page of patients matching the filters. See <see cref="PatientSearch"/> for how they apply.</summary>
public sealed record SearchPatientsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirthFrom,
    DateOnly? DateOfBirthTo)
    : IRequest<PagedResponse<PatientResponse>>;
