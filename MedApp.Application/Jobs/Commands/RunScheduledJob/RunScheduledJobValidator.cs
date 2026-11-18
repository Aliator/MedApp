using FluentValidation;
using MedApp.Domain.Jobs;

namespace MedApp.Application.Jobs.Commands.RunScheduledJob;

public sealed class RunScheduledJobValidator : AbstractValidator<RunScheduledJobCommand>
{
    public RunScheduledJobValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ScheduledJob.MaxNameLength);
    }
}
