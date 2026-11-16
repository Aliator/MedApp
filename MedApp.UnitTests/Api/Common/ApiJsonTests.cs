using System.Text.Json;
using FluentAssertions;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Infrastructure.Data.Conversions;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.PatientsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Api.Common;

/// <summary>
/// Times leave the API as UTC with a <c>Z</c>, and dates as <c>yyyy-mm-dd</c>, with the API's own JSON settings
/// (<see cref="ApiJson"/>). Browsers read a time without a zone as their local time.
/// </summary>
[TestFixture]
public sealed class ApiJsonTests
{
    /// <summary><see cref="Now"/> as EF reads it back from SQL Server, which keeps no zone.</summary>
    private static DateTime ReadFromTheDatabase() =>
        (DateTime)new UtcDateTimeConverter().ConvertFromProvider(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified))!;

    [Test]
    public void Hub_NotificationReadFromTheDatabase_IsSentAsUtcWithZ()
    {
        var notification = new NotificationResponse(Guid.Empty, "ProcedureAssigned", "Assigned", "Synthetic", null, ReadFromTheDatabase(), false);

        JsonSerializer.Serialize(notification, ApiJson.Hub).Should().Contain("\"createdAtUtc\":\"2026-06-15T12:00:00Z\"");
    }

    [Test]
    public void Controllers_DateOfBirth_IsSentAsADate()
    {
        var patient = new PatientResponse(Guid.Empty, "Test", "Patient", ValidDateOfBirth, "test.patient@example.test");

        JsonSerializer.Serialize(patient, ApiJson.Controllers).Should().Contain("\"dateOfBirth\":\"1111-01-01\"");
    }

    [Test]
    public void Controllers_DueDate_IsSentAsADateAndItsEndAsUtcWithZ()
    {
        var summary = new PatientProcedureSummaryResponse
        {
            DueDate = Today,
            DueDateUtc = (DateTime)new UtcDateTimeConverter().ConvertFromProvider(
                DateTime.SpecifyKind(DueDates.EndOfDayUtc(Today), DateTimeKind.Unspecified))!,
        };

        var json = JsonSerializer.Serialize(summary, ApiJson.Controllers);

        json.Should().Contain("\"dueDate\":\"2026-06-15\"");
        json.Should().Contain("\"dueDateUtc\":\"2026-06-15T22:59:59.9999999Z\"");
    }
}
