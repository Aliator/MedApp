using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedApp.Infrastructure.Data.Conversions;

/// <summary>
/// Every <see cref="DateTime"/> the app stores is UTC, but SQL Server's <c>datetime2</c> keeps no time zone, so EF
/// reads them back as <see cref="DateTimeKind.Unspecified"/>, and System.Text.Json then sends them without a
/// <c>Z</c>, which browsers read as their local time (an hour out during British Summer Time). This marks values read
/// from the database as UTC. Writing, it turns a local time (<c>DateTime.Now</c>) into UTC, so it isn't stored as a
/// wall-clock time and read back an hour out; UTC and unmarked values are written as they are. Applied to every
/// <c>DateTime</c> and <c>DateTime?</c> in <see cref="MedAppDbContext.ConfigureConventions"/>.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
