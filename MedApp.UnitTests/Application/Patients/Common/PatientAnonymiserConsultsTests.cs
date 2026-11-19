using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Consults;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.ConsultsTestConstants;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>Anonymising a patient with consults (MC25): their free text goes, open ones end, and so does the asked teams' reading.</summary>
[TestFixture]
public sealed class PatientAnonymiserConsultsTests
{
    private static readonly DateTime AnonymisedAt = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Apply_Consults_LoseTheirTextAndOpenOnesEndWithTheirGrants()
    {
        var pathway = CreateDermatologyPathway();
        var open = CreateConsult(pathway);
        var replied = CreateConsult(pathway, ConsultStatus.Replied);
        var closed = CreateConsult(pathway, ConsultStatus.Closed);
        var grant = PathwayAccess.Consult(pathway.Id, pathway.PatientId, Lab.Id, PathwaysTestConstants.Now);
        var baseRecord = PrivacyTestConstants.CreateRecord(null, pathway);
        var record = new PatientRecord
        {
            Patient = baseRecord.Patient,
            Pathways = baseRecord.Pathways,
            PathwayHistory = baseRecord.PathwayHistory,
            NoteVersions = [],
            NoteDeletionRequests = [],
            Activity = baseRecord.Activity,
            Appointments = baseRecord.Appointments,
            AppointmentHistory = baseRecord.AppointmentHistory,
            PatientMessages = baseRecord.PatientMessages,
            Notifications = baseRecord.Notifications,
            Attachments = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = [],
            ConsentRecords = [],
            Consults = [open, replied, closed],
            ConsultGrants = [grant],
        };
        var by = new ActivityActor(PathwaysTestConstants.Admin.UserId, PathwaysTestConstants.Admin.Username, null);

        PatientAnonymiser.Apply(record, AnonymisedAt, by);

        record.Consults.Should().AllSatisfy(c => c.Question.Should().Be(PatientAnonymiser.RemovedText));
        replied.Reply.Should().Be(PatientAnonymiser.RemovedText);
        closed.Reply.Should().Be(PatientAnonymiser.RemovedText);
        open.Status.Should().Be(ConsultStatus.Withdrawn);
        replied.Status.Should().Be(ConsultStatus.Closed);
        open.ClosedByUsername.Should().Be(PathwaysTestConstants.Admin.Username);
        open.ClosedAtUtc.Should().Be(AnonymisedAt);
        open.Version.Should().Be(2);
        closed.ClosedAtUtc.Should().NotBe(AnonymisedAt, "a closed consult stays as it was");
        grant.EndedAtUtc.Should().Be(AnonymisedAt);
    }
}
