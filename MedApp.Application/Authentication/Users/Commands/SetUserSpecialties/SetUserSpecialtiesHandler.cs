using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.SetUserSpecialties;

/// <summary>
/// Sets which specialties someone belongs to. Every specialty must exist (404), and an archived one can only be kept,
/// not newly given (409). A change is recorded in the activity log (<see cref="ActivityAction.StaffSpecialtiesChanged"/>,
/// with the specialties before and after) in the same save; setting what they already have records nothing.
/// </summary>
public sealed class SetUserSpecialtiesHandler(
    IIdentityReadService readService,
    ISpecialtyRepository repository,
    IActivityLog activityLog)
    : IRequestHandler<SetUserSpecialtiesCommand, IReadOnlyList<StaffSpecialtyResponse>>
{
    public async Task<IReadOnlyList<StaffSpecialtyResponse>> Handle(
        SetUserSpecialtiesCommand request,
        CancellationToken cancellationToken)
    {
        var user = await readService.GetUserAsync(request.Username, cancellationToken)
                   ?? throw new NotFoundException($"User '{request.Username}' was not found.");

        var before = await repository.GetForUserAsync(user.UserId, cancellationToken);
        var beforeIds = before.Select(s => s.Id).ToHashSet();

        var chosen = await SpecialtyRules.CheckChosenAsync(repository, request.SpecialtyIds, beforeIds, cancellationToken);
        if (beforeIds.SetEquals(chosen.Keys))
            return SpecialtyMapper.ToStaffResponses(before);

        var after = SpecialtyMapper.ToStaffResponses(chosen.Values);
        var from = SpecialtyMapper.ToStaffResponses(before);
        var entry = activityLog.Entry(
            ActivityAction.StaffSpecialtiesChanged,
            targetUsername: user.Username,
            details: new
            {
                from = from.Select(s => s.Name).ToList(),
                to = after.Select(s => s.Name).ToList(),
                fromIds = from.Select(s => s.Id).ToList(),
                toIds = after.Select(s => s.Id).ToList()
            });

        await repository.SetForUserAsync(user.UserId, chosen.Keys.ToList(), entry, cancellationToken);

        return after;
    }
}
