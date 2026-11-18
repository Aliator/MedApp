using MedApp.Application.Letters.Common;
using MedApp.Contracts.Letters.Responses;
using MediatR;

namespace MedApp.Application.Letters.Queries.GetLetterMergeFields;

/// <summary>Every merge field a template's text may hold, in the order the template page lists them.</summary>
public sealed record GetLetterMergeFieldsQuery : IRequest<IReadOnlyList<LetterMergeFieldResponse>>;

public sealed class GetLetterMergeFieldsHandler
    : IRequestHandler<GetLetterMergeFieldsQuery, IReadOnlyList<LetterMergeFieldResponse>>
{
    public Task<IReadOnlyList<LetterMergeFieldResponse>> Handle(
        GetLetterMergeFieldsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LetterMergeFieldResponse>>(
            LetterMergeFields.All.Select(LetterTemplateMapper.ToResponse).ToList());
}
