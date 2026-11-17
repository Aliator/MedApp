using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Appointments.Requests;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Contracts.Notifications.Responses;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Appointments;

/// <summary>
/// Two people changing one appointment at once, in SQL Server: the appointment's row version and the booking's
/// expected status make the second a 409 instead of a silent overwrite, and a filtered unique index keeps a stage to
/// one open appointment even when two requests pass the handler's check together.
/// </summary>
[TestFixture]
public sealed class AppointmentConcurrencyTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _ana = null!;
    private StaffAccount _ben = null!;
    private PatientResponse _patient = null!;

    [SetUp]
    public async Task SetUpPeopleAsync()
    {
        _admin = await SignInAsAdminAsync();
        _ana = await CreateStaffAsync(UserRole);
        _ben = await CreateStaffAsync(UserRole);
        _patient = await CreatePatientAsync(_admin);
    }

    /// <summary>
    /// Ana books a Requested appointment with Dr P while Ben books the same one with Dr Q. Each takes a different
    /// calendar lock, so they don't wait for each other: whichever comes second is refused, by its expected status
    /// (it reads the appointment once the first has saved) or by the row version (both read it first). Repeated over
    /// a few appointments, so a lucky ordering can't pass it by chance.
    /// </summary>
    [Test]
    public async Task BookAppointment_OneRequestedAppointmentBookedTwiceAtOnceForDifferentStaff_OneWinsAndTheOtherGets409()
    {
        var drP = await CreateStaffAsync(UserRole);
        var drQ = await CreateStaffAsync(UserRole);
        var start = NextBookableStart();

        for (var i = 0; i < 4; i++)
        {
            var requested = await RequestAppointmentAsync(_admin, _patient.Id);
            var slot = start.AddHours(i);

            var responses = await Task.WhenAll(
                PostBookAsync(_ana.Client, requested.Id, drP.UserId, slot),
                PostBookAsync(_ben.Client, requested.Id, drQ.UserId, slot));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            var winner = await responses.Single(r => r.StatusCode == HttpStatusCode.OK).ReadAsAsync<AppointmentResponse>();
            var loser = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
            loser.Title.Should().BeOneOf(AppointmentWorkflow.BookedMeanwhileMessage, AppointmentWorkflow.ChangedMeanwhileMessage);

            // Stored as the winner saved it, with one Booked entry in its history.
            (await GetAppointmentAsync(_admin, requested.Id)).Should().BeEquivalentTo(new
            {
                Status = "Booked",
                winner.StaffUserId,
                StartUtc = (DateTime?)slot,
            });
            (await HistoryAsync(requested.Id)).Select(e => e.Action).Should().Equal(AppointmentEventAction.Requested, AppointmentEventAction.Booked);
        }

        // Only the member of staff each appointment ended up with was told: four bookings, four notifications.
        var told = (await NotificationsOfAsync(drP.Client)).Items.Concat((await NotificationsOfAsync(drQ.Client)).Items).ToList();
        told.Should().HaveCount(4).And.AllSatisfy(n => n.Kind.Should().Be("AppointmentBooked"));
    }

    [Test]
    public async Task BookAppointment_AlreadyBookedBySomeoneElse_StaleBookIs409AndKeepsTheirBooking()
    {
        var requested = await RequestAppointmentAsync(_admin, _patient.Id);
        var start = NextBookableStart();
        await (await PostBookAsync(_ana.Client, requested.Id, _ana.UserId, start)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Ben's list still showed it as needing booking.
        var stale = await PostBookAsync(_ben.Client, requested.Id, _ana.UserId, start.AddHours(4));

        (await stale.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(AppointmentWorkflow.BookedMeanwhileMessage);
        (await GetAppointmentAsync(_admin, requested.Id)).StartUtc.Should().Be(start);
        (await HistoryAsync(requested.Id)).Select(e => e.Action).Should().Equal(AppointmentEventAction.Requested, AppointmentEventAction.Booked);
        // Moving it, knowing it's booked, works.
        await (await PostRescheduleAsync(_ben.Client, requested.Id, _ana.UserId, start.AddHours(4))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Test]
    public async Task BookAppointment_WithoutAnExpectedStatus_Returns400()
    {
        var requested = await RequestAppointmentAsync(_admin, _patient.Id);

        var response = await PostBookAsync(_ana.Client, requested.Id, _ana.UserId, NextBookableStart(), expectedStatus: "");

        (await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest)).Errors.Should().ContainKey("ExpectedStatus");
    }

    /// <summary>The row version itself: a save from a copy loaded before someone else's save changes nothing.</summary>
    [Test]
    public async Task SaveAsync_FromACopyLoadedBeforeAnotherSave_IsAConflict_AndKeepsTheOtherSave()
    {
        var appointment = await RequestAppointmentAsync(_admin, _patient.Id);

        await using var first = Api.Services.CreateAsyncScope();
        await using var second = Api.Services.CreateAsyncScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var firstCopy = (await firstRepository.GetForUpdateAsync(appointment.Id, CancellationToken.None))!;
        var staleCopy = (await secondRepository.GetForUpdateAsync(appointment.Id, CancellationToken.None))!;

        firstCopy.Notes = "Synthetic first notes";
        await firstRepository.SaveAsync([], CancellationToken.None);

        staleCopy.Notes = "Synthetic stale notes";
        var act = () => secondRepository.SaveAsync([], CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(AppointmentWorkflow.ChangedMeanwhileMessage);
        var stored = await RunInScopeAsync(services =>
            services.GetRequiredService<IAppointmentRepository>().GetByIdAsync(appointment.Id, CancellationToken.None));
        stored!.Notes.Should().Be("Synthetic first notes");
        stored.RowVersion.Should().NotBeEmpty();
    }

    /// <summary>
    /// Two people ask for a visit for one stage at the same moment. Both can pass the handler's check; the filtered
    /// unique index refuses the second save, and it gets the same 409 the check gives. Repeated over a few stages.
    /// </summary>
    [Test]
    public async Task CreateAppointment_TwoVisitsForOneStageAtOnce_OneWinsAndTheOtherGets409()
    {
        var procedure = await CreateProcedureAsync(_admin, _patient.Id, (await CreateTemplateWithStagesAsync(_admin, 4)).Id);

        foreach (var stage in procedure.Stages)
        {
            var responses = await Task.WhenAll(
                PostRequestAsync(_ana.Client, procedure.Id, stage.Id),
                PostRequestAsync(_ben.Client, procedure.Id, stage.Id));

            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
            var loser = await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
            loser.Title.Should().Be("This stage already has a visit in Requires Appointment. Book that one instead.");
        }

        var open = await SearchAppointmentsAsync(_admin, $"procedureId={procedure.Id}");
        open.Should().HaveCount(procedure.Stages.Count);
        open.Select(a => a.StageId).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public async Task CreateAppointment_ForAStageWithABookedVisit_Returns409SayingWhenAndWithWhom()
    {
        var procedure = await CreateProcedureAsync(_admin, _patient.Id, (await CreateTemplateWithStagesAsync(_admin, 1)).Id);
        var stageId = procedure.Stages.Single().Id;
        var start = NextBookableStart();
        await BookNewAppointmentAsync(_ana.Client, _patient.Id, _ana.UserId, start, procedureId: procedure.Id, stageId: stageId);

        var refused = await PostRequestAsync(_ben.Client, procedure.Id, stageId);

        (await refused.ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict)).Title.Should().Be(
            $"This stage already has a visit booked, on {UkTime.Describe(start)} with {_ana.Username}. " +
            "Reschedule or cancel that one instead.");
    }

    /// <summary>The index itself, past the handler: a second open appointment for a stage isn't saved.</summary>
    [Test]
    public async Task AddAsync_ASecondOpenAppointmentForAStage_ThrowsAndSavesNothing_ButAClosedOneIsFine()
    {
        var procedure = await CreateProcedureAsync(_admin, _patient.Id, (await CreateTemplateWithStagesAsync(_admin, 1)).Id);
        var stageId = procedure.Stages.Single().Id;
        var first = await RequestAppointmentAsync(_admin, _patient.Id, procedure.Id, stageId);

        var act = () => RunInScopeAsync(services => services.GetRequiredService<IAppointmentRepository>()
            .AddAsync(NewAppointment(procedure.Id, stageId, AppointmentStatus.Requested), [], CancellationToken.None));

        (await act.Should().ThrowAsync<StageHasOpenAppointmentException>()).Which.StageId.Should().Be(stageId);
        (await SearchAppointmentsAsync(_admin, $"stageId={stageId}")).Select(a => a.Id).Should().Equal(first.Id);

        // Closed appointments don't count: once the first is cancelled, a new one can be asked for.
        await RunInScopeAsync(services => services.GetRequiredService<IAppointmentRepository>()
            .AddAsync(NewAppointment(procedure.Id, stageId, AppointmentStatus.Cancelled), [], CancellationToken.None));
        await (await PostCancelAsync(_admin, first.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PostRequestAsync(_ana.Client, procedure.Id, stageId)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    private Task<HttpResponseMessage> PostRequestAsync(HttpClient client, Guid procedureId, Guid stageId) =>
        client.PostAsJsonAsync("/api/appointments", new CreateAppointmentRequest
        {
            PatientId = _patient.Id,
            ProcedureId = procedureId,
            StageId = stageId,
        });

    private Appointment NewAppointment(Guid procedureId, Guid stageId, AppointmentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = _patient.Id,
        ProcedureId = procedureId,
        StageId = stageId,
        Status = status,
        CancelReason = status == AppointmentStatus.Cancelled ? CancelReason : null,
        CreatedAtUtc = Now,
        CreatedByUserId = _ana.UserId,
        LastUpdated = Now,
    };

    private static Task<List<AppointmentEvent>> HistoryAsync(Guid appointmentId) =>
        RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>()
            .Set<AppointmentEvent>().AsNoTracking()
            .Where(e => e.AppointmentId == appointmentId)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Action)
            .ToListAsync());

    private static async Task<NotificationsResponse> NotificationsOfAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications")).ReadAsAsync<NotificationsResponse>();
}
