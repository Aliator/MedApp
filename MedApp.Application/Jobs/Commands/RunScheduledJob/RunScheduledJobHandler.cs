using MedApp.Application.Jobs.Common;
using MedApp.Contracts.Jobs.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Jobs.Commands.RunScheduledJob;

/// <summary>
/// Runs the job in this request, through the runner, so it takes the job's lease as a scheduled run does: a 409 when
/// another copy is running it, or it's switched off; a 404 for an unknown job. A run that fails still answers 200, with
/// the failure recorded on the job.
/// </summary>
public sealed class RunScheduledJobHandler(
    IScheduledJobRunner runner,
    IEnumerable<IScheduledJob> jobs,
    IOptions<JobsOptions> options,
    TimeProvider timeProvider) : IRequestHandler<RunScheduledJobCommand, ScheduledJobResponse>
{
    public async Task<ScheduledJobResponse> Handle(RunScheduledJobCommand request, CancellationToken cancellationToken)
    {
        var row = await runner.RunNowAsync(request.Name, cancellationToken);
        var job = jobs.Single(job => job.Name == row.Name);
        return ScheduledJobMapper.ToResponse(
            job, row, options.Value.IsJobEnabled(job.Name), timeProvider.GetUtcNow().UtcDateTime);
    }
}
