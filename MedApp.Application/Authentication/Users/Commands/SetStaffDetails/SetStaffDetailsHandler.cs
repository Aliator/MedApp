using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Authentication.Users.Common;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetStaffDetails;

/// <summary>
/// Sets someone's full name and job title, tidied (trimmed, runs of spaces made one). A change is recorded in the
/// activity log (<see cref="ActivityAction.StaffDetailsChanged"/>, with the names of the fields changed, never their
/// values) in the same save; setting what's there already records nothing. An unknown account is a 404.
/// </summary>
public sealed class SetStaffDetailsHandler(
    IIdentityReadService readService,
    IStaffDetailsService staffDetails,
    IActivityLog activityLog)
    : IRequestHandler<SetStaffDetailsCommand, StaffDetailsResponse>
{
    public async Task<StaffDetailsResponse> Handle(SetStaffDetailsCommand request, CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");
        var before = await staffDetails.GetAsync(user.UserId, cancellationToken)
                     ?? throw new NotFoundException($"User '{request.Username}' was not found.");

        var after = new StaffDetails(StaffDetailsRules.Tidy(request.FullName), StaffDetailsRules.Tidy(request.JobTitle));

        var fields = new List<string>();
        if (after.FullName != before.FullName)
            fields.Add(StaffDetailsRules.FullNameField);
        if (after.JobTitle != before.JobTitle)
            fields.Add(StaffDetailsRules.JobTitleField);

        if (fields.Count > 0)
        {
            var entry = activityLog.Entry(
                ActivityAction.StaffDetailsChanged,
                targetUsername: user.Username,
                details: new { fields });
            if (!await staffDetails.SetAsync(user.UserId, after, entry, cancellationToken))
                throw new NotFoundException($"User '{request.Username}' was not found.");
        }

        return StaffDetailsMapper.ToResponse(after);
    }
}
