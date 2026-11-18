using FluentValidation;
using MedApp.Application.Common.Paging;

namespace MedApp.Application.Appointments.Queries.SearchAppointments;

public sealed class SearchAppointmentsValidator : AbstractValidator<SearchAppointmentsQuery>
{
    public SearchAppointmentsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be 1 or more.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");

        RuleFor(x => x.Sort)
            .Must(sort => sort is null || AppointmentSearch.IsSortColumn(sort))
            .WithMessage($"Sort must be one of: {string.Join(", ", AppointmentSearch.SortColumns)}.");

        RuleForEach(x => x.Statuses)
            .Must(AppointmentSearch.IsStatus)
            .WithMessage($"Each status must be one of: {AppointmentSearch.StatusNames}.");

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("The end of the date range must not be before its start.");

        RuleFor(x => x.View)
            .Must(view => view is null || AppointmentSearch.IsView(view))
            .WithMessage($"View must be one of: {string.Join(", ", AppointmentSearch.Views)}.");

        RuleFor(x => x.Search)
            .MaximumLength(200)
            .WithMessage("Search must not exceed 200 characters.");
    }
}
