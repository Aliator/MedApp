using MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;
using MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.DataRequests.Commands;
using MedApp.Application.Jobs.Common;
using MedApp.Application.Mdt.Commands.ChooseMdtPresenter;
using MedApp.Application.Mdt.Commands.ListForMdt;
using MedApp.Application.Mdt.Commands.RecordMdtOutcome;
using MedApp.Application.Monitoring.ServiceAlerts;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using static MedApp.DevMcp.DemoData.DemoPatients;
using static MedApp.DevMcp.DemoData.DemoSetup;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The clinic's meetings and calendars (the light demo's base seed and clinical slice): two Dermatology MDTs, one that
/// has started this morning (or on the last weekday) and the skin cancer team meeting ahead, with their lists; Nadia
/// Thornbury's blocks for this week and next; Bea Sandiford out all day on the next two open days, so the tour's team
/// meeting suggestions move past them; and the service alerts the admin's rules raise.
/// </summary>
public static class DemoClinic
{
    /// <summary>Today's MDT (08:00 to 08:45): today's if it has started and run, else the last weekday's.</summary>
    public static DateTime MdtToday(DemoTimeline t) => t.LastPast(8, 0, 60);

    /// <summary>The meetings, made before the work they list.</summary>
    public static async Task<(MeetingResponse Today, MeetingResponse Ahead)> CreateMeetingsAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var dermatology = seed.TeamId(DemoPeople.Dermatology);
        var (nadia, bea, ravi) = (DemoPeople.Thornbury, DemoPeople.Sandiford, DemoPeople.Aldous);

        // The MDT that has started: the service manager arranged it, the lab's pathologist invited to present (MC21). Tom
        // Hallworth isn't invited (he is in the light demo): it's before his working day, which the app refuses.
        var start = MdtToday(t);
        var today = await seed.Send(t.Ago(7, 9, 0), DemoPeople.Whitlock, new CreateMeetingCommand("Skin MDT (morning)", "Dermatology multidisciplinary team meeting.",
            start, start.AddMinutes(45), [nadia.Id, bea.Id, ravi.Id], DemoPeople.Whitlock.Calendar, IsMdt: true, TeamId: dermatology), "arrange today's MDT");
        foreach (var (who, at) in new[] { (nadia, t.Ago(6, 9, 30)), (bea, t.Ago(6, 10, 0)), (ravi, t.Ago(6, 11, 0)) })
            await seed.Send(at, who, new ReplyToMeetingCommand(today.Id, nameof(MeetingReply.Accepted), today.StartUtc, today.EndUtc, who.Calendar), "accept today's MDT");

