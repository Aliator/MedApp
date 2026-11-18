using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.Data;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Appointments;

/// <summary>
/// What deleting does to appointments: a patient with appointments stays, a deleted pathway's appointments are
/// detached in the same save (their history keeps it), and an account with upcoming bookings stays.
/// </summary>
[TestFixture]
public sealed class AppointmentDeletionTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SignInAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    [Test]
    public async Task DeletePatient_WithAnAppointment_Returns409()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var appointment = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, NextBookableStart());
        await (await PostCancelAsync(_staff.Client, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        // Even a cancelled one: its history is the patient's.
        await (await _staff.Client.DeleteAsync($"/api/patients/{patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _staff.Client.GetAsync($"/api/patients/{patient.Id}")).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    /// <summary>
    /// PatientPathwayRepository.DeleteAsync clears the appointments' pathway and stage in the same save that
    /// deletes the pathway, so neither the foreign keys nor a half-done delete get in the way.
    /// </summary>
    [Test]
    public async Task DeletePathway_Cancelled_KeepsItsAppointmentsDetached()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var first = await CreateStageDefinitionAsync(_admin, visitRequirement: "Required");
        var second = await CreateStageDefinitionAsync(_admin);
        var template = await CreateTemplateAsync(_admin, first.Id, second.Id);
        var pathway = await CreatePathwayAsync(_staff.Client, patient.Id, template.Id);
        var requested = (await SearchAppointmentsAsync(_staff.Client, $"pathwayId={pathway.Id}")).Single();
        var booked = await BookNewAppointmentAsync(_staff.Client, patient.Id, _staff.UserId, NextBookableStart(),
            pathwayId: pathway.Id, stageId: pathway.Stages[1].Id);
        await (await PostPathwayStatusAsync(_staff.Client, pathway.Id, "Cancelled", BlockReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.DeleteAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await _staff.Client.GetAsync(PathwayUrl(pathway.Id))).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        foreach (var id in new[] { requested.Id, booked.Id })
        {
            var detached = await GetAppointmentAsync(_staff.Client, id);
            detached.Should().BeEquivalentTo(new
            {
                PathwayId = (Guid?)null,
                PathwayTitle = (string?)null,
                StageId = (Guid?)null,
                PatientId = patient.Id,
            });
        }

        (await GetAppointmentAsync(_staff.Client, booked.Id)).Status.Should().Be("Booked");
        (await SearchAppointmentsAsync(_staff.Client, $"pathwayId={pathway.Id}")).Should().BeEmpty();

        // Their history keeps the pathway: admins see it (as deleted), staff see nothing of a deleted pathway's.
        var history = await RunInScopeAsync(services => services.GetRequiredService<MedAppDbContext>()
            .Set<AppointmentEvent>().AsNoTracking().Where(e => e.AppointmentId == booked.Id).ToListAsync());
        history.Should().NotBeEmpty().And.AllSatisfy(e => e.PathwayId.Should().Be(pathway.Id));
        var adminTimeline = await TimelineAsync(_admin, patient.Id);
        adminTimeline.Where(e => e.AppointmentId == booked.Id).Should().NotBeEmpty()
            .And.AllSatisfy(e => e.Should().BeEquivalentTo(new { PathwayId = (Guid?)pathway.Id, IsPathwayDeleted = true }));
        (await TimelineAsync(_staff.Client, patient.Id)).Should().NotContain(e => e.PathwayId == pathway.Id);
    }

    [Test]
    public async Task DeleteUser_WithAnUpcomingBookedAppointment_Returns400UntilItsCancelled()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var appointment = await BookNewAppointmentAsync(_admin, patient.Id, _staff.UserId, NextBookableStart());

        var refused = await _admin.DeleteAsJsonAsync($"/api/auth/users/{_staff.Username}", new DeleteUserRequest(AdminPassword));

        var errors = await refused.ReadAsAsync<List<IdentityError>>(HttpStatusCode.BadRequest);
        errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.HasUpcomingAppointmentsCode);
        errors[0].Description.Should().Contain("1 upcoming appointment.");
        await (await _admin.GetAsync($"/api/auth/users/{_staff.Username}")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await PostCancelAsync(_admin, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{_staff.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        // The cancelled appointment stays, its member of staff shown as a deleted user.
        (await GetAppointmentAsync(_admin, appointment.Id)).Should().BeEquivalentTo(new
        {
            StaffUserId = (Guid?)_staff.UserId,
            StaffUsername = PathwayMapper.DeletedUser,
            Status = "Cancelled",
        });
    }

    /// <summary>
    /// Taking every role from someone with upcoming bookings would take their calendar away (it's only for staff), so
    /// it's refused as deleting them is. Keeping a staff role, or cancelling the bookings first, is fine.
    /// </summary>
    [Test]
    public async Task SetRoles_NoneForSomeoneWithAnUpcomingBookedAppointment_Returns400UntilItsCancelled()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var appointment = await BookNewAppointmentAsync(_admin, patient.Id, _staff.UserId, NextBookableStart());
        var url = $"/api/auth/users/{_staff.Username}/roles";

        var refused = await _admin.PutAsJsonAsync(url, new SetUserRolesRequest([]));

        var errors = await refused.ReadAsAsync<List<IdentityError>>(HttpStatusCode.BadRequest);
        errors.Should().ContainSingle().Which.Code.Should().Be(IdentityErrors.HasUpcomingAppointmentsCode);
        errors[0].Description.Should().Be($"{_staff.Username} has 1 upcoming appointment. Move or cancel them first.");
        (await GetCalendarAsync(_admin, _staff.UserId, UkToday)).Should().NotBeNull("they're still staff, with a calendar");

        // Another staff role is fine: they keep their calendar.
        await (await _admin.PutAsJsonAsync(url, new SetUserRolesRequest([AdminRole]))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await PostCancelAsync(_admin, appointment.Id, CancelReason)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await _admin.PutAsJsonAsync(url, new SetUserRolesRequest([]))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteUser_WhosePastAppointmentWasAttended_IsDeleted()
    {
        var patient = await CreatePatientAsync(_staff.Client);
        var start = NextBookableStart();
        var appointment = await BookNewAppointmentAsync(_admin, patient.Id, _staff.UserId, start);
        MoveClockPast(start);
        await (await PostOutcomeAsync(_admin, appointment.Id, "Attended")).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await (await _admin.DeleteAsJsonAsync($"/api/auth/users/{_staff.Username}", new DeleteUserRequest(AdminPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    private static async Task<List<PatientTimelineEntryResponse>> TimelineAsync(HttpClient client, Guid patientId) =>
        (await (await client.GetAsync($"/api/patients/{patientId}/timeline?pageSize=100"))
            .ReadAsAsync<PatientTimelinePageResponse>()).Items;
}
