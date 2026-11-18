using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.Repositories;
using MedApp.Contracts.Jobs.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Jobs.Queries.GetScheduledJobs;

public sealed class GetScheduledJobsHandler(
    IEnumerable<IScheduledJob> jobs,
    IScheduledJobRepository repository,
    IOptions<JobsOptions> options,
    TimeProvider timeProvider) : IRequestHandler<GetScheduledJobsQuery, IReadOnlyList<ScheduledJobResponse>>
{
    public async Task<IReadOnlyList<ScheduledJobResponse>> Handle(
        GetScheduledJobsQuery request, CancellationToken cancellationToken)
    {
        var rows = (await repository.GetAllAsync(cancellationToken)).ToDictionary(row => row.Name, StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return jobs
            .Select(job => ScheduledJobMapper.ToResponse(
                job, rows.GetValueOrDefault(job.Name), options.Value.IsJobEnabled(job.Name), now))
            .OrderBy(job => job.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(job => job.Name, StringComparer.Ordinal)
            .ToList();
    }
}
