using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Notifications.Commands.MarkNotificationsRead;
using MedApp.Application.Notifications.Queries.GetMyNotifications;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Contracts.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Procedures.Audit.Requests;
using MedApp.Contracts.Procedures.Audit.Responses;
using MedApp.Infrastructure.Data.Conversions;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class ProcedureAuditControllerTests
{
    [Test]
    public async Task SearchProcedureAudit_SendsEveryFilterAndReturnsThePage()
    {
        var request = AutoFixtureFactory.Create().Create<SearchProcedureAuditRequest>();
        var page = new PagedResponse<PatientProcedureAuditEntryResponse>([], 1, 10, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await new ProcedureAuditController(mediator).WithRequest(SignedIn("Admin")).SearchProcedureAudit(request, cts.Token);

        mediator.Request.Should().Be(new SearchProcedureAuditQuery(
            request.Page, request.PageSize, request.Ascending, request.ProcedureId, request.PatientId, request.UserId,
            request.Action, request.Search, request.From, request.To, request.Actions));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task SearchProcedureAudit_TimeReadFromTheDatabase_IsSentAsUtcWithZ()
    {
        // As SQL Server gives a time back (no time zone), then as EF reads it (UtcDateTimeConverter).
        var stored = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);
        var read = (DateTime)new UtcDateTimeConverter().ConvertFromProvider(stored)!;
        var page = new PagedResponse<PatientProcedureAuditEntryResponse>([new PatientProcedureAuditEntryResponse { AtUtc = read }], 1, 10, 1, 1);
        var mediator = new RecordingMediator(_ => page);

        var result = await new ProcedureAuditController(mediator).WithRequest(SignedIn("Admin"))
            .SearchProcedureAudit(AutoFixtureFactory.Create().Create<SearchProcedureAuditRequest>(), CancellationToken.None);

        var json = JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Which.Value, MvcJsonOptions());
        json.Should().Contain("\"atUtc\":\"2026-06-15T12:00:00Z\"");
    }

    /// <summary>The JSON options the API's controllers write responses with.</summary>
    private static JsonSerializerOptions MvcJsonOptions() =>
        new ServiceCollection().AddControllers().Services.BuildServiceProvider()
            .GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
}

[TestFixture]
public sealed class NotificationsControllerTests
{
    private static NotificationsController Controller(RecordingMediator mediator) =>
        new NotificationsController(mediator).WithRequest(SignedIn("User"));

    [TestCase(null, GetMyNotificationsQuery.DefaultTake)]
    [TestCase(5, 5)]
    public async Task GetMine_ReturnsTheSignedInUsersNewest(int? take, int expectedTake)
    {
        var notifications = new NotificationsResponse([], 0);
        var mediator = new RecordingMediator(_ => notifications);

        var result = await Controller(mediator).GetMine(take, CancellationToken.None);

        mediator.Request.Should().Be(new GetMyNotificationsQuery(UserId, expectedTake));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(notifications);
    }

    [Test]
    public async Task MarkAllRead_MarksEveryOneOfTheirs()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).MarkAllRead(CancellationToken.None);

        mediator.Request.Should().Be(new MarkNotificationsReadCommand(UserId, null));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task MarkRead_MarksThatOne()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).MarkRead(RecordId, CancellationToken.None);

        mediator.Request.Should().Be(new MarkNotificationsReadCommand(UserId, RecordId));
        result.Should().BeOfType<NoContentResult>();
    }
}
