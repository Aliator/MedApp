using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Contracts.Consent;
using MedApp.Domain.Consent;
using MediatR;

namespace MedApp.Application.Consent.Setup;

// Consent forms: organisation-wide setup, kept by holders of ManageSetup and read by everyone. One a consent record has
// used is archived, not deleted (decision 145).

/// <summary>Every consent form by name, then version; archived ones only with <paramref name="IncludeArchived"/>.</summary>
public sealed record GetConsentFormsQuery(bool IncludeArchived) : IRequest<IReadOnlyList<ConsentFormResponse>>;

public sealed record CreateConsentFormCommand(string Name, string FormVersion) : IRequest<ConsentFormResponse>;

public sealed record UpdateConsentFormCommand(Guid Id, string Name, string FormVersion) : IRequest<ConsentFormResponse>;

public sealed record SetConsentFormArchivedCommand(Guid Id, bool IsArchived) : IRequest<ConsentFormResponse>;

public sealed record DeleteConsentFormCommand(Guid Id) : IRequest;

public sealed class CreateConsentFormValidator : AbstractValidator<CreateConsentFormCommand>
{
    public CreateConsentFormValidator()
    {
        RuleFor(x => x.Name).FormName();
        RuleFor(x => x.FormVersion).FormVersion();
    }
}

public sealed class UpdateConsentFormValidator : AbstractValidator<UpdateConsentFormCommand>
{
    public UpdateConsentFormValidator()
    {
        RuleFor(x => x.Name).FormName();
        RuleFor(x => x.FormVersion).FormVersion();
    }
}

public sealed class ConsentFormHandlers(IConsentRepository repository, TimeProvider timeProvider) :
    IRequestHandler<GetConsentFormsQuery, IReadOnlyList<ConsentFormResponse>>,
    IRequestHandler<CreateConsentFormCommand, ConsentFormResponse>,
    IRequestHandler<UpdateConsentFormCommand, ConsentFormResponse>,
    IRequestHandler<SetConsentFormArchivedCommand, ConsentFormResponse>,
    IRequestHandler<DeleteConsentFormCommand>
{
    public static string Taken(string name, string version) =>
        $"There's already a consent form called '{name}', version {version}.";

    public static string InUse(ConsentForm form) =>
        $"Consent has been recorded on '{form.Name}', so it can't be deleted. Archive it instead.";

    public async Task<IReadOnlyList<ConsentFormResponse>> Handle(GetConsentFormsQuery request, CancellationToken cancellationToken)
    {
        var forms = await repository.GetFormsAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetFormsInUseAsync(forms.Select(f => f.Id).ToList(), cancellationToken);
        return forms.Select(f => ConsentMapper.ToResponse(f, inUse.Contains(f.Id))).ToList();
    }

    public async Task<ConsentFormResponse> Handle(CreateConsentFormCommand request, CancellationToken cancellationToken)
    {
        var (name, version) = (request.Name.Trim(), request.FormVersion.Trim());
        if (await repository.FormExistsAsync(name, version, null, cancellationToken))
            throw new ConflictException(Taken(name, version));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var form = new ConsentForm
        {
            Id = Guid.NewGuid(),
            Name = name,
            FormVersion = version,
            CreatedAt = now,
            LastUpdated = now,
        };
        await repository.AddFormAsync(form, cancellationToken);
        return ConsentMapper.ToResponse(form, inUse: false);
    }

    public async Task<ConsentFormResponse> Handle(UpdateConsentFormCommand request, CancellationToken cancellationToken)
    {
        var form = await GetOrThrowAsync(request.Id, cancellationToken);
        var (name, version) = (request.Name.Trim(), request.FormVersion.Trim());
        if (await repository.FormExistsAsync(name, version, form.Id, cancellationToken))
            throw new ConflictException(Taken(name, version));

        form.Name = name;
        form.FormVersion = version;
        form.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateFormAsync(form, cancellationToken);
        return await ResponseAsync(form, cancellationToken);
    }

    public async Task<ConsentFormResponse> Handle(SetConsentFormArchivedCommand request, CancellationToken cancellationToken)
    {
        var form = await GetOrThrowAsync(request.Id, cancellationToken);
        if (form.IsArchived != request.IsArchived)
        {
            form.IsArchived = request.IsArchived;
            form.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateFormAsync(form, cancellationToken);
        }

        return await ResponseAsync(form, cancellationToken);
    }

    public async Task Handle(DeleteConsentFormCommand request, CancellationToken cancellationToken)
    {
        var form = await GetOrThrowAsync(request.Id, cancellationToken);
        if ((await repository.GetFormsInUseAsync([form.Id], cancellationToken)).Contains(form.Id))
            throw new ConflictException(InUse(form));
        await repository.DeleteFormAsync(form.Id, cancellationToken);
    }

    private async Task<ConsentForm> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await repository.GetFormAsync(id, ct) ?? throw new NotFoundException($"Consent form '{id}' was not found.");

    private async Task<ConsentFormResponse> ResponseAsync(ConsentForm form, CancellationToken ct) =>
        ConsentMapper.ToResponse(form, (await repository.GetFormsInUseAsync([form.Id], ct)).Contains(form.Id));
}

/// <summary>Validation rules for consent forms.</summary>
public static class ConsentFormRules
{
    public static IRuleBuilderOptions<T, string> FormName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Give it a name.")
            .Must(n => n is null || n.Trim().Length <= ConsentForm.MaxNameLength)
            .WithMessage($"The name must be at most {ConsentForm.MaxNameLength} characters.");

    public static IRuleBuilderOptions<T, string> FormVersion<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Give the form's version, as printed on it.")
            .Must(v => v is null || v.Trim().Length <= ConsentForm.MaxFormVersionLength)
            .WithMessage($"The version must be at most {ConsentForm.MaxFormVersionLength} characters.");
}
