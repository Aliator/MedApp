using FluentValidation;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

public sealed class GetAttachmentsValidator : AbstractValidator<GetAttachmentsQuery>
{
    public GetAttachmentsValidator()
    {
        RuleFor(x => x)
            .Must(x => new[] { x.PatientId, x.ProcedureId, x.StageId }.Count(id => id is not null) == 1)
            .OverridePropertyName("Target")
            .WithMessage("Give exactly one of a patient, a procedure or a stage.");
    }
}
