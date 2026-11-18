using MedApp.Contracts.Jobs.Responses;
using MediatR;

namespace MedApp.Application.Jobs.Queries.GetScheduledJobs;

/// <summary>Every scheduled job, with how its last run went, by title.</summary>
public sealed record GetScheduledJobsQuery : IRequest<IReadOnlyList<ScheduledJobResponse>>;
