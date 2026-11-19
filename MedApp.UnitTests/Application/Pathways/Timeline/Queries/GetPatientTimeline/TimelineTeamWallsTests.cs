using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Appointments;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Pathways.Timeline.Queries.GetPatientTimeline;

/// <summary>
/// The team walls on a patient's timeline and files (section 3.2's MC4 rows of <c>docs/multi-clinic-plan.md</c>), each
/// filter run in memory: another team's entries and files are left out, not summarised; admins see every team's.
/// </summary>
[TestFixture]
public sealed class TimelineTeamWallsTests
{
    private static readonly Guid Dermatology = Guid.Parse("0000005a-0000-0000-0000-0000000000d1");
    private static readonly Guid Gynaecology = Guid.Parse("0000005a-0000-0000-0000-0000000000d2");
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);

    private static readonly TeamScope DermatologyStaff = new(Guid.NewGuid(), [Dermatology], seesAllTeams: false);
    private static readonly TeamScope Admin = new(Guid.NewGuid(), [], seesAllTeams: true);

    private PatientPathway _mine = null!;
    private PatientPathway _theirs = null!;
    private List<PathwayAccess> _grants = null!;

    [SetUp]
    public void SetUp()
    {
        _mine = new PatientPathway { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Dermatology, Title = "Mine" };
        _theirs = new PatientPathway { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, Title = "Theirs" };
        _grants =
        [
            PathwayAccess.Owner(_mine.Id, PatientId, Dermatology, Now),
            PathwayAccess.Owner(_theirs.Id, PatientId, Gynaecology, Now),
        ];
    }

    private static GetPatientTimelineQuery Query(TeamScope scope) => new(PatientId, null, 50, null, null, false, scope);

    private static PatientPathwayAuditEntry Audit(Guid pathwayId, Guid teamId) => new()
    {
        Id = Guid.NewGuid(), PatientId = PatientId, PatientPathwayId = pathwayId, TeamId = teamId,
        Action = PatientPathwayAuditAction.NoteAdded, AtUtc = Now,
    };

    [Test]
    public void AuditEntries_OnlyThoseOfPathwaysSeenAndOfTheViewersTeamWithoutOne()
    {
        PatientPathwayAuditEntry[] entries =
        [
            Audit(_mine.Id, Dermatology),
            Audit(_theirs.Id, Gynaecology),
            Audit(PatientPathwayAuditEntry.NoPathway, Dermatology),
            Audit(PatientPathwayAuditEntry.NoPathway, Gynaecology),
        ];

        var rows = PatientTimelineSearch.Page(entries.AsQueryable(), new[] { _mine, _theirs }.AsQueryable(), Query(DermatologyStaff), _grants.AsQueryable())
            .ToList();

        rows.Select(r => r.Entry).Should().BeEquivalentTo([entries[0], entries[2]]);
        PatientTimelineSearch.Page(entries.AsQueryable(), new[] { _mine, _theirs }.AsQueryable(), Query(Admin)).Should().HaveCount(4);
    }

    [Test]
    public void AppointmentEvents_OnlyThoseOfPathwaysSeenAndOfTheViewersTeamWithoutOne()
    {
        AppointmentEvent[] events =
        [
            new() { Id = Guid.NewGuid(), PatientId = PatientId, PathwayId = _mine.Id, TeamId = Dermatology, AtUtc = Now },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, PathwayId = _theirs.Id, TeamId = Gynaecology, AtUtc = Now },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, AtUtc = Now },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Dermatology, AtUtc = Now },
        ];

        var rows = AppointmentTimelineSearch.Page(events.AsQueryable(), new[] { _mine, _theirs }.AsQueryable(), Query(DermatologyStaff), _grants.AsQueryable())
            .ToList();

        rows.Select(r => r.Event).Should().BeEquivalentTo([events[0], events[3]]);
    }

    [Test]
    public void ReferralEvents_OnlyTheViewersTeamsOrOnAPathwaySeen()
    {
        ReferralEvent[] events =
        [
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Dermatology, AtUtc = Now },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, AtUtc = Now },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, PathwayId = _mine.Id, AtUtc = Now },
        ];

        var rows = ReferralTimelineSearch.Page(events.AsQueryable(), Array.Empty<Referral>().AsQueryable(), Query(DermatologyStaff), _grants.AsQueryable())
            .ToList();

        rows.Select(r => r.Event).Should().BeEquivalentTo([events[0], events[2]]);
    }

    [Test]
    public void Files_APatientsList_HoldsOnlyTheViewersTeamsAndThoseOnPathwaysSeen()
    {
        Attachment[] files =
        [
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Dermatology, PathwayId = _mine.Id },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, PathwayId = _theirs.Id },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology },
            new() { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Dermatology },
        ];
        var query = new GetAttachmentsQuery(PatientId, null, null, null, false, Guid.NewGuid(), false, true, DermatologyStaff);

        AttachmentSearch.Filter(files.AsQueryable(), query, _grants.AsQueryable()).Should().BeEquivalentTo([files[0], files[3]]);
        AttachmentSearch.Filter(files.AsQueryable(), query with { Scope = Admin }).Should().HaveCount(4);
    }

    [Test]
    public async Task Files_OneBehindTheWall_IsA404AsIfItWerentThere_AndOneSeenIsServed()
    {
        var access = new Mock<IPathwayAccessRepository>();
        access.Setup(a => a.GetLiveAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<TeamScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, TeamScope scope, CancellationToken _) =>
                (IReadOnlyList<PathwayAccess>)_grants.Where(g => ids.Contains(g.PathwayId)).Where(TeamAccess.LiveGrantFor(scope).Compile()).ToList());
        var activity = new RecordingActivityLog();
        var boundary = new TeamBoundary(access.Object, new Mock<ITeamRepository>().Object, activity);
        var theirs = new Attachment { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, PathwayId = _theirs.Id };
        var mine = new Attachment { Id = Guid.NewGuid(), PatientId = PatientId, TeamId = Gynaecology, PathwayId = _mine.Id };

        var act = () => AttachmentWalls.EnsureVisibleAsync(theirs, boundary, DermatologyStaff, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage(AttachmentWalls.NotFoundMessage(theirs.Id));
        activity.Once.Should().ContainSingle().Which.Details.Should().Contain(theirs.Id.ToString());
        await AttachmentWalls.EnsureVisibleAsync(mine, boundary, DermatologyStaff, CancellationToken.None);
        await AttachmentWalls.EnsureVisibleAsync(theirs, boundary, Admin, CancellationToken.None);
    }
}