        // The skin cancer team meeting ahead, arranged by the admin.
        var day = t.OpenDayAhead(2);
        var ahead = await seed.Send(t.Ago(3, 9, 0), DemoPeople.Admin, new CreateMeetingCommand("Skin cancer team meeting", "Weekly review of this week’s histology results.",
            DemoTimeline.At(day, 15, 30), DemoTimeline.At(day, 16, 30), [nadia.Id, bea.Id], DemoPeople.Admin.Calendar, IsMdt: true, TeamId: dermatology), "arrange the skin cancer team meeting");
        await seed.Send(t.Ago(2, 10, 0), nadia, new ReplyToMeetingCommand(ahead.Id, nameof(MeetingReply.Accepted), ahead.StartUtc, ahead.EndUtc, nadia.Calendar), "accept the skin cancer team meeting");
        seed.Tour["mdtToday"] = today.Id;
        seed.Tour["mdtAhead"] = ahead.Id;
        return (today, ahead);
    }

    /// <summary>
    /// The MDT lists: Ellis Varden to discuss and Aisha Khoury discussed this morning (her second biopsy too small),
    /// Harriet Quill presented by the lab's pathologist; Priya Holloway and Felix Moreton on the meeting ahead.
    /// </summary>
    public static async Task CreateMdtListsAsync(DemoSeed seed, MeetingResponse today, MeetingResponse ahead)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        async Task<Contracts.Mdt.Responses.MdtItemResponse> List(string patient, MeetingResponse meeting, string question, DateTime at) =>
            await seed.Send(at, nadia, new ListForMdtCommand(seed.Stories[patient].Id, meeting.Id, question, nadia.Actor), $"list {patient} for the MDT");

        var ellis = await List(EllisVarden, today, "Non-healing scaly lesion, left forearm: punch biopsy here first, or refer straight to the hospital?", t.Ago(5, 12, 0));
        seed.Tour["mdtItemToDiscuss"] = ellis.Id;
        var aisha = await List(AishaKhoury, today, "Two lesions, left shin: the second biopsy was too small to report. Repeat it, or refer to the hospital?", t.Ago(5, 12, 5));
        var harriet = await List(HarrietQuill, today, "Nodular BCC on biopsy: confirm with pathology before the referral is sent.", t.Ago(4, 12, 0));
        harriet = await seed.Send(t.Ago(4, 12, 5), nadia, new ChooseMdtPresenterCommand(harriet.Id, DemoPeople.Aldous.Id, harriet.Version, nadia.Actor), "choose the presenter");
        seed.Tour["mdtItemPresented"] = harriet.Id;
        await List(PriyaHolloway, ahead, "Histology due this week: agree the next step.", t.Ago(2, 12, 0));
        var felixSeen = DemoTimeline.At(t.WeekdayBack(1), 16, 45);
        await List(FelixMoreton, ahead, "Suspicious 9 mm lesion on the shin, dermoscopy uncertain: agree the next step.", felixSeen);

        await seed.Send(today.StartUtc.AddMinutes(20), nadia, new RecordMdtOutcomeCommand(aisha.Id,
            "Repeat the punch biopsy of the lower lesion. List again with both reports.", [nadia.Id, DemoPeople.Sandiford.Id], aisha.Version, nadia.Actor),
            "record the MDT's outcome");
    }

    /// <summary>
    /// Nadia Thornbury's blocks this week and next (lunch daily; Wednesday's triage; the Tuesday and Thursday biopsy
    /// lists; the Monday and Friday skin clinics), Ravi Aldous's (cut-up, reporting and lunch daily; Tuesday's and
    /// Thursday's quality checks) and the admin's (lunch daily; Monday's and Thursday's rota planning; Wednesday's
    /// governance review), and when seeded at a weekend, that weekend's sessions for the three, each made by its owner, leaving out any that would overlap a visit or meeting
    /// already in her diary (the app refuses a block over one; the light demo lets them overlap). Then Bea Sandiford's
    /// outreach clinics on the first two open days from today, from after this morning's MDT if she was at it.
    /// </summary>
    public static async Task CreateBlocksAsync(DemoSeed seed, MeetingResponse today, MeetingResponse ahead)
    {
        var t = seed.Timeline;
        var nadia = DemoPeople.Thornbury;
        var ravi = DemoPeople.Aldous;
        var admin = DemoPeople.Admin;
        var skipped = 0;
        async Task Block(DemoPerson who, DateOnly date, string type, (int H, int M) from, (int H, int M) to, string? note)
        {
            try
            {
                await seed.Send(t.Ago(14, 8, 30), who, new CreateCalendarBlockCommand(who.Id, seed.ActivityTypes[type],
                    DemoTimeline.At(date, from.H, from.M), DemoTimeline.At(date, to.H, to.M), note, who.Calendar), "add a calendar block");
            }
            catch (McpException e) when (e.InnerException is ConflictException)
            {
                skipped++;
            }
        }

        foreach (var week in new[] { 0, 7 })
            for (var day = 0; day < 5; day++)
            {
                var date = t.ThisWeek(week + day);
                if (!t.IsOpen(date))
                    continue;
                await Block(nadia, date, Lunch, (12, 30), (13, 15), null);
                if (day == 2)
                    await Block(nadia, date, Triage, (9, 0), (10, 30), "Referral triage");
                if (day is 1 or 3)
                    await Block(nadia, date, ProcedureList, (9, 0), (12, 0), "Biopsy list");
                if (day is 0 or 4)
                    await Block(nadia, date, Clinic, (13, 30), (17, 0), "Skin clinic");

                await Block(ravi, date, ProcedureList, (8, 30), (10, 30), "Cut-up");
                await Block(ravi, date, AdminTime, (11, 0), (12, 30), "Reporting");
                await Block(ravi, date, Lunch, (12, 30), (13, 15), null);
                if (day is 1 or 3)
                    await Block(ravi, date, AdminTime, (14, 0), (16, 0), "Quality checks");

                await Block(admin, date, Lunch, (12, 30), (13, 15), null);
                if (day is 0 or 3)
                    await Block(admin, date, AdminTime, (13, 30), (14, 30), "Rota planning");
                if (day == 2)
                    await Block(admin, date, AdminTime, (10, 0), (11, 0), "Governance review");
            }

        if (t.IsWeekend)
            foreach (var week in new[] { 0, 7 })
                foreach (var day in new[] { 5, 6 })
                {
                    var date = t.ThisWeek(week + day);
                    if (t.ClosedDays.Contains(date))
                        continue;
                    await Block(nadia, date, Clinic, (9, 0), (12, 0), "Weekend skin clinic");
                    await Block(nadia, date, AdminTime, (12, 0), (13, 0), "Letters");
                    await Block(ravi, date, ProcedureList, (9, 0), (11, 0), "Weekend cut-up");
                    await Block(ravi, date, AdminTime, (11, 0), (13, 0), "Weekend reporting");
                    await Block(admin, date, AdminTime, (9, 30), (12, 0), "Weekend cover");
                }

        // Bea Sandiford is out at a clinic all day today and the next open day, so the tour's team meeting can't have her
        // then: adding the team shows who's busy, and the suggestions move on to a time they're all free. Her MDTs those
        // days stay in her diary: the clinic is either side of them.
        var bea = DemoPeople.Sandiford;
        var meetings = new[] { today, ahead };
        foreach (var date in t.OpenDaysFromToday(2))
        {
            var from = DemoTimeline.At(date, 8, 0);
            foreach (var meeting in meetings.Where(m => UkTime.DateOf(m.StartUtc) == date).OrderBy(m => m.StartUtc).Append(null))
            {
                var to = meeting?.StartUtc ?? DemoTimeline.At(date, 18, 0);
                if (to - from >= TimeSpan.FromMinutes(30))
                    await seed.Send(t.Ago(10, 9, 0), bea, new CreateCalendarBlockCommand(bea.Id, seed.ActivityTypes[Clinic], from, to, "Outreach clinic", bea.Calendar),
                        "add Bea Sandiford's outreach clinic");
                if (meeting is not null)
                    from = meeting.EndUtc;
            }
        }

        seed.Counts["blocksLeftOutOverVisits"] = skipped;
    }

    /// <summary>
    /// Tobias Lund's own request for his data, received 26 days ago and his identity checked in person, so it's open and due
    /// within the week: the admin's Needs your attention has it (the light demo's seeded data request).
    /// </summary>
    public static async Task CreateDataRequestAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        var admin = DemoPeople.Admin;
        var received = t.DaysFrom(-26);
        var request = await seed.Send(t.Ago(26, 10, 0), admin,
            new CreateDataRequestCommand(seed.Patients[TobiasLund], received, true, null, null, admin.Actor), "log Tobias Lund's data request");
        await seed.Send(t.Ago(26, 10, 30), admin,
            new RecordIdentityCheckCommand(request.Id, received, "InPerson", null, request.Version, admin.Actor), "record the identity check");
        seed.Tour["dataRequest"] = request.Id;
    }

    /// <summary>
    /// The service alerts the admin's rules raise now (MC19): the job's own run, as the scheduler would have run it this
    /// morning (referrals not triaged, lab cases overdue). The notifications from more than a week ago have been read.
    /// </summary>
    public static async Task FinishAsync(DemoSeed seed)
    {
        var t = seed.Timeline;
        seed.Services.At(t.HoursAgo(1), DemoPeople.Admin.ActivityActor);
        var alerts = await seed.Services.Run(async sp =>
        {
            var job = sp.GetRequiredService<IEnumerable<IScheduledJob>>().Single(j => j.Name == ServiceAlertsJob.JobName);
            return await job.RunAsync(t.HoursAgo(1), seed.Ct);
        });
        seed.Counts["serviceAlerts"] = alerts;

        var readBefore = t.Ago(7, 0, 0);
        await seed.Db(db => db.Notifications.Where(n => n.CreatedAtUtc < readBefore && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAtUtc, x => x.CreatedAtUtc.AddHours(2)), seed.Ct));
    }
}
