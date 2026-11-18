using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Notifications.Commands.MarkNotificationsRead;
using MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;
using MedApp.Application.Notifications.Queries.GetMyNotificationPreferences;
using MedApp.Application.Notifications.Queries.GetMyNotifications;
using MedApp.Contracts.Notifications.Requests;
using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Contracts.Common;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Pathways.Audit.Requests;
using MedApp.Contracts.Pathways.Audit.Responses;
using MedApp.Infrastructure.Data.Conversions;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PathwayAuditControllerTests
{
    [Test]
    public async Task SearchPathwayAudit_SendsEveryFilterAndReturnsThePage()
    {
        var request = AutoFixtureFactory.Create().Create<SearchPathwayAuditRequest>();
        var page = new PagedResponse<PatientPathwayAuditEntryResponse>([], 1, 10, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await new PathwayAuditController(mediator).WithRequest(SignedIn("Admin")).SearchPathwayAudit(request, cts.Token);

        mediator.Request.Should().Be(new SearchPathwayAuditQuery(
            request.Page, request.PageSize, request.Ascending, request.PathwayId, request.PatientId, request.UserId,
            request.Action, request.Search, request.From, request.To, request.Actions));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task SearchPathwayAudit_TimeReadFromTheDatabase_IsSentAsUtcWithZ()
    {
        // As SQL Server gives a time back (no time zone), then as EF reads it (UtcDateTimeConverter).
        var stored = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);
        var read = (DateTime)new UtcDateTimeConverter().ConvertFromProvider(stored)!;
        var page = new PagedResponse<PatientPathwayAuditEntryResponse>([new PatientPathwayAuditEntryResponse { AtUtc = read }], 1, 10, 1, 1);
        var mediator = new RecordingMediator(_ => page);

        var result = await new PathwayAuditController(mediator).WithRequest(SignedIn("Admin"))
            .SearchPathwayAudit(AutoFixtureFactory.Create().Create<SearchPathwayAuditRequest>(), CancellationToken.None);

        var json = JsonSerializer.Serialize(result.Should().BeOfType<OkObjectResult>().Which.Value, ApiJson.Controllers);
        json.Should().Contain("\"atUtc\":\"2026-06-15T12:00:00Z\"");
    }
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

    [Test]
    public async Task GetMyPreferences_ReturnsTheSignedInUsersChoices()
    {
        var preferences = new NotificationPreferencesResponse(false);
        var mediator = new RecordingMediator(_ => preferences);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetMyPreferences(cts.Token);

        mediator.Request.Should().Be(new GetMyNotificationPreferencesQuery(UserId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(preferences);
    }

    [Test]
    public async Task UpdateMyPreferences_SavesTheSignedInUsersChoices()
    {
        var preferences = new NotificationPreferencesResponse(false);
        var mediator = new RecordingMediator(_ => preferences);

        var result = await Controller(mediator).UpdateMyPreferences(
            new UpdateNotificationPreferencesRequest { PathwayAssigned = false }, CancellationToken.None);

        mediator.Request.Should().Be(new UpdateMyNotificationPreferencesCommand(UserId, false));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(preferences);
    }
}
