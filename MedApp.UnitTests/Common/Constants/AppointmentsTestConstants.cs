using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic data for appointment tests, on the calendar tests' clock (<see cref="CalendarTestConstants.Now"/>: 09:00 on
/// Monday 16 November 2026, GMT). Appointments are with <see cref="CalendarTestConstants.StaffUserId"/> unless moved.
/// </summary>
public static class AppointmentsTestConstants
{
    /// <summary>The member of staff the appointments are with, acting on their own.</summary>
    public static readonly PathwayActor Self = new(StaffUserId, StaffUsername);

    /// <summary>Someone else, changing the member of staff's appointments.</summary>
    public static readonly PathwayActor Colleague = new(Guid.Parse("0000000b-0000-0000-0000-000000000003"), "calendar.colleague");

    public const string PathwayTitle = "Synthetic pathway";
    public const string StageName = "Synthetic visit";
    public const string Notes = "Synthetic appointment notes.";
    public const string OtherNotes = "Other synthetic appointment notes.";
    public const string CancelReason = "Synthetic cancellation reason.";

    public const int DefaultPage = 1;
    public const int DefaultPageSize = 10;

    /// <summary>The accounts the mocked username lookup knows.</summary>
    public static Dictionary<Guid, string> Usernames => new()
    {
        [StaffUserId] = StaffUsername,
        [OtherStaffUserId] = OtherStaffUsername,
        [Colleague.UserId] = Colleague.Username,
    };

    /// <summary>The members of staff the mocked staff list knows (the colleague isn't one, so can't be booked with).</summary>
    public static IReadOnlyList<(Guid UserId, string Username)> Staff =>
    [
        (StaffUserId, StaffUsername),
        (OtherStaffUserId, OtherStaffUsername),
    ];

    /// <summary>A search with no filters, the first page, by start.</summary>
    public static SearchAppointmentsQuery CreateSearchQuery() =>
        new(DefaultPage, DefaultPageSize, null, true, null, null, null, null, null, null, null, null);

    /// <summary>A pathway for the appointment's patient, with the appointment linked to its first stage.</summary>
    public static PatientPathway LinkToPathway(
        Appointment appointment,
        PatientPathwayStatus status = PatientPathwayStatus.Assigned,
        VisitRequirement requirement = VisitRequirement.Required)
    {
        var pathway = PathwaysTestConstants.CreatePathway(status, patient: appointment.Patient, title: PathwayTitle);
        var stage = pathway.StageAt(1);
        stage.Name = StageName;
        stage.VisitRequirement = requirement;

        appointment.PathwayId = pathway.Id;
        appointment.Pathway = pathway;
        appointment.StageId = stage.Id;
        appointment.Stage = stage;
        return pathway;
    }
}
