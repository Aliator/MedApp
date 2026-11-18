using MedApp.Application.Calendar.Common;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

/// <summary>
/// Adds a year's England and Wales bank holidays (<see cref="BankHolidays"/>) as closed days, in one go. Dates already
/// closed, and past dates, are skipped and listed.
/// </summary>
public sealed record AddBankHolidaysCommand(int Year, CalendarActor Actor) : IRequest<AddBankHolidaysResponse>;
