using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Lab.Requests;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;
using MediatR;

namespace MedApp.Application.Lab.Tests;

// The lab's test catalogue (MC13 of docs/multi-clinic-plan.md; the light demo's labTests.ts): organisation-wide setup,
// kept by holders of ManageSetup and read by everyone. A test a lab case or sample has used is archived, not deleted.

/// <summary>Every lab test by name; archived ones only with <paramref name="IncludeArchived"/>.</summary>
public sealed record GetLabTestsQuery(bool IncludeArchived) : IRequest<IReadOnlyList<LabTestResponse>>;

public sealed record CreateLabTestCommand(LabTestRequest Test) : IRequest<LabTestResponse>;

public sealed record UpdateLabTestCommand(Guid Id, LabTestRequest Test) : IRequest<LabTestResponse>;

public sealed record SetLabTestArchivedCommand(Guid Id, bool IsArchived) : IRequest<LabTestResponse>;

public sealed record DeleteLabTestCommand(Guid Id) : IRequest;

/// <summary>A lab test's fields, checked as the light demo checks them (each field's own words).</summary>
public static partial class LabTestRules
{
    public const string KindMessage = "Choose the kind of test.";
    public const string TargetMessage = "The target must be 1 to 90 days.";
    public const string LimitMessage = "The limit must be 1 to 90 days.";
    public const string LimitBeforeTargetMessage = "The limit can't be sooner than the target.";
    public const string AtRiskMessage = "At risk must be 0 to 30 days before the due date.";
    public const string AtRiskAfterTargetMessage = "At risk must start before the target, so fewer days than it.";
    public const string NoStepsMessage = "Add at least one step.";
    public static readonly string TooManyStepsMessage = $"A test has at most {LabTest.MaxSteps} steps.";
    public const string SnomedMessage = "A SNOMED CT code is 6 to 18 digits.";

    public static string StepNameMessage(int index) =>
        $"Give step {index + 1} a name of at most {LabTestStep.MaxNameLength} characters.";

    public static string StepKindMessage(int index) => $"Choose step {index + 1}'s kind.";

    /// <summary>A SNOMED CT code: optional, 6 to 18 digits (a concept ID), never looked up.</summary>
    public static bool IsSnomedCode(string? value) => string.IsNullOrWhiteSpace(value) || SnomedPattern().IsMatch(value.Trim());

    public static string? SnomedCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>The test's fields' problems, each on its field (<c>steps[0].name</c> for a step's).</summary>
    public static IEnumerable<ValidationFailure> Check(LabTestRequest? test)
    {
        if (test is null)
        {
            yield return new ValidationFailure("Name", "Give it a name.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(test.Name))
            yield return new ValidationFailure("Name", "Give it a name.");
        else if (test.Name.Trim().Length > LabTest.MaxNameLength)
            yield return new ValidationFailure("Name", $"The name must be at most {LabTest.MaxNameLength} characters.");

        if (!PathwayEnums.TryParse<LabTestKind>(test.Kind, out _))
            yield return new ValidationFailure("Kind", KindMessage);

        if (test.TargetDays is < LabTest.MinDays or > LabTest.MaxDays)
            yield return new ValidationFailure("TargetDays", TargetMessage);
        if (test.LimitDays is < LabTest.MinDays or > LabTest.MaxDays)
            yield return new ValidationFailure("LimitDays", LimitMessage);
        else if (test.LimitDays < test.TargetDays)
            yield return new ValidationFailure("LimitDays", LimitBeforeTargetMessage);
        if (test.AtRiskDays is < 0 or > LabTest.MaxAtRiskDays)
            yield return new ValidationFailure("AtRiskDays", AtRiskMessage);
        else if (test.AtRiskDays >= test.TargetDays)
            yield return new ValidationFailure("AtRiskDays", AtRiskAfterTargetMessage);

        var steps = test.Steps ?? [];
        if (steps.Count == 0)
            yield return new ValidationFailure("Steps", NoStepsMessage);
        if (steps.Count > LabTest.MaxSteps)
            yield return new ValidationFailure("Steps", TooManyStepsMessage);
        for (var i = 0; i < steps.Count; i++)
        {
            var name = steps[i]?.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || name.Length > LabTestStep.MaxNameLength)
                yield return new ValidationFailure($"Steps[{i}].Name", StepNameMessage(i));
            if (!PathwayEnums.TryParse<LabStepKind>(steps[i]?.Kind, out _))
                yield return new ValidationFailure($"Steps[{i}].Kind", StepKindMessage(i));
        }

        if (!IsSnomedCode(test.SnomedCode))
            yield return new ValidationFailure("SnomedCode", SnomedMessage);
    }

    [GeneratedRegex(@"^\d{6,18}$")]
    private static partial Regex SnomedPattern();
}

public sealed class CreateLabTestValidator : AbstractValidator<CreateLabTestCommand>
{
    public CreateLabTestValidator() =>
        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (var failure in LabTestRules.Check(command.Test))
                context.AddFailure(failure);
        });
}

public sealed class UpdateLabTestValidator : AbstractValidator<UpdateLabTestCommand>
{
    public UpdateLabTestValidator() =>
        RuleFor(x => x).Custom((command, context) =>
        {
            foreach (var failure in LabTestRules.Check(command.Test))
                context.AddFailure(failure);
        });
}

