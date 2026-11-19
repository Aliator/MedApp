using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Lab.Cases.Commands;

// A lab case's steps and outcomes (MC15 of docs/multi-clinic-plan.md; the light demo's labCases.ts): only the lab
// team works a case (ProcessSpecimens), from the version shown, and only what's next. The lab member records each
// outcome; MedApp never chooses or suggests one (decision 191).

/// <summary>The next step: one of the test's (<paramref name="TestStepId"/>) or the fixed <c>Accessioned</c>.</summary>
public sealed record RecordLabStepCommand(
    Guid LabCaseId, Guid? TestStepId, string? FixedStep, DateTime? AtUtc, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

/// <summary>A choice's outcome, as the lab member recorded it.</summary>
public sealed record RecordLabOutcomeCommand(
    Guid LabCaseId, string? Choice, string? Outcome, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

/// <summary>The latest step marked entered in error, by whoever recorded it.</summary>
public sealed record MarkLabStepEnteredInErrorCommand(Guid LabCaseId, Guid StepId, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

public sealed class RecordLabStepValidator : AbstractValidator<RecordLabStepCommand>
{
    public RecordLabStepValidator()
    {
        RuleFor(x => x.Detail)
            .Must(d => d is null || d.Trim().Length <= LabCase.MaxWhereLength)
            .WithMessage($"The detail must be at most {LabCase.MaxWhereLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }
}

public sealed class RecordLabOutcomeValidator : AbstractValidator<RecordLabOutcomeCommand>
{
    public RecordLabOutcomeValidator()
    {
        RuleFor(x => x.Detail)
            .Must(d => d is null || d.Trim().Length <= LabCase.MaxDetailLength)
            .WithMessage($"The detail must be at most {LabCase.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }
}

public sealed class MarkLabStepEnteredInErrorValidator : AbstractValidator<MarkLabStepEnteredInErrorCommand>
{
    public MarkLabStepEnteredInErrorValidator()
    {
        RuleFor(x => x.Detail).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage(LabCaseRules.WhatWasWrongMessage)
            .Must(d => d is null || d.Trim().Length <= LabCase.MaxDetailLength)
            .WithMessage($"The detail must be at most {LabCase.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }
}

/// <summary>What lab case validators share.</summary>
public static class LabCaseValidation
{
    public const string VersionMessage = "Give the version of the lab case you changed.";

    public static ValidationException Invalid(string field, string message) => new([new ValidationFailure(field, message)]);
}

/// <summary>
/// Records the case's next step: 409 unless it's the step asked now (<see cref="LabCaseRules.NotNext"/>); a second
/// opinion's step says where it went (400 without); its time is now or typed up to 7 days back, never before the step
/// before. Audited (<c>LabStepRecorded</c>, never the detail).
/// </summary>
public sealed class RecordLabStepHandler(LabCaseAccess cases, ILabCaseRepository repository, TimeProvider timeProvider)
    : IRequestHandler<RecordLabStepCommand, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(RecordLabStepCommand request, CancellationToken cancellationToken)
    {
        var (labCase, labTeamId) = await cases.ForWorkAsync(request.LabCaseId, request.ExpectedVersion, request.Actor, cancellationToken);
        var next = LabCaseRules.Next(labCase);
        if (next?.Kind != LabNextKind.Step)
            throw new ConflictException(LabCaseRules.NotNext(next));

        var isNext = next.TestStep is { } testStep
            ? request.TestStepId == testStep.Id
            : request.TestStepId is null && string.Equals(request.FixedStep?.Trim(), LabCaseRules.AccessionedName, StringComparison.OrdinalIgnoreCase);
        if (!isNext)
            throw new ConflictException(LabCaseRules.NotNext(next));

        var detail = LabCaseRules.Optional(request.Detail);
        if (next.TestStep?.Kind == LabStepKind.SentForSecondOpinion && detail is null)
            throw LabCaseValidation.Invalid(nameof(request.Detail), LabCaseRules.SecondOpinionWhereMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var at = SpecimenRules.StepTime(request.AtUtc, LabCaseRules.LatestLive(labCase)?.AtUtc, now, "done", nameof(request.AtUtc));

        var change = new LabCaseChange(labCase, labCase.Pathway, request.Actor, now);
        change.RecordStep(next.TestStep, detail, at);
        repository.Track(change, isNew: false);
        await repository.SaveAsync(cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}

/// <summary>
/// Records a choice's outcome: 409 unless it's the choice asked now; 400 for an outcome the choice (and the test) doesn't
/// offer. <b>Insufficient</b> needs why (400 without): the case closes as <c>RepeatRequested</c>, its received samples
/// are rejected (insufficient), the stage that took the sample reopens for the repeat (MC16: <c>ReopenForRepeatSample</c>;
/// the lab stage stays with the lab), and the clinic is told (<c>RepeatSampleRequested</c>), all in one save.
/// </summary>
public sealed class RecordLabOutcomeHandler(
    LabCaseAccess cases,
    ILabCaseRepository repository,
    IPatientPathwayRepository pathways,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<RecordLabOutcomeCommand, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(RecordLabOutcomeCommand request, CancellationToken cancellationToken)
    {
        var (labCase, labTeamId) = await cases.ForWorkAsync(request.LabCaseId, request.ExpectedVersion, request.Actor, cancellationToken);
        var next = LabCaseRules.Next(labCase);
        if (next?.Kind != LabNextKind.Choice
            || !PathwayEnums.TryParse<LabChoice>(request.Choice, out var choice)
            || choice != next.Choice)
            throw new ConflictException(LabCaseRules.NotNext(next));

        var offered = next.Outcomes ?? [];
        if (!PathwayEnums.TryParse<LabOutcome>(request.Outcome, out var outcome) || !offered.Contains(outcome))
            throw LabCaseValidation.Invalid(
                nameof(request.Outcome), $"Choose the outcome: {string.Join(", ", offered.Select(LabCaseRules.Describe))}.");

        var detail = outcome == LabOutcome.Insufficient ? LabCaseRules.Optional(request.Detail) : null;
        if (outcome == LabOutcome.Insufficient && detail is null)
            throw LabCaseValidation.Invalid(nameof(request.Detail), LabCaseRules.InsufficientDetailMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        // The pathway with its links (tracked: the same instance the case holds), for reopening the sample's stage.
        var pathway = labCase.PathwayId is { } pathwayId ? await pathways.GetForUpdateAsync(pathwayId, cancellationToken) : null;
        var change = new LabCaseChange(labCase, pathway, request.Actor, now);
        change.RecordOutcome(choice, outcome, detail);
        repository.Track(change, isNew: false);

        PathwayChange? pathwayChange = null;
        IReadOnlyList<Domain.Notifications.Notification> toTell = [];
        if (outcome == LabOutcome.Insufficient)
        {
            var samples = labCase.Specimens.OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Number).ToList();
            foreach (var sample in samples.Where(s => s.Status == SpecimenStatus.Received))
            {
                var rejected = new SpecimenChange(sample, pathway, request.Actor, now.AddTicks(1));
                rejected.Reject(SpecimenRejectionReason.InsufficientSample, detail);
                repository.Track(null, rejected.AuditEntries);
            }

            // On a pathway, the stage that took the sample reopens for the repeat (MC16); the lab stage stays the lab's.
            var first = samples.FirstOrDefault();
            string? reopened = null;
            if (labCase.Source == SpecimenSource.Clinic && first is not null && pathway is not null
                && pathway.Stages.FirstOrDefault(s => s.Id == first.StageId) is { } sampleStage)
            {
                pathwayChange = new PathwayChange(pathway, request.Actor, now.AddTicks(2));
                reopened = pathwayChange.ReopenForRepeatSample(sampleStage, labTeamId);
            }

            // The clinic is told, with no patient details or the detail typed (the hospital's comes with MC20).
            if (labCase.Source == SpecimenSource.Clinic && first is not null)
                toTell = LabCaseNotifications.RepeatRequested(labCase, first, reopened, request.Actor, now);
        }

        if (pathwayChange is { HasChanges: true })
            await pathways.SaveAsync(pathwayChange, cancellationToken);
        else
            await repository.SaveAsync(cancellationToken);

        foreach (var notification in toTell)
            await notifications.SendAsync(notification, cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}

/// <summary>
/// Marks the latest step entered in error (kept, struck through): 404 for an unknown step; 409 unless it's the latest
/// recorded (never Received); 403 unless the caller recorded it. Audited (<c>LabStepEnteredInError</c>, never the detail).
/// </summary>
public sealed class MarkLabStepEnteredInErrorHandler(LabCaseAccess cases, ILabCaseRepository repository, TimeProvider timeProvider)
    : IRequestHandler<MarkLabStepEnteredInErrorCommand, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(MarkLabStepEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var (labCase, labTeamId) = await cases.ForWorkAsync(request.LabCaseId, request.ExpectedVersion, request.Actor, cancellationToken);
        var step = labCase.Steps.FirstOrDefault(s => s.Id == request.StepId)
                   ?? throw new NotFoundException($"Step '{request.StepId}' was not found.");
        if (step.Kind == LabCaseStepKind.Received || step.IsEnteredInError || LabCaseRules.LatestLive(labCase)?.Id != step.Id)
            throw new ConflictException(LabCaseRules.LatestStepOnlyMessage);
        if (step.ByUserId != request.Actor.UserId)
            throw new ForbiddenException(LabCaseRules.OwnStepOnlyMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LabCaseChange(labCase, labCase.Pathway, request.Actor, now);
        change.MarkStepEnteredInError(step, request.Detail!.Trim());
        repository.Track(change, isNew: false);
        await repository.SaveAsync(cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}
