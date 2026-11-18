using MedApp.Contracts.Jobs.Responses;
using MediatR;

namespace MedApp.Application.Jobs.Commands.RunScheduledJob;

/// <summary>Runs a scheduled job now, due or not, and returns it afterwards (Run now).</summary>
public sealed record RunScheduledJobCommand(string Name) : IRequest<ScheduledJobResponse>;
