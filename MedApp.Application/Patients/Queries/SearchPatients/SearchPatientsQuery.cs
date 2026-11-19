using MedApp.Application.Common.Teams;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.SearchPatients;

/// <summary>
/// One page of patients matching the filters. See <see cref="PatientSearch"/> for how they apply. Patients are
/// centre-wide (decision 183): the search has no team wall. <paramref name="TeamId"/> is the clinic picker (MC6): only
/// patients with a pathway, referral or appointment in that team; anyone but an admin (<paramref name="Scope"/>) may
/// pick only one of their own (400).
/// </summary>
public sealed record SearchPatientsQuery(
    int Page,
    int PageSize,
    string? Sort,
    bool Ascending,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly? DateOfBirthFrom,
    DateOnly? DateOfBirthTo,
    string? NhsNumber = null,
    Guid? TeamId = null,
    TeamScope? Scope = null,
    bool HideHospitalOnly = false)
    : IRequest<PagedResponse<PatientResponse>>
{
    /// <summary>The viewer's team walls (the system's outside a request): here only who may pick which team.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
