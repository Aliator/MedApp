using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Specimens.Setup;

// Labs: organisation-wide setup, kept by holders of ManageSetup and read by everyone. One a specimen was sent to is
// archived, not deleted.

/// <summary>Every lab by name; archived ones only with <paramref name="IncludeArchived"/>.</summary>
public sealed record GetLaboratoriesQuery(bool IncludeArchived) : IRequest<IReadOnlyList<LaboratoryResponse>>;

public sealed record CreateLaboratoryCommand(string Name, bool UsesMedApp) : IRequest<LaboratoryResponse>;

public sealed record UpdateLaboratoryCommand(Guid Id, string Name, bool UsesMedApp) : IRequest<LaboratoryResponse>;

public sealed record SetLaboratoryArchivedCommand(Guid Id, bool IsArchived) : IRequest<LaboratoryResponse>;

public sealed record DeleteLaboratoryCommand(Guid Id) : IRequest;

public sealed class CreateLaboratoryValidator : AbstractValidator<CreateLaboratoryCommand>
{
    public CreateLaboratoryValidator() => RuleFor(x => x.Name).SetupName(Laboratory.MaxNameLength);
}

public sealed class UpdateLaboratoryValidator : AbstractValidator<UpdateLaboratoryCommand>
{
    public UpdateLaboratoryValidator() => RuleFor(x => x.Name).SetupName(Laboratory.MaxNameLength);
}

public sealed class LaboratoryHandlers(ISpecimenRepository repository, TimeProvider timeProvider) :
    IRequestHandler<GetLaboratoriesQuery, IReadOnlyList<LaboratoryResponse>>,
    IRequestHandler<CreateLaboratoryCommand, LaboratoryResponse>,
    IRequestHandler<UpdateLaboratoryCommand, LaboratoryResponse>,
    IRequestHandler<SetLaboratoryArchivedCommand, LaboratoryResponse>,
    IRequestHandler<DeleteLaboratoryCommand>
{
    public static string NameTaken(string name) => $"There's already a lab called '{name}'.";

    public static string InUse(Laboratory laboratory) =>
        $"Specimens have been sent to '{laboratory.Name}', so it can't be deleted. Archive it instead.";

    public async Task<IReadOnlyList<LaboratoryResponse>> Handle(GetLaboratoriesQuery request, CancellationToken cancellationToken)
    {
        var labs = await repository.GetLaboratoriesAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetLaboratoriesInUseAsync(labs.Select(l => l.Id).ToList(), cancellationToken);
        return labs.Select(l => SpecimenMapper.ToResponse(l, inUse.Contains(l.Id))).ToList();
    }

    public async Task<LaboratoryResponse> Handle(CreateLaboratoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.LaboratoryNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(NameTaken(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var laboratory = new Laboratory
        {
            Id = Guid.NewGuid(),
            Name = name,
            UsesMedApp = request.UsesMedApp,
            CreatedAt = now,
            LastUpdated = now,
        };
        await repository.AddLaboratoryAsync(laboratory, cancellationToken);
        return SpecimenMapper.ToResponse(laboratory, inUse: false);
    }

    public async Task<LaboratoryResponse> Handle(UpdateLaboratoryCommand request, CancellationToken cancellationToken)
    {
        var laboratory = await GetOrThrowAsync(request.Id, cancellationToken);
        var name = request.Name.Trim();
        if (await repository.LaboratoryNameExistsAsync(name, laboratory.Id, cancellationToken))
            throw new ConflictException(NameTaken(name));

        laboratory.Name = name;
        laboratory.UsesMedApp = request.UsesMedApp;
        laboratory.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateLaboratoryAsync(laboratory, cancellationToken);
        return await ResponseAsync(laboratory, cancellationToken);
    }

    public async Task<LaboratoryResponse> Handle(SetLaboratoryArchivedCommand request, CancellationToken cancellationToken)
    {
        var laboratory = await GetOrThrowAsync(request.Id, cancellationToken);
        if (laboratory.IsArchived != request.IsArchived)
        {
            laboratory.IsArchived = request.IsArchived;
            laboratory.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateLaboratoryAsync(laboratory, cancellationToken);
        }

        return await ResponseAsync(laboratory, cancellationToken);
    }

    public async Task Handle(DeleteLaboratoryCommand request, CancellationToken cancellationToken)
    {
        var laboratory = await GetOrThrowAsync(request.Id, cancellationToken);
        if ((await repository.GetLaboratoriesInUseAsync([laboratory.Id], cancellationToken)).Contains(laboratory.Id))
            throw new ConflictException(InUse(laboratory));
        await repository.DeleteLaboratoryAsync(laboratory.Id, cancellationToken);
    }

    private async Task<Laboratory> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await repository.GetLaboratoryAsync(id, ct) ?? throw new NotFoundException($"Lab '{id}' was not found.");

    private async Task<LaboratoryResponse> ResponseAsync(Laboratory laboratory, CancellationToken ct) =>
        SpecimenMapper.ToResponse(laboratory, (await repository.GetLaboratoriesInUseAsync([laboratory.Id], ct)).Contains(laboratory.Id));
}
