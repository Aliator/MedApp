using System.Text.RegularExpressions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Hospitals.Common;
using MedApp.Application.Hospitals.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Hospitals.Responses;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Hospitals.Setup;

// Hospitals (MC18 of docs/multi-clinic-plan.md; the light demo's hospitals.ts): organisation-wide setup, kept by
// holders of ManageSetup and read by everyone. One a request names is archived, never deleted.

/// <summary>Every hospital by name; archived ones only with <paramref name="IncludeArchived"/>.</summary>
public sealed record GetHospitalsQuery(bool IncludeArchived) : IRequest<IReadOnlyList<HospitalResponse>>;

/// <summary>A hospital's details, for a new one or a change.</summary>
public interface IHospitalDetails
{
    string? Name { get; }
    string? OdsCode { get; }
    string? Contact { get; }
    string? ReportDeliveryMethod { get; }
    int DefaultDeadlineDays { get; }
}

public sealed record CreateHospitalCommand(
    string? Name, string? OdsCode, string? Contact, string? ReportDeliveryMethod, int DefaultDeadlineDays)
    : IRequest<HospitalResponse>, IHospitalDetails;

public sealed record UpdateHospitalCommand(
    Guid Id, string? Name, string? OdsCode, string? Contact, string? ReportDeliveryMethod, int DefaultDeadlineDays)
    : IRequest<HospitalResponse>, IHospitalDetails;

public sealed record SetHospitalArchivedCommand(Guid Id, bool IsArchived) : IRequest<HospitalResponse>;

public sealed record DeleteHospitalCommand(Guid Id) : IRequest;

/// <summary>The hospital's details, checked as the light demo's <c>hospitalFields</c> does.</summary>
public static partial class HospitalRules
{
    public const string NameMissing = "Give it a name.";
    public static readonly string NameTooLong = $"The name must be at most {Hospital.MaxNameLength} characters.";
    public const string OdsCodeInvalid = "Enter its ODS code: 3 to 10 letters and digits.";
    public static readonly string ContactTooLong = $"The contact must be at most {Hospital.MaxContactLength} characters.";
    public const string DeliveryMethodInvalid = "Choose how its reports are sent.";
    public static readonly string DeadlineInvalid =
        $"The default deadline must be {Hospital.MinDeadlineDays} to {Hospital.MaxDeadlineDays} days.";

    public static string NameTaken(string name) => $"There's already a hospital called '{name}'.";

    public static string OdsCodeTaken(string odsCode) => $"There's already a hospital with the ODS code '{odsCode}'.";

    public static string InUse(Hospital hospital) =>
        $"'{hospital.Name}' has sent requests, so it can't be deleted. Archive it instead.";

    public static NotFoundException NotFound(Guid id) => new($"Hospital '{id}' was not found.");

    /// <summary>An ODS code as stored: trimmed, in capitals.</summary>
    public static string OdsCode(string? odsCode) => odsCode?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool IsOdsCode(string? odsCode) => OdsCodePattern().IsMatch(OdsCode(odsCode));

    public static bool TryParseMethod(string? method, out LetterDispatchMethod result) =>
        PathwayEnums.TryParse(method, out result);

    [GeneratedRegex("^[A-Z0-9]{3,10}$")]
    private static partial Regex OdsCodePattern();
}

public abstract class HospitalDetailsValidator<T> : AbstractValidator<T> where T : IHospitalDetails
{
    protected HospitalDetailsValidator()
    {
        RuleFor(x => x.Name).Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(HospitalRules.NameMissing)
            .Must(n => n is null || n.Trim().Length <= Hospital.MaxNameLength).WithMessage(HospitalRules.NameTooLong);
        RuleFor(x => x.OdsCode).Must(HospitalRules.IsOdsCode).WithMessage(HospitalRules.OdsCodeInvalid);
        RuleFor(x => x.Contact).Must(c => c is null || c.Length <= Hospital.MaxContactLength).WithMessage(HospitalRules.ContactTooLong);
        RuleFor(x => x.ReportDeliveryMethod).Must(m => HospitalRules.TryParseMethod(m, out _))
            .WithMessage(HospitalRules.DeliveryMethodInvalid);
        RuleFor(x => x.DefaultDeadlineDays).InclusiveBetween(Hospital.MinDeadlineDays, Hospital.MaxDeadlineDays)
            .WithMessage(HospitalRules.DeadlineInvalid);
    }
}

public sealed class CreateHospitalValidator : HospitalDetailsValidator<CreateHospitalCommand>;

