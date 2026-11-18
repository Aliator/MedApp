using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Queries.GetMdtMeetings;

public sealed class GetMdtMeetingsHandler(
    IMdtItemRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetMdtMeetingsQuery, IReadOnlyList<MdtMeetingResponse>>
{
    public const int DaysBack = 60;
    public const int DaysAhead = 180;

    public async Task<IReadOnlyList<MdtMeetingResponse>> Handle(GetMdtMeetingsQuery request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rows = await repository.GetMeetingsAsync(
            request.SpecialtyId, now.AddDays(-DaysBack), now.AddDays(DaysAhead), cancellationToken);
        var usernames = await identityReadService.GetUsernamesByIdAsync(
            rows.SelectMany(r => r.Meeting.Attendees.Select(a => a.StaffUserId)).Distinct(), cancellationToken);
        return rows.Select(r => MdtMapper.ToResponse(r, usernames)).ToList();
    }
}
