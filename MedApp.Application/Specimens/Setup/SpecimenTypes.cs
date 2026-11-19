using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Specimens.Setup;

// Specimen types: organisation-wide setup, kept by holders of ManageSetup and read by everyone. One a specimen has is
// archived, not deleted.

/// <summary>Every specimen type by name; archived ones only with <paramref name="IncludeArchived"/>.</summary>
public sealed record GetSpecimenTypesQuery(bool IncludeArchived) : IRequest<IReadOnlyList<SpecimenTypeResponse>>;

public sealed record CreateSpecimenTypeCommand(string Name, int ExpectedTurnaroundDays, string? SnomedCode = null)
    : IRequest<SpecimenTypeResponse>;

public sealed record UpdateSpecimenTypeCommand(Guid Id, string Name, int ExpectedTurnaroundDays, string? SnomedCode = null)
    : IRequest<SpecimenTypeResponse>;

public sealed record SetSpecimenTypeArchivedCommand(Guid Id, bool IsArchived) : IRequest<SpecimenTypeResponse>;

public sealed record DeleteSpecimenTypeCommand(Guid Id) : IRequest;

public sealed class CreateSpecimenTypeValidator : AbstractValidator<CreateSpecimenTypeCommand>
{
    public CreateSpecimenTypeValidator()
    {
        RuleFor(x => x.Name).SetupName(SpecimenType.MaxNameLength);
        RuleFor(x => x.ExpectedTurnaroundDays).Turnaround();
        RuleFor(x => x.SnomedCode).SnomedCode();
    }
}

public sealed class UpdateSpecimenTypeValidator : AbstractValidator<UpdateSpecimenTypeCommand>
{
    public UpdateSpecimenTypeValidator()
    {
        RuleFor(x => x.Name).SetupName(SpecimenType.MaxNameLength);
        RuleFor(x => x.ExpectedTurnaroundDays).Turnaround();
        RuleFor(x => x.SnomedCode).SnomedCode();
    }
}

public sealed class SpecimenTypeHandlers(ISpecimenRepository repository, TimeProvider timeProvider) :
    IRequestHandler<GetSpecimenTypesQuery, IReadOnlyList<SpecimenTypeResponse>>,
    IRequestHandler<CreateSpecimenTypeCommand, SpecimenTypeResponse>,
    IRequestHandler<UpdateSpecimenTypeCommand, SpecimenTypeResponse>,
    IRequestHandler<SetSpecimenTypeArchivedCommand, SpecimenTypeResponse>,
    IRequestHandler<DeleteSpecimenTypeCommand>
{
    public static string NameTaken(string name) => $"There's already a sample type called '{name}'.";

    public static string InUse(SpecimenType type) =>
        $"Samples of type '{type.Name}' have been recorded, so it can't be deleted. Archive it instead.";

    public async Task<IReadOnlyList<SpecimenTypeResponse>> Handle(GetSpecimenTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await repository.GetTypesAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetTypesInUseAsync(types.Select(t => t.Id).ToList(), cancellationToken);
        return types.Select(t => SpecimenMapper.ToResponse(t, inUse.Contains(t.Id))).ToList();
    }

    public async Task<SpecimenTypeResponse> Handle(CreateSpecimenTypeCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.TypeNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(NameTaken(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var type = new SpecimenType
        {
            Id = Guid.NewGuid(),
            Name = name,
            ExpectedTurnaroundDays = request.ExpectedTurnaroundDays,
            SnomedCode = Lab.Tests.LabTestRules.SnomedCode(request.SnomedCode),
            CreatedAt = now,
            LastUpdated = now,
        };
        await repository.AddTypeAsync(type, cancellationToken);
        return SpecimenMapper.ToResponse(type, inUse: false);
    }

    public async Task<SpecimenTypeResponse> Handle(UpdateSpecimenTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await GetOrThrowAsync(request.Id, cancellationToken);
        var name = request.Name.Trim();
        if (await repository.TypeNameExistsAsync(name, type.Id, cancellationToken))
            throw new ConflictException(NameTaken(name));

        type.Name = name;
        type.ExpectedTurnaroundDays = request.ExpectedTurnaroundDays;
        type.SnomedCode = Lab.Tests.LabTestRules.SnomedCode(request.SnomedCode);
        type.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateTypeAsync(type, cancellationToken);
        return await ResponseAsync(type, cancellationToken);
    }

    public async Task<SpecimenTypeResponse> Handle(SetSpecimenTypeArchivedCommand request, CancellationToken cancellationToken)
    {
        var type = await GetOrThrowAsync(request.Id, cancellationToken);
        if (type.IsArchived != request.IsArchived)
        {
            type.IsArchived = request.IsArchived;
            type.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateTypeAsync(type, cancellationToken);
        }

        return await ResponseAsync(type, cancellationToken);
    }

    public async Task Handle(DeleteSpecimenTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await GetOrThrowAsync(request.Id, cancellationToken);
        if ((await repository.GetTypesInUseAsync([type.Id], cancellationToken)).Contains(type.Id))
            throw new ConflictException(InUse(type));
        await repository.DeleteTypeAsync(type.Id, cancellationToken);
    }

    private async Task<SpecimenType> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await repository.GetTypeAsync(id, ct) ?? throw new NotFoundException($"Sample type '{id}' was not found.");

    private async Task<SpecimenTypeResponse> ResponseAsync(SpecimenType type, CancellationToken ct) =>
        SpecimenMapper.ToResponse(type, (await repository.GetTypesInUseAsync([type.Id], ct)).Contains(type.Id));
}

/// <summary>Validation rules for the setup lists.</summary>
public static class SpecimenSetupRules
{
    public static IRuleBuilderOptions<T, string> SetupName<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Give it a name.")
            .Must(n => n is null || n.Trim().Length <= maxLength).WithMessage($"The name must be at most {maxLength} characters.");

    public static IRuleBuilderOptions<T, int> Turnaround<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(SpecimenType.MinTurnaroundDays, SpecimenType.MaxTurnaroundDays)
            .WithMessage($"The expected turnaround must be {SpecimenType.MinTurnaroundDays} to {SpecimenType.MaxTurnaroundDays} days.");

    /// <summary>A SNOMED CT code (MC13): optional, 6 to 18 digits, never looked up.</summary>
    public static IRuleBuilderOptions<T, string?> SnomedCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Lab.Tests.LabTestRules.IsSnomedCode).WithMessage(Lab.Tests.LabTestRules.SnomedMessage);
}
