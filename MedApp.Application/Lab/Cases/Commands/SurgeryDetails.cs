using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Lab.Cases.Commands;

/// <summary>
/// The hospital's surgery details, typed by the lab as written (MC20; the light demo's <c>addSurgeryDetails</c>): the
/// date, the procedure, the outcome and other details, or "no surgery expected" with why. Nothing is worked out from them.
/// </summary>
public sealed record AddSurgeryDetailsCommand(
    Guid LabCaseId,
    DateOnly? SurgeryOn,
    string? Procedure,
    string? Outcome,
    bool NoSurgeryExpected,
    string? Detail,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<LabCaseResponse>;

public sealed class AddSurgeryDetailsValidator : AbstractValidator<AddSurgeryDetailsCommand>
{
    public AddSurgeryDetailsValidator()
    {
        RuleFor(x => x.Procedure).Must(t => Fits(t, LabCaseSurgeryDetails.MaxProcedureLength))
            .WithMessage(TooLong("The procedure", LabCaseSurgeryDetails.MaxProcedureLength));
        RuleFor(x => x.Outcome).Must(t => Fits(t, LabCaseSurgeryDetails.MaxTextLength))
            .WithMessage(TooLong("The outcome", LabCaseSurgeryDetails.MaxTextLength));
        RuleFor(x => x.Detail).Must(t => Fits(t, LabCaseSurgeryDetails.MaxTextLength))
            .WithMessage(TooLong("The detail", LabCaseSurgeryDetails.MaxTextLength));

        When(x => x.NoSurgeryExpected, () =>
        {
            RuleFor(x => x.NoSurgeryExpected)
                .Must((x, _) => x.SurgeryOn is null && LabCaseRules.Optional(x.Procedure) is null && LabCaseRules.Optional(x.Outcome) is null)
                .WithMessage(LabCaseRules.NoSurgeryFieldsMessage);
            RuleFor(x => x.Detail).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage(LabCaseRules.NoSurgeryWhyMessage);
        }).Otherwise(() =>
            RuleFor(x => x.Procedure)
                .Must((x, _) => x.SurgeryOn is not null
                                || LabCaseRules.Optional(x.Procedure) is not null
                                || LabCaseRules.Optional(x.Outcome) is not null
                                || LabCaseRules.Optional(x.Detail) is not null)
                .WithMessage(LabCaseRules.SurgeryDetailsMissingMessage));

        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }

    public static string TooLong(string what, int max) => $"{what} must be at most {max:N0} characters.";

    private static bool Fits(string? text, int max) => text is null || text.Trim().Length <= max;
}

/// <summary>
/// Adds the hospital's surgery details (the lab team, ProcessSpecimens): 409 for anyone else, an anonymised record, a
/// stale version, a clinic's case or a closed one; 404 for a case behind the wall. "No surgery expected" only once the
/// report has been sent (400). Added to a case awaiting them, they close it; before its report, they're kept and the case
/// stays open. Audited (<c>SurgeryDetailsAdded</c>, <c>LabCaseClosed</c>; never the text).
/// </summary>
public sealed class AddSurgeryDetailsHandler(LabCaseAccess cases, ILabCaseRepository repository, TimeProvider timeProvider)
    : IRequestHandler<AddSurgeryDetailsCommand, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(AddSurgeryDetailsCommand request, CancellationToken cancellationToken)
    {
        var labTeamId = await cases.LabTeamIdAsync(cancellationToken);
        LabCaseAccess.EnsureWorksCases(request.Actor, labTeamId);
        var (labCase, _) = await cases.FindAsync(request.LabCaseId, request.Actor, labTeamId, forUpdate: true, cancellationToken);

        if (PatientRules.IsAnonymised(labCase.Patient))
            throw new ConflictException(LabCaseRules.AnonymisedMessage);
        LabCaseRules.EnsureVersion(labCase, request.ExpectedVersion);
        if (labCase.Source != SpecimenSource.Hospital)
            throw new ConflictException(LabCaseRules.SurgeryDetailsHospitalOnlyMessage);
        if (!LabCaseRules.TakesSurgeryDetails(labCase))
            throw new ConflictException(LabCaseRules.SurgeryDetailsClosedMessage);
        if (request.NoSurgeryExpected && labCase.Status != LabCaseStatus.AwaitingSurgeryDetails)
            throw LabCaseValidation.Invalid(nameof(request.NoSurgeryExpected), LabCaseRules.NoSurgeryNotSentMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new LabCaseChange(labCase, labCase.Pathway, request.Actor, now);
        change.AddSurgeryDetails(
            request.SurgeryOn,
            LabCaseRules.Optional(request.Procedure),
            LabCaseRules.Optional(request.Outcome),
            request.NoSurgeryExpected,
            LabCaseRules.Optional(request.Detail));
        repository.Track(change, isNew: false);
        await repository.SaveAsync(cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}
