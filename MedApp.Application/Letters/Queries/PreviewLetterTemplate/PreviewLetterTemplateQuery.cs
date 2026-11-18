using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Letters.Common;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Queries.PreviewLetterTemplate;

/// <summary>A rendered PDF and the name it's downloaded under.</summary>
public sealed record LetterPdf(string FileName, byte[] Content)
{
    public const string ContentType = "application/pdf";
}

/// <summary>
/// A template's text (saved or not) as a PDF, filled with made-up details (<see cref="LetterMergeFields.SampleValues"/>)
/// and marked as a preview on every page, so an admin sees the layout, the merge fields' places and the large print.
/// </summary>
public sealed record PreviewLetterTemplateQuery(
    string? Name,
    string Letterhead,
    string BodySkeleton,
    string Closing,
    bool LargePrint) : IRequest<LetterPdf>;

public sealed class PreviewLetterTemplateValidator : AbstractValidator<PreviewLetterTemplateQuery>
{
    public PreviewLetterTemplateValidator(ILetterRenderer renderer)
    {
        RuleFor(x => x.Name)
            .Must(name => name is null || name.Trim().Length <= LetterTemplate.MaxNameLength)
            .WithMessage($"Name must not exceed {LetterTemplate.MaxNameLength} characters.");

        this.TemplateText(renderer, x => x.Letterhead, x => x.BodySkeleton, x => x.Closing);
    }
}

public sealed class PreviewLetterTemplateHandler(ILetterRenderer renderer, TimeProvider timeProvider)
    : IRequestHandler<PreviewLetterTemplateQuery, LetterPdf>
{
    public const string FileName = "letter-template-preview.pdf";

    public const string Marking = "Preview with made-up details: not a letter";

    public async Task<LetterPdf> Handle(PreviewLetterTemplateQuery request, CancellationToken cancellationToken)
    {
        var values = LetterMergeFields.SampleValues(UkTime.Today(timeProvider));
        var example = (string name) => values[name] ?? string.Empty;

        // The practice is the example recipient, so the address shows where a real one will go.
        var recipient = new List<string> { example(LetterMergeFields.PracticeName) };
        recipient.AddRange(LetterMergeFields.SplitLines(example(LetterMergeFields.PracticeAddress)));

        var letter = new LetterDocument(
            Title: string.IsNullOrWhiteSpace(request.Name) ? "Letter template preview" : $"{request.Name.Trim()} (preview)",
            Letterhead: LetterTemplateRules.Lines(LetterMergeFields.FillPlain(request.Letterhead, values).Text),
            RecipientAddress: recipient,
            Body: LetterMergeFields.FillMarkdown(LetterTemplateRules.Tidy(request.BodySkeleton), values).Text,
            Closing: LetterTemplateRules.Lines(LetterMergeFields.FillPlain(request.Closing, values).Text),
            PatientIdentifiers: LetterIdentifiers.Line(
                example(LetterMergeFields.PatientName),
                example(LetterMergeFields.PatientDateOfBirth),
                example(LetterMergeFields.PatientNhsNumber)),
            LargePrint: request.LargePrint,
            Marking: Marking);

        return new LetterPdf(FileName, await renderer.RenderAsync(letter, cancellationToken));
    }
}
