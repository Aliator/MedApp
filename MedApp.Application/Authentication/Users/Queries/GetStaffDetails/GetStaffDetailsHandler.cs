using MedApp.Application.Common.Exceptions;
using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Queries.GetStaffDetails;

/// <summary>Reads someone's full name and job title; an unknown account is a 404. Staff data, so nothing is logged.</summary>
public sealed class GetStaffDetailsHandler(IIdentityReadService readService, IStaffDetailsService staffDetails)
    : IRequestHandler<GetStaffDetailsQuery, StaffDetailsResponse>
{
    public async Task<StaffDetailsResponse> Handle(GetStaffDetailsQuery request, CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");
        var details = await staffDetails.GetAsync(user.UserId, cancellationToken)
                      ?? throw new NotFoundException($"User '{request.Username}' was not found.");
        return StaffDetailsMapper.ToResponse(details);
    }
}