/// <summary>Maps lab tests to their responses.</summary>
public static class LabTestMapper
{
    public static LabTestResponse ToResponse(LabTest test, bool inUse) => new()
    {
        Id = test.Id,
        Name = test.Name,
        Kind = test.Kind.ToString(),
        SnomedCode = test.SnomedCode,
        TargetDays = test.TargetDays,
        LimitDays = test.LimitDays,
        AtRiskDays = test.AtRiskDays,
        Steps = test.Steps
            .OrderBy(s => s.Position)
            .Select(s => new LabTestStepResponse { Id = s.Id, Name = s.Name, Kind = s.Kind.ToString(), IsOptional = s.IsOptional })
            .ToList(),
        IsArchived = test.IsArchived,
        CanBeDeleted = !inUse,
    };
}

public sealed class LabTestHandlers(ILabTestRepository repository, TimeProvider timeProvider) :
    IRequestHandler<GetLabTestsQuery, IReadOnlyList<LabTestResponse>>,
    IRequestHandler<CreateLabTestCommand, LabTestResponse>,
    IRequestHandler<UpdateLabTestCommand, LabTestResponse>,
    IRequestHandler<SetLabTestArchivedCommand, LabTestResponse>,
    IRequestHandler<DeleteLabTestCommand>
{
    public static string NameTaken(string name) => $"There's already a lab test called '{name}'.";

    public static string InUse(LabTest test) => $"Lab cases have used '{test.Name}', so it can't be deleted. Archive it instead.";

    public static NotFoundException NotFound(Guid id) => new($"Lab test '{id}' was not found.");

    public async Task<IReadOnlyList<LabTestResponse>> Handle(GetLabTestsQuery request, CancellationToken cancellationToken)
    {
        var tests = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetInUseAsync(tests.Select(t => t.Id).ToList(), cancellationToken);
        return tests.Select(t => LabTestMapper.ToResponse(t, inUse.Contains(t.Id))).ToList();
    }

    public async Task<LabTestResponse> Handle(CreateLabTestCommand request, CancellationToken cancellationToken)
    {
        var name = request.Test.Name.Trim();
        if (await repository.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(NameTaken(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var test = new LabTest { Id = Guid.NewGuid(), CreatedAt = now };
        Apply(test, request.Test, now);
        await repository.AddAsync(test, cancellationToken);
        return LabTestMapper.ToResponse(test, inUse: false);
    }

    public async Task<LabTestResponse> Handle(UpdateLabTestCommand request, CancellationToken cancellationToken)
    {
        var test = await GetOrThrowAsync(request.Id, cancellationToken);
        var name = request.Test.Name.Trim();
        if (await repository.NameExistsAsync(name, test.Id, cancellationToken))
            throw new ConflictException(NameTaken(name));

        var (added, removed) = Apply(test, request.Test, timeProvider.GetUtcNow().UtcDateTime);
        await repository.UpdateAsync(test, added, removed, cancellationToken);
        return await ResponseAsync(test, cancellationToken);
    }

    public async Task<LabTestResponse> Handle(SetLabTestArchivedCommand request, CancellationToken cancellationToken)
    {
        var test = await GetOrThrowAsync(request.Id, cancellationToken);
        if (test.IsArchived != request.IsArchived)
        {
            test.IsArchived = request.IsArchived;
            test.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateAsync(test, [], [], cancellationToken);
        }

        return await ResponseAsync(test, cancellationToken);
    }

    public async Task Handle(DeleteLabTestCommand request, CancellationToken cancellationToken)
    {
        var test = await GetOrThrowAsync(request.Id, cancellationToken);
        if ((await repository.GetInUseAsync([test.Id], cancellationToken)).Contains(test.Id))
            throw new ConflictException(InUse(test));
        await repository.DeleteAsync(test.Id, cancellationToken);
    }

    /// <summary>
    /// Sets the test's fields, and its steps by position: a step kept in its place keeps its ID (and so the cases that
    /// recorded it), new places get new steps, and places no longer there are removed.
    /// </summary>
    private static (List<LabTestStep> Added, List<LabTestStep> Removed) Apply(LabTest test, LabTestRequest request, DateTime nowUtc)
    {
        test.Name = request.Name.Trim();
        test.Kind = PathwayEnums.Parse<LabTestKind>(request.Kind);
        test.SnomedCode = LabTestRules.SnomedCode(request.SnomedCode);
        test.TargetDays = request.TargetDays;
        test.LimitDays = request.LimitDays;
        test.AtRiskDays = request.AtRiskDays;
        test.LastUpdated = nowUtc;

        var existing = test.Steps.OrderBy(s => s.Position).ToList();
        var added = new List<LabTestStep>();
        for (var i = 0; i < request.Steps.Count; i++)
        {
            var wanted = request.Steps[i];
            var kind = PathwayEnums.Parse<LabStepKind>(wanted.Kind);
            if (i < existing.Count)
            {
                existing[i].Name = wanted.Name.Trim();
                existing[i].Kind = kind;
                existing[i].IsOptional = wanted.IsOptional;
                existing[i].Position = i;
                continue;
            }

            var step = new LabTestStep
            {
                Id = Guid.NewGuid(),
                LabTestId = test.Id,
                Position = i,
                Name = wanted.Name.Trim(),
                Kind = kind,
                IsOptional = wanted.IsOptional,
            };
            added.Add(step);
        }

        var removed = existing.Skip(request.Steps.Count).ToList();
        foreach (var step in removed)
            test.Steps.Remove(step);
        test.Steps.AddRange(added);
        return (added, removed);
    }

    private async Task<LabTest> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw NotFound(id);

    private async Task<LabTestResponse> ResponseAsync(LabTest test, CancellationToken ct) =>
        LabTestMapper.ToResponse(test, (await repository.GetInUseAsync([test.Id], ct)).Contains(test.Id));
}