public sealed class UpdateHospitalValidator : HospitalDetailsValidator<UpdateHospitalCommand>;

public sealed class HospitalHandlers(IHospitalRepository repository, TimeProvider timeProvider) :
    IRequestHandler<GetHospitalsQuery, IReadOnlyList<HospitalResponse>>,
    IRequestHandler<CreateHospitalCommand, HospitalResponse>,
    IRequestHandler<UpdateHospitalCommand, HospitalResponse>,
    IRequestHandler<SetHospitalArchivedCommand, HospitalResponse>,
    IRequestHandler<DeleteHospitalCommand>
{
    public async Task<IReadOnlyList<HospitalResponse>> Handle(GetHospitalsQuery request, CancellationToken cancellationToken)
    {
        var hospitals = await repository.GetAllAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetInUseAsync(hospitals.Select(h => h.Id).ToList(), cancellationToken);
        return hospitals.Select(h => HospitalMapper.ToResponse(h, inUse.Contains(h.Id))).ToList();
    }

    public async Task<HospitalResponse> Handle(CreateHospitalCommand request, CancellationToken cancellationToken)
    {
        var (name, odsCode) = await EnsureUniqueAsync(request, null, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hospital = new Hospital { Id = Guid.NewGuid(), CreatedAt = now, Version = 1 };
        Apply(hospital, request, name, odsCode, now);
        await repository.AddAsync(hospital, cancellationToken);
        return HospitalMapper.ToResponse(hospital, inUse: false);
    }

    public async Task<HospitalResponse> Handle(UpdateHospitalCommand request, CancellationToken cancellationToken)
    {
        var hospital = await GetOrThrowAsync(request.Id, cancellationToken);
        var (name, odsCode) = await EnsureUniqueAsync(request, hospital.Id, cancellationToken);
        Apply(hospital, request, name, odsCode, timeProvider.GetUtcNow().UtcDateTime);
        hospital.Version++;
        await repository.UpdateAsync(hospital, cancellationToken);
        return await ResponseAsync(hospital, cancellationToken);
    }

    public async Task<HospitalResponse> Handle(SetHospitalArchivedCommand request, CancellationToken cancellationToken)
    {
        var hospital = await GetOrThrowAsync(request.Id, cancellationToken);
        if (hospital.IsArchived != request.IsArchived)
        {
            hospital.IsArchived = request.IsArchived;
            hospital.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            hospital.Version++;
            await repository.UpdateAsync(hospital, cancellationToken);
        }

        return await ResponseAsync(hospital, cancellationToken);
    }

    public async Task Handle(DeleteHospitalCommand request, CancellationToken cancellationToken)
    {
        var hospital = await GetOrThrowAsync(request.Id, cancellationToken);
        if ((await repository.GetInUseAsync([hospital.Id], cancellationToken)).Contains(hospital.Id))
            throw new ConflictException(HospitalRules.InUse(hospital));
        await repository.DeleteAsync(hospital.Id, cancellationToken);
    }

    private async Task<(string Name, string OdsCode)> EnsureUniqueAsync(IHospitalDetails details, Guid? exceptId, CancellationToken ct)
    {
        var name = details.Name!.Trim();
        var odsCode = HospitalRules.OdsCode(details.OdsCode);
        if (await repository.NameExistsAsync(name, exceptId, ct))
            throw new ConflictException(HospitalRules.NameTaken(name));
        if (await repository.OdsCodeExistsAsync(odsCode, exceptId, ct))
            throw new ConflictException(HospitalRules.OdsCodeTaken(odsCode));
        return (name, odsCode);
    }

    private static void Apply(Hospital hospital, IHospitalDetails details, string name, string odsCode, DateTime nowUtc)
    {
        hospital.Name = name;
        hospital.OdsCode = odsCode;
        hospital.Contact = string.IsNullOrWhiteSpace(details.Contact) ? null : details.Contact.Trim();
        HospitalRules.TryParseMethod(details.ReportDeliveryMethod, out var method);
        hospital.ReportDeliveryMethod = method;
        hospital.DefaultDeadlineDays = details.DefaultDeadlineDays;
        hospital.LastUpdated = nowUtc;
    }

    private async Task<Hospital> GetOrThrowAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) ?? throw HospitalRules.NotFound(id);

    private async Task<HospitalResponse> ResponseAsync(Hospital hospital, CancellationToken ct) =>
        HospitalMapper.ToResponse(hospital, (await repository.GetInUseAsync([hospital.Id], ct)).Contains(hospital.Id));
}
