using MedApp.Application.Calendar.Common;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Clinics.Responses;
using MediatR;

namespace MedApp.Application.Clinics.Queries.GetClinicPatterns;

public sealed class GetClinicPatternsHandler(
    IClinicRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetClinicPatternsQuery, IReadOnlyList<ClinicPatternResponse>>
{
    public async Task<IReadOnlyList<ClinicPatternResponse>> Handle(GetClinicPatternsQuery request, CancellationToken cancellationToken)
    {
        var patterns = await repository.GetPatternsAsync(cancellationToken);
        var usernames = await identityReadService.GetUsernamesByIdAsync(patterns.Select(p => p.StaffUserId).Distinct(), cancellationToken);
        var today = UkTime.Today(timeProvider);
        return patterns
            .Select(p => ClinicMapper.ToResponse(p, usernames.GetValueOrDefault(p.StaffUserId) ?? string.Empty, today))
            .ToList();
    }
}
