using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;
using MedApp.Application.Patients.Common;

namespace MedApp.Application.Patients.Queries.SearchPatients;

public sealed class SearchPatientsHandler(IPatientRepository repository)
    : IRequestHandler<SearchPatientsQuery, PagedResponse<PatientResponse>>
{
    public async Task<PagedResponse<PatientResponse>> Handle(SearchPatientsQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.SearchAsync(request, cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize, PatientMapper.ToResponse);
    }
}
