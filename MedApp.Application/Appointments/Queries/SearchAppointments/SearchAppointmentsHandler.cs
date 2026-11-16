using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Common;
using MediatR;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

public sealed class SearchAppointmentsHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SearchAppointmentsQuery, PagedResponse<AppointmentResponse>>
{
    public async Task<PagedResponse<AppointmentResponse>> Handle(SearchAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var result = await repository.SearchAsync(request, cancellationToken);
        var items = await AppointmentMapper.ToResponsesAsync(result.Items, identityReadService, now, cancellationToken);

        return PagedMapper.ToResponse(new PagedResult<AppointmentResponse>(items, result.TotalCount), request.Page, request.PageSize);
    }
}
