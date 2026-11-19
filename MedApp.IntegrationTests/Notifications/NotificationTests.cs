using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.API.Common.Authentication;
using MedApp.API.Notifications;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Notifications;

/// <summary>Being assigned a pathway: the stored notification, and its live delivery through the hub.</summary>
[TestFixture]
public sealed class NotificationTests : IntegrationTest
{
    private static readonly TimeSpan LiveDeliveryTimeout = TimeSpan.FromSeconds(15);

    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;
    private PatientPathwayResponse _pathway = null!;

    [SetUp]
    public async Task CreatePathwayAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
        _pathway = await CreatePatientWithPathwayAsync(_admin, stages: 1);
    }

    [Test]
    public async Task AssignPathway_ToSomeoneElse_StoresAnUnreadNotificationForThem()
    {
        var assigned = await AssignAsync(_staff.UserId);

        assigned.Status.Should().Be("Assigned");
        assigned.Assignments.Should().ContainSingle().Which.Username.Should().Be(_staff.Username);
        var mine = await NotificationsOfAsync(_staff.Client);
        mine.UnreadCount.Should().Be(1);
        mine.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Kind = "PathwayAssigned",
            Title = _pathway.Title,
            PathwayId = (Guid?)_pathway.Id,
            IsRead = false,
        });
        mine.Items[0].Message.Should().Contain((await ClinicStaffAsync()).Username);
        (await NotificationsOfAsync(_admin)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task AssignPathway_ToYourself_StoresNoNotification()
    {
        // Whoever assigns (the test's clinic staff) assigns themselves.
        var me = await ClinicStaffAsync();

        await AssignAsync(me.UserId);

        (await NotificationsOfAsync(me.Client)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task MarkRead_One_LeavesTheOthersUnread()
    {
        await AssignAsync(_staff.UserId);
        var second = await CreatePatientWithPathwayAsync(_admin, stages: 1);
        await (await (await ClinicalAsync(_admin)).PostAsync($"{PathwayUrl(second.Id)}/assignments/{_staff.UserId}", null)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var before = await NotificationsOfAsync(_staff.Client);

        await (await _staff.Client.PostAsync($"/api/notifications/{before.Items[0].Id}/read", null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var after = await NotificationsOfAsync(_staff.Client);
        after.UnreadCount.Should().Be(1);
        after.Items.Single(n => n.Id == before.Items[0].Id).IsRead.Should().BeTrue();
    }

    [Test]
    public async Task MarkAllRead_SomeUnread_MarksThemAllRead()
    {
        await AssignAsync(_staff.UserId);

        await (await _staff.Client.PostAsync("/api/notifications/read", null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        var mine = await NotificationsOfAsync(_staff.Client);
        mine.UnreadCount.Should().Be(0);
        mine.Items.Should().AllSatisfy(n => n.IsRead.Should().BeTrue());
    }

    [Test]
    public async Task AssignPathway_UserHasAPageOpen_PushesTheNotificationThroughTheHub()
    {
        var received = new TaskCompletionSource<NotificationResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var hub = await ConnectToHubAsync(_staff.Username, _staff.Password);
        hub.On<NotificationResponse>(NotificationsHub.NotificationMethod, n => received.TrySetResult(n));

        await AssignAsync(_staff.UserId);

        var pushed = await received.Task.WaitAsync(LiveDeliveryTimeout);
        var stored = (await NotificationsOfAsync(_staff.Client)).Items.Single();
        pushed.Should().BeEquivalentTo(stored);
    }

    [Test]
    public async Task Hub_SignedOut_RefusesTheConnection()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(MedAppApiFactory.BaseAddress, NotificationsHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Api.Server.CreateHandler();
            })
            .Build();

        var act = () => hub.StartAsync();

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await hub.DisposeAsync();
    }

    private async Task<PatientPathwayResponse> AssignAsync(Guid userId) =>
        await (await (await ClinicalAsync(_admin)).PostAsync($"{PathwayUrl(_pathway.Id)}/assignments/{userId}", null))
            .ReadAsAsync<PatientPathwayResponse>();

    private static async Task<NotificationsResponse> NotificationsOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();

    /// <summary>
    /// Connects to the hub as a page would, with its own session. The test server has no real sockets, so it uses long
    /// polling through the server's own handler (the browser uses WebSockets; the hub and the push are the same).
    /// </summary>
    private static async Task<HubConnection> ConnectToHubAsync(string username, string password)
    {
        var login = await (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password)))
            .ReadAsAsync<LoginResponse>();

        var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(MedAppApiFactory.BaseAddress, NotificationsHub.Path), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Api.Server.CreateHandler();
                options.Headers["Cookie"] = $"{SessionAuthenticationDefaults.HostCookieName}={login.SessionId}";
            })
            .Build();
        await hub.StartAsync();
        return hub;
    }
}
