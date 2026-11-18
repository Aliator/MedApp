using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;
using MedApp.Domain.Pathways.PatientPathways;
using AuditAction = MedApp.Domain.Pathways.PatientPathways.PatientPathwayAuditAction;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// Records to open when showing the app (see <c>docs/demo-data.md</c>): the names and titles depend on the day the
/// seed runs, so the summary lists them. Patients are named as "First Last", which no other patient shares (see
/// <see cref="DemoPatients"/>), so searching for the name finds the one record; procedures as "Title, for First Last".
/// </summary>
public static class DemoTour
{
    public static object Build(DemoWorld world, IReadOnlyList<PathwayPlan> plans, IReadOnlyList<PatientSeed> patients)
    {
        var names = patients.ToDictionary(p => p.Id, p => p.FullName);
        var deleted = world.Deletions.Select(d => d.Pathway.Id).ToHashSet();
        var byId = world.Pathways.Where(p => !deleted.Contains(p.Id)).ToDictionary(p => p.Id);
        string Describe(PatientPathway p) => $"{p.Title}, for {names[p.PatientId]}";
        IEnumerable<string> Pathways(Func<PathwayPlan, bool> where, Func<PatientPathway, bool>? also = null, int take = 3) =>
            plans.Where(where).Select(p => byId.GetValueOrDefault(p.PathwayId)).OfType<PatientPathway>()
                .Where(p => also?.Invoke(p) ?? true).Take(take).Select(Describe);

        var audit = world.Audit.ToLookup(a => a.PatientPathwayId);
        bool Has(PatientPathway p, AuditAction action, string? detail = null) =>
            audit[p.Id].Any(a => a.Action == action && (detail is null || a.Details.Contains(detail)));

        var lead = world.Lead;
        var day = world.Timeline.DiaryDay;
        return new
        {
            blocked = Pathways(p => p.Fate == Fate.Blocked, p => p.Status == Status.Blocked),
            blockedThenUnblocked = Pathways(p => p.Fate == Fate.Unblocked, p => Has(p, AuditAction.StatusChanged, "\"from\":\"Blocked\"")),
            cancelledWithItsBookingCancelled = Pathways(p => p.Fate == Fate.CancelledAfterBooking, p => p.Status == Status.Cancelled),
            stageUnlockedAndCorrected = Pathways(p => p.UnlockStage >= 0, p => Has(p, AuditAction.StageUnlocked)),
            finishedWithoutItsVisit = Pathways(_ => true, p => Has(p, AuditAction.StageFinished, "\"visitRequirement\":\"Required\"")),
            dueDateOrPriorityChanged = Pathways(p => p.EditDueDate || p.RaisePriority, p => Has(p, AuditAction.DetailsUpdated)),
            reassigned = Pathways(p => p.ReassignStage >= 0, p => Has(p, AuditAction.Unassigned)),
            visitNeverBooked = Pathways(p => p.NeverBookStage >= 0, p => PathwayWorkflow.IsOpen(p.Status)),
            oldPathwayStillFinishing = Pathways(p => p.Straggler, p => PathwayWorkflow.IsOpen(p.Status)),
            missedAndRebooked = world.Appointments.Where(a => a.Status == AppointmentStatus.DidNotAttend && a.PathwayId is { } id && byId.ContainsKey(id))
                .Select(a => Describe(byId[a.PathwayId!.Value])).Distinct().Take(3),
            inReview = byId.Values.Where(p => p.Status == Status.InReview).Take(3).Select(Describe),
            deletedPathwaysOf = world.Deletions.Select(d => names[d.Pathway.PatientId]),
            leadsDiaryDay = day,
            leadsAppointmentsThatDay = world.Appointments
                .Where(a => a.StaffUserId == lead.Id && a.StartUtc is { } s && UkTime.DateOf(s) == day && AppointmentWorkflow.IsOnCalendar(a.Status))
                .OrderBy(a => a.StartUtc).Select(a => $"{UkTime.DescribeTime(a.StartUtc!.Value)} {names[a.PatientId]}"),
            calendar = Calendar(world),
        };
    }

    /// <summary>
    /// Where to see meetings, the team view and personal templates: the diary day's meetings (open Calendar, Team, on
    /// that day), the next few meetings, the ones moved or cancelled (their notifications are in the bell), and whose
    /// templates differ from the usual.
    /// </summary>
    public static object Calendar(DemoWorld world)
    {
        var calendars = world.Calendars;
        var plan = calendars.MeetingPlan;
        var now = world.Timeline.NowUtc;
        var day = world.Timeline.DiaryDay;
        string Describe(Meeting m) => $"{UkTime.Describe(m.StartUtc)} to {UkTime.DescribeTime(m.EndUtc)}: {m.Title} ({m.Attendees.Count} going)";

        return new
        {
            teamViewDay = day,
            meetingsThatDay = calendars.Meetings.Where(m => UkTime.DateOf(m.StartUtc) == day).OrderBy(m => m.StartUtc).Select(Describe),
            upcomingMeetings = calendars.Meetings.Where(m => m.StartUtc > now).OrderBy(m => m.StartUtc).Take(8).Select(Describe),
            movedMeetings = new[] { plan.Replanned, plan.Moved, plan.MovedOneToOne }.OfType<MeetingStory>().Select(s => Describe(s.Meeting)),
            cancelledMeeting = plan.Cancelled is { } cancelled ? Describe(cancelled.Meeting) : null,
            personalTemplates = new[]
            {
                $"{DemoStaff.DiabetesNurseUsername}: '{DemoTemplates.PartTimeWeek}' since {UkTime.DescribeDate(world.Timeline.ThisMonday.AddDays(-7 * 20))}, '{DemoTemplates.FullTimeWeek}' before",
                $"{DemoTemplates.LateLunchUsername}: '{DemoTemplates.MinorSurgeryList}' lunch at 13:15 since {UkTime.DescribeDate(world.Timeline.ThisMonday.AddDays(-7 * 10))}, 12:45 before",
                $"{DemoStaff.GynaecologistUsername} and {DemoStaff.UrologistUsername}: a 'Visiting week' ('{DemoTemplates.AtTheHospital}' on the days they aren't here)",
                $"{DemoStaff.ScientistUsername}: '{DemoTemplates.LabReporting}' every day",
                $"{DemoStaff.CoordinatorUsername}: a late lunch, 13:30, covering the desk",
                $"{DemoStaff.SkinGpUsername}: '{DemoTemplates.FridayList}' (minor surgery on Friday mornings)",
                $"{DemoStaff.ManagerUsername}: '{DemoTemplates.OfficeEarlyFinish}' on Fridays",
                $"{world.Lead.Username}: '{DemoTemplates.ClinicAndManagement}' on Wednesdays, '{DemoTemplates.OutreachClinic}' on Thursdays",
            },
        };
    }
}
