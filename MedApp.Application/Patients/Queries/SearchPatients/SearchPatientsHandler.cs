using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.SearchPatients;

public sealed class SearchPatientsHandler(IPatientRepository repository)
    : IRequestHandler<SearchPatientsQuery, PagedResponse<PatientResponse>>
{
    public async Task<PagedResponse<PatientResponse>> Handle(SearchPatientsQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        var items = result.Items
            .Select(p => new PatientResponse(p.Id, p.FirstName, p.LastName, p.DateOfBirth, p.Email))
            .ToList();

        return new PagedResponse<PatientResponse>(
            items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            Paging.TotalPages(result.TotalCount, request.PageSize));
    }
}
