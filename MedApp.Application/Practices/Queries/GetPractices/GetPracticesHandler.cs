using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Queries.GetPractices;

/// <summary>The directory. It names organisations and counts, never patients, so nothing is logged.</summary>
public sealed class GetPracticesHandler(IPracticeRepository repository)
    : IRequestHandler<GetPracticesQuery, IReadOnlyList<PracticeResponse>>
{
    public async Task<IReadOnlyList<PracticeResponse>> Handle(GetPracticesQuery request, CancellationToken cancellationToken)
    {
        var practices = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var usage = await repository.GetUsageAsync(practices.Select(p => p.Id), cancellationToken);

        return practices
            .Select(p => PracticeMapper.ToResponse(p, usage.GetValueOrDefault(p.Id, PracticeUsage.None)))
            .ToList();
    }
}
