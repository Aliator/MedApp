using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.SearchRetentionReview;

/// <summary>
/// One page of the patients whose records are past their retention period and due for review (admins only), the
/// longest overdue first. With <paramref name="IncludeKept"/>, also those an admin has decided to keep for now.
/// </summary>
public sealed record SearchRetentionReviewQuery(int Page, int PageSize, bool IncludeKept)
    : IRequest<PagedResponse<RetentionReviewItemResponse>>;
