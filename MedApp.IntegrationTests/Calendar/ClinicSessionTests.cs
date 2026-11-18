using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Clinics.Requests;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Sites.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Calendar;

/// <summary>
/// Clinic sessions in SQL Server (centre plan decisions 123 to 127): booking into a slot, with two patients racing for
/// one slot; a room held by one session at a time, with two sessions racing for one room; a site's closed day closing
/// its rooms; cancelling a session with its bookings; patterns making sessions only when asked; and who may do what.
/// </summary>
[TestFixture]
public sealed class ClinicSessionTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _clinician = null!;
    private SiteResponse _site = null!;
    private RoomResponse _room = null!;
    private SpecialtyResponse _specialty = null!;
    private DateOnly _day;

    [SetUp]
    public async Task SetUpClinicAsync()
    {
        _admin = await SignInAsAdminAsync();
        _clinician = await CreateStaffAsync(UserRole);
        _site = await CreateSiteAsync(_admin);
        _room = await CreateRoomAsync(_admin, _site.Id);
        _specialty = await (await _admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);
        _day = NextWorkingDay();
    }

    private Task<HttpResponseMessage> PostSessionAsync(
        HttpClient client, Guid staffUserId, Guid roomId, int startHour, int endHour, int slotMinutes = 20, DateOnly? day = null) =>
        client.PostAsJsonAsync("/api/clinics/sessions", new ClinicSessionRequest
        {
            StaffUserId = staffUserId,
            RoomId = roomId,
            SpecialtyId = _specialty.Id,
            StartUtc = UkAt(day ?? _day, startHour),
            EndUtc = UkAt(day ?? _day, endHour),
            SlotMinutes = slotMinutes
        });

    private async Task<ClinicSessionResponse> CreateSessionAsync(int startHour = 9, int endHour = 12) =>
        await (await PostSessionAsync(_admin, _clinician.UserId, _room.Id, startHour, endHour)).ReadAsAsync<ClinicSessionResponse>(HttpStatusCode.Created);

    private static Task<HttpResponseMessage> BookIntoSlotAsync(HttpClient client, Guid appointmentId, ClinicSessionResponse session, DateTime slotStart) =>
        client.PostAsJsonAsync($"{AppointmentUrl(appointmentId)}/book", new BookAppointmentRequest
        {
            StaffUserId = session.StaffUserId,
            StartUtc = slotStart,
            EndUtc = slotStart.AddMinutes(session.SlotMinutes),
            ExpectedStatus = "Requested",
            ClinicSessionId = session.Id
        });

    private static async Task<ClinicSessionDetailResponse> GetSessionAsync(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/clinics/sessions/{id}")).ReadAsAsync<ClinicSessionDetailResponse>();

    [Test]
    public async Task CreateSession_ShowsOnTheCalendarAsBusyTime_AndInTheList()
    {
        var session = await CreateSessionAsync();

        session.SlotCount.Should().Be(9);
        session.FreeSlotCount.Should().Be(9);
        session.RoomName.Should().Be(_room.Name);
        session.SiteName.Should().Be(_site.Name);
        var calendar = await GetCalendarAsync(_clinician.Client, _clinician.UserId, _day);
        calendar.Sessions.Should().ContainSingle().Which.Id.Should().Be(session.Id);
        var listed = await (await _clinician.Client.GetAsync($"/api/clinics/sessions?from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}&withFreeSlots=true"))
            .ReadAsAsync<List<ClinicSessionResponse>>();
        listed.Select(s => s.Id).Should().Contain(session.Id);

        // A block over it is a clash on the clinician's calendar.
        var block = await PostBlockAsync(_clinician.Client, _clinician.UserId, UkAt(_day, 11), UkAt(_day, 13));
        (await block.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Contain("clinic session");
    }

    [Test]
    public async Task BookIntoSlot_TakesTheSessionAndRoom_AndAnotherTimeIsRefused()
    {
        var session = await CreateSessionAsync();
        var patient = await CreatePatientAsync(_admin);
        var requested = await RequestAppointmentAsync(_admin, patient.Id);

        var booked = await (await BookIntoSlotAsync(_admin, requested.Id, session, UkAt(_day, 9, 20))).ReadAsAsync<AppointmentResponse>();

        booked.ClinicSessionId.Should().Be(session.Id);
        booked.RoomId.Should().Be(_room.Id);
        var detail = await GetSessionAsync(_admin, session.Id);
        detail.Slots[1].PatientId.Should().Be(patient.Id);
        detail.Session.BookedCount.Should().Be(1);

        // Not a slot; and a time in the session outside it is the session's.
        var other = await RequestAppointmentAsync(_admin, patient.Id);
        (await (await BookIntoSlotAsync(_admin, other.Id, session, UkAt(_day, 9, 10))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Be(ClinicRules.NotASlot(new MedApp.Domain.Clinics.ClinicSession { StartUtc = session.StartUtc, SlotMinutes = 20 }));
        (await (await PostBookAsync(_admin, other.Id, _clinician.UserId, UkAt(_day, 10))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("clinic session");
    }

    /// <summary>
    /// Two people book two patients into the same slot at once: the clinician's calendar lock lets one in, and the other
    /// sees it there (409). Repeated over several slots, so a lucky ordering can't pass it by chance.
    /// </summary>
    [Test]
    public async Task BookIntoSlot_TwoPatientsRacingForOneSlot_OneWinsAndTheOtherGets409()
    {
        var session = await CreateSessionAsync();
        var ana = await CreateStaffAsync(UserRole);
        var ben = await CreateStaffAsync(UserRole);

        for (var i = 0; i < 4; i++)
        {
            var slot = UkAt(_day, 9).AddMinutes(20 * i);
            var first = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);
            var second = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);

            var responses = await Task.WhenAll(
                BookIntoSlotAsync(ana.Client, first.Id, session, slot),
                BookIntoSlotAsync(ben.Client, second.Id, session, slot));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        }

        var detail = await GetSessionAsync(_admin, session.Id);
        detail.Session.BookedCount.Should().Be(4);
        detail.Slots.Take(4).Should().OnlyContain(s => s.AppointmentId != null);
        (await AppointmentsInSessionAsync(session.Id)).Should().Be(4);
    }

    /// <summary>Booking new appointments straight into one slot at once (POST api/appointments): the same.</summary>
    [Test]
    public async Task CreateAppointment_TwoNewBookingsRacingForOneSlot_OneWins()
    {
        var session = await CreateSessionAsync();
        var slot = UkAt(_day, 10);

        Task<HttpResponseMessage> Book(Guid patientId) => _admin.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = patientId,
            StaffUserId = _clinician.UserId,
            StartUtc = slot,
            EndUtc = slot.AddMinutes(20),
            ClinicSessionId = session.Id
        });

        var responses = await Task.WhenAll(Book((await CreatePatientAsync(_admin)).Id), Book((await CreatePatientAsync(_admin)).Id));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        (await AppointmentsInSessionAsync(session.Id)).Should().Be(1);
    }

    /// <summary>
    /// The last word on one patient per slot is the database's: two appointments saved into one slot past every check
    /// (as if the lock were missing) are refused by the unique index, as a 409.
    /// </summary>
    [Test]
    public async Task UniqueIndex_TwoAppointmentsInOneSlot_TheSecondIsRefused()
    {
        var session = await CreateSessionAsync();
        var patient = await CreatePatientAsync(_admin);
        var actor = new PathwayActor(_clinician.UserId, _clinician.Username);

        async Task AddAsync()
        {
            await RunInScopeAsync(async services =>
            {
                var appointment = new Appointment
                {
                    Id = Guid.NewGuid(),
                    PatientId = patient.Id,
                    Status = AppointmentStatus.Requested,
                    CreatedAtUtc = Now,
                    CreatedByUserId = _clinician.UserId,
                    LastUpdated = Now
                };
                var change = new AppointmentChange(appointment, actor, Now);
                change.Book(_clinician.UserId, _clinician.Username, UkAt(_day, 9), UkAt(_day, 9, 20), new AppointmentPlace(session.Id, _room.Id));
                await services.GetRequiredService<IAppointmentRepository>().AddAsync(appointment, change.Events, CancellationToken.None);
            });
        }

        await AddAsync();
        var act = AddAsync;

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClinicRules.SlotTaken);
    }

    /// <summary>
    /// Two clinicians' sessions in one room at the same time, asked for at once: the room's lock lets one in, and the
    /// other sees the room taken (409). Repeated over several hours.
    /// </summary>
    [Test]
    public async Task CreateSession_TwoSessionsRacingForOneRoom_OneWinsAndTheOtherGets409()
    {
        var other = await CreateStaffAsync(UserRole);

        for (var hour = 8; hour < 16; hour += 2)
        {
            var responses = await Task.WhenAll(
                PostSessionAsync(_admin, _clinician.UserId, _room.Id, hour, hour + 2),
                PostSessionAsync(_admin, other.UserId, _room.Id, hour, hour + 2));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            (await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
                .Title.Should().Contain(_room.Name).And.Contain("is taken by a clinic session");
        }

        var sessions = await (await _admin.GetAsync($"/api/clinics/sessions?from={_day:yyyy-MM-dd}&to={_day:yyyy-MM-dd}&roomId={_room.Id}"))
            .ReadAsAsync<List<ClinicSessionResponse>>();
        sessions.Should().HaveCount(4);
    }

    /// <summary>An appointment outside any session that names the room, and a session there, asked for at once.</summary>
    [Test]
    public async Task RoomBookingAndSession_RacingForOneRoom_OneWins()
    {
        var other = await CreateStaffAsync(UserRole);
        var patient = await CreatePatientAsync(_admin);

        var responses = await Task.WhenAll(
            PostSessionAsync(_admin, _clinician.UserId, _room.Id, 9, 12),
            _admin.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
            {
                PatientId = patient.Id,
                StaffUserId = other.UserId,
                StartUtc = UkAt(_day, 10),
                EndUtc = UkAt(_day, 10, 30),
                RoomId = _room.Id
            }));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
    }

    [Test]
    public async Task SitesClosedDay_ClosesItsRooms_ForSessionsAndBookings()
    {
        var session = await CreateSessionAsync();
        var requested = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);
        await AddClosedDayAsync(_admin, _day, siteId: _site.Id);

        (await (await BookIntoSlotAsync(_admin, requested.Id, session, UkAt(_day, 9))).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("is closed on");
        var otherRoom = await CreateRoomAsync(_admin, _site.Id);
        (await (await PostSessionAsync(_admin, _clinician.UserId, otherRoom.Id, 13, 15)).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict))
            .Title.Should().Contain("is closed on");
    }

    [Test]
    public async Task CancelSession_CancelsItsBookings_ListsWhomToTell_AndTellsTheClinician()
    {
        var session = await CreateSessionAsync();
        var patients = new List<PatientResponse> { await CreatePatientAsync(_admin), await CreatePatientAsync(_admin) };
        var appointments = new List<AppointmentResponse>();
        for (var i = 0; i < patients.Count; i++)
        {
            var requested = await RequestAppointmentAsync(_admin, patients[i].Id);
            appointments.Add(await (await BookIntoSlotAsync(_admin, requested.Id, session, UkAt(_day, 9).AddMinutes(20 * i))).ReadAsAsync<AppointmentResponse>());
        }

        var stale = await _admin.PostAsJsonAsync($"/api/clinics/sessions/{session.Id}/cancel",
            new CancelClinicSessionRequest { Reason = CancelReason, ExpectedLastUpdated = session.LastUpdated.AddMinutes(-1) });
        await stale.ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        var result = await (await _admin.PostAsJsonAsync($"/api/clinics/sessions/{session.Id}/cancel",
                new CancelClinicSessionRequest { Reason = CancelReason, ExpectedLastUpdated = session.LastUpdated }))
            .ReadAsAsync<CancelClinicSessionResponse>();

        result.Session.Status.Should().Be("Cancelled");
        result.PatientsToTell.Select(p => p.PatientId).Should().Equal(patients.Select(p => p.Id));
        foreach (var appointment in appointments)
        {
            var now = await GetAppointmentAsync(_admin, appointment.Id);
            now.Status.Should().Be("Cancelled");
            now.CancelReason.Should().Be(CancelReason);
        }

        var told = await (await _clinician.Client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
        told.Items.Should().ContainSingle(n => n.Kind == "ClinicSessionCancelled");
        told.Items.Should().NotContain(n => n.Kind == "AppointmentCancelled");
        var listed = await RunInScopeAsync(async services => await services.GetRequiredService<MedAppDbContext>().ActivityEntries
            .CountAsync(e => e.Action == ActivityAction.PatientListed && e.PatientId == patients[0].Id));
        listed.Should().BeGreaterThan(0);

        // Off the calendar, and the time is free again.
        (await GetCalendarAsync(_clinician.Client, _clinician.UserId, _day)).Sessions.Should().BeEmpty();
        await (await PostSessionAsync(_admin, _clinician.UserId, _room.Id, 9, 12)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    /// <summary>
    /// Cancelling a session part-way through its day leaves what has started for its outcome, and the call list can be
    /// had again from the cancelled session.
    /// </summary>
    [Test]
    public async Task CancelSession_PartWayThroughItsDay_KeepsWhatHasStarted_AndTheCallListCanBeReadAgain()
    {
        var session = await CreateSessionAsync();
        var first = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);
        var second = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);
        await (await BookIntoSlotAsync(_admin, first.Id, session, UkAt(_day, 9))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await BookIntoSlotAsync(_admin, second.Id, session, UkAt(_day, 10))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        MoveClockPast(UkAt(_day, 9));

        var result = await (await _admin.PostAsJsonAsync($"/api/clinics/sessions/{session.Id}/cancel",
                new CancelClinicSessionRequest { Reason = CancelReason, ExpectedLastUpdated = session.LastUpdated }))
            .ReadAsAsync<CancelClinicSessionResponse>();

        result.PatientsToTell.Select(p => p.AppointmentId).Should().Equal(second.Id);
        (await GetAppointmentAsync(_admin, first.Id)).Status.Should().Be("Booked");
        var again = await GetSessionAsync(_clinician.Client, session.Id);
        again.Session.Status.Should().Be("Cancelled");
        again.PatientsToTell.Select(p => p.AppointmentId).Should().Equal(second.Id);
        again.Slots[0].AppointmentId.Should().Be(first.Id);
    }

    [Test]
    public async Task UpdateSession_WithBookings_OnlyTheRoomMoves_AndItsAppointmentsGoWithIt()
    {
        var session = await CreateSessionAsync();
        var requested = await RequestAppointmentAsync(_admin, (await CreatePatientAsync(_admin)).Id);
        await (await BookIntoSlotAsync(_admin, requested.Id, session, UkAt(_day, 9))).ShouldHaveStatusAsync(HttpStatusCode.OK);
        var newRoom = await CreateRoomAsync(_admin, _site.Id);

        UpdateClinicSessionRequest Change(Guid roomId, int endHour) => new()
        {
            RoomId = roomId,
            SpecialtyId = _specialty.Id,
            StartUtc = session.StartUtc,
            EndUtc = UkAt(_day, endHour),
            SlotMinutes = 20,
            ExpectedLastUpdated = session.LastUpdated
        };

        (await (await _admin.PutAsJsonAsync($"/api/clinics/sessions/{session.Id}", Change(_room.Id, 13)))
            .ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(ClinicRules.HasBookings);
        var moved = await (await _admin.PutAsJsonAsync($"/api/clinics/sessions/{session.Id}", Change(newRoom.Id, 12)))
            .ReadAsAsync<ClinicSessionResponse>();

        moved.RoomId.Should().Be(newRoom.Id);
        (await GetAppointmentAsync(_admin, requested.Id)).RoomId.Should().Be(newRoom.Id);
        var history = await RunInScopeAsync(async services => await services.GetRequiredService<MedAppDbContext>().AppointmentEvents
            .Where(e => e.AppointmentId == requested.Id).OrderBy(e => e.AtUtc).Select(e => e.Action).ToListAsync());
        history.Should().Equal(AppointmentEventAction.Requested, AppointmentEventAction.Booked, AppointmentEventAction.Rescheduled);
        // The old room is free again; it's still in use (named), so it can't be deleted.
        await (await PostSessionAsync(_admin, _clinician.UserId, _room.Id, 13, 15)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await _admin.DeleteAsync($"/api/rooms/{_room.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Pattern_CreateSessions_MakesThemOnce_AndSaysWhatItSkipped()
    {
        var pattern = await (await _admin.PostAsJsonAsync("/api/clinics/patterns", new ClinicPatternRequest
        {
            StaffUserId = _clinician.UserId,
            RoomId = _room.Id,
            SpecialtyId = _specialty.Id,
            DayOfWeek = _day.DayOfWeek.ToString(),
            Starts = new TimeOnly(9, 0),
            Ends = new TimeOnly(12, 0),
            SlotMinutes = 20,
            EveryWeeks = 1,
            FromDate = _day
        })).ReadAsAsync<ClinicPatternResponse>(HttpStatusCode.Created);
        // A day closed everywhere a week on is skipped.
        await AddClosedDayAsync(_admin, _day.AddDays(7));

        var first = await (await _admin.PostAsJsonAsync($"/api/clinics/patterns/{pattern.Id}/create-sessions", new CreateSessionsRequest { Weeks = 3 }))
            .ReadAsAsync<CreateSessionsResponse>();
        var second = await (await _admin.PostAsJsonAsync($"/api/clinics/patterns/{pattern.Id}/create-sessions", new CreateSessionsRequest { Weeks = 3 }))
            .ReadAsAsync<CreateSessionsResponse>();

        first.Created.Select(s => s.Date).Should().Contain(_day).And.NotContain(_day.AddDays(7));
        first.Skipped.Should().ContainSingle(s => s.Date == _day.AddDays(7));
        first.Created.Should().OnlyContain(s => s.PatternId == pattern.Id);
        second.Created.Should().BeEmpty();
        second.Skipped.Where(s => s.Reason == ClinicPatternRules.AlreadyMade).Select(s => s.Date)
            .Should().BeEquivalentTo(first.Created.Select(s => s.Date));
        (await (await _admin.GetAsync("/api/clinics/patterns")).ReadAsAsync<List<ClinicPatternResponse>>())
            .Should().ContainSingle(p => p.Id == pattern.Id);
    }

    [Test]
    public async Task WithoutManageClinics_StaffReadSessions_ButCantChangeThem()
    {
        var session = await CreateSessionAsync();

        await (await PostSessionAsync(_clinician.Client, _clinician.UserId, _room.Id, 13, 15)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _clinician.Client.PostAsJsonAsync($"/api/clinics/sessions/{session.Id}/cancel",
            new CancelClinicSessionRequest { Reason = CancelReason, ExpectedLastUpdated = session.LastUpdated })).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _clinician.Client.GetAsync("/api/clinics/patterns")).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        (await GetSessionAsync(_clinician.Client, session.Id)).Session.CanChange.Should().BeFalse();
        (await GetSessionAsync(_admin, session.Id)).Session.CanChange.Should().BeTrue();
    }

    private static Task<int> AppointmentsInSessionAsync(Guid sessionId) =>
        RunInScopeAsync(async services => await services.GetRequiredService<MedAppDbContext>().Appointments
            .CountAsync(a => a.ClinicSessionId == sessionId && a.Status == AppointmentStatus.Booked));
}
