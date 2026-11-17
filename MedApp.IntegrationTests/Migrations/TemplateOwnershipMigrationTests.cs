using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>MadeCalendarTemplatesPersonal</c> against a real SQL Server: templates that were organisation-wide become each
/// member of staff's own copies (a template only a leaver used goes to the admin), and every day of everyone's
/// calendar, past and future, projects exactly as it did before. The
/// rows are synthetic and inserted with SQL at the migration before (the model has moved on since), and the "before"
/// calendars are projected from those same rows in memory, the "after" ones from the database by the real repository.
/// </summary>
[TestFixture]
public sealed class TemplateOwnershipMigrationTests
{
    private const string Before = "20261116195926_AddedAppointmentRowVersionAndOpenStageIndex";
    private const string Migration = "20261116212859_MadeCalendarTemplatesPersonal";

    private static readonly Guid Lunch = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid AdminTime = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid Training = Guid.Parse("22222222-0000-0000-0000-000000000003");

    private static readonly Guid AdminUserId = Guid.Parse("0000000e-0000-0000-0000-000000000001");
    private static readonly Guid LaterAdminUserId = Guid.Parse("0000000e-0000-0000-0000-000000000002");
    private static readonly Guid StaffA = Guid.Parse("0000000e-0000-0000-0000-00000000000a");
    private static readonly Guid StaffB = Guid.Parse("0000000e-0000-0000-0000-00000000000b");

    /// <summary>Someone who has left: their account is deleted, but their default week rows and override remain.</summary>
    private static readonly Guid Leaver = Guid.Parse("0000000e-0000-0000-0000-00000000000c");

    /// <summary>"Today" for the projections: Monday 16 November 2026. The range runs from before the clocks went back.</summary>
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly DateOnly From = new(2026, 10, 19);
    private static readonly DateOnly To = new(2026, 12, 4);

    private MedAppDbContext _context = null!;
    private Seed _seed = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_TemplateOwnership_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        _seed = new Seed();
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_TemplatesSharedByTwoPeople_EachCalendarProjectsExactlyAsBefore()
    {
        await _seed.InsertAsync(_context, withAdmin: true);
        // The leaver's calendar too, though nobody can open it now: their rows keep pointing at the templates they used.
        var people = new[] { StaffA, StaffB, Leaver };
        var before = people.ToDictionary(id => id, id => Project(_seed.DataFor(id)));

        // To the latest, as the repository reads meetings too (the migrations after this one only add).
        await _context.Database.MigrateAsync();

        var repository = new CalendarRepository(_context);
        foreach (var staffUserId in people)
        {
            var after = Project(await repository.GetCalendarDataAsync(staffUserId, From, To, CancellationToken.None));
            after.Days.Should().Equal(before[staffUserId].Days, $"every day of {staffUserId}'s calendar keeps its template");
            after.Blocks.Should().Equal(before[staffUserId].Blocks, $"every template block of {staffUserId}'s calendar stays");
        }

        // The removed lunch (an exception) is still off that day, and the past days kept their old lunch.
        before[StaffA].Blocks.Should().NotContain(b => b.Date == _seed.ExceptionDate && b.ActivityTypeId == Lunch);
        before[StaffA].Blocks.Should().Contain(b => b.Date == From && b.Start == new TimeOnly(12, 0));
        before[StaffA].Blocks.Should().Contain(b => b.Date == Today.AddDays(1) && b.Start == new TimeOnly(12, 30));
        before[Leaver].Days.Should().Contain(d => d.DayTemplateName == Seed.LeaverDayName);
    }

    [Test]
    public async Task Up_TemplatesSharedByTwoPeople_GivesEachTheirOwnCopiesAndTheRestToTheFirstAdmin()
    {
        await _seed.InsertAsync(_context, withAdmin: true);

        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var weeks = await _context.WeekTemplates.AsNoTracking().Include(t => t.Days).ToListAsync();
        var days = await _context.DayTemplates.AsNoTracking().Include(t => t.Blocks).ToListAsync();
        var staffWeeks = await _context.StaffWeekTemplates.AsNoTracking().ToListAsync();

        // The standard week: one each for A and B (the first by ID keeps the original), every dated row copied. The
        // leaver used it too, but gets no copy.
        var standardWeeks = weeks.Where(w => w.Name == Seed.StandardWeekName).ToList();
        standardWeeks.Select(w => w.OwnerUserId).Should().BeEquivalentTo([StaffA, StaffB]);
        standardWeeks.Should().Contain(w => w.Id == _seed.StandardWeekId && w.OwnerUserId == StaffA);
        standardWeeks.Should().AllSatisfy(w => w.Days.Should().HaveCount(_seed.WeekDays.Count(d => d.WeekTemplateId == _seed.StandardWeekId)));
        staffWeeks.Where(s => s.StaffUserId != Leaver).Should().AllSatisfy(s =>
            standardWeeks.Single(w => w.Id == s.WeekTemplateId).OwnerUserId.Should().Be(s.StaffUserId));

        // Each week's days use its owner's own day templates.
        foreach (var week in standardWeeks)
            week.Days.Should().AllSatisfy(d => days.Single(t => t.Id == d.DayTemplateId).OwnerUserId.Should().Be(week.OwnerUserId));

        // The clinic day: A and B each have one, with every dated block row (the closed one too).
        days.Where(t => t.Name == Seed.ClinicDayName).Select(t => t.OwnerUserId).Should().BeEquivalentTo([StaffA, StaffB]);
        days.Where(t => t.Name == Seed.ClinicDayName).Should().AllSatisfy(t =>
            t.Blocks.Select(b => (b.StartTime, b.ValidFrom, b.ValidTo)).Should().BeEquivalentTo(
                _seed.ClinicBlocks.Select(b => (b.StartTime, b.ValidFrom, b.ValidTo))));

        // The theatre day: A uses it by their override and from the week's Monday; B from the week's Monday too
        // (any dated row counts), so each has one.
        days.Where(t => t.Name == Seed.TheatreDayName).Select(t => t.OwnerUserId).Should().BeEquivalentTo([StaffA, StaffB]);

        // Nobody used these, or only the leaver: the longest-standing admin has them, not the later one, so an admin can
        // still find them.
        weeks.Should().ContainSingle(t => t.Name == Seed.UnusedWeekName).Which.OwnerUserId.Should().Be(AdminUserId);
        days.Should().ContainSingle(t => t.Name == Seed.UnusedDayName).Which.OwnerUserId.Should().Be(AdminUserId);
        weeks.Should().ContainSingle(t => t.Name == Seed.LeaverWeekName).Which.Should().BeEquivalentTo(new { Id = _seed.LeaverWeekId, OwnerUserId = AdminUserId });
        days.Should().ContainSingle(t => t.Name == Seed.LeaverDayName).Which.Should().BeEquivalentTo(new { Id = _seed.LeaverDayId, OwnerUserId = AdminUserId });

        // The leaver's rows still point at what they used: the originals, whoever owns them now.
        staffWeeks.Where(s => s.StaffUserId == Leaver).Select(s => s.WeekTemplateId)
            .Should().BeEquivalentTo([_seed.StandardWeekId, _seed.LeaverWeekId]);
        (await _context.CalendarDayOverrides.AsNoTracking().SingleAsync(o => o.StaffUserId == Leaver)).DayTemplateId.Should().Be(_seed.LeaverDayId);

        // Each member of staff's exception points at a block of their own clinic day; the leaver's at the original's.
        var exceptions = await _context.CalendarBlockExceptions.AsNoTracking().Include(e => e.DayTemplateBlock).ToListAsync();
        exceptions.Should().HaveCount(3);
        exceptions.Where(e => e.StaffUserId != Leaver).Should().AllSatisfy(e =>
            days.Single(t => t.Id == e.DayTemplateBlock.DayTemplateId).OwnerUserId.Should().Be(e.StaffUserId));
        exceptions.Single(e => e.StaffUserId == Leaver).DayTemplateBlock.DayTemplateId.Should().Be(_seed.LeaverDayId);
    }

    [Test]
    public async Task Up_NoAdmin_DeletesTheTemplatesNoMemberOfStaffUsed()
    {
        await _seed.InsertAsync(_context, withAdmin: false);

        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await _context.WeekTemplates.AnyAsync(t => t.Name == Seed.UnusedWeekName)).Should().BeFalse();
        (await _context.DayTemplates.AnyAsync(t => t.Name == Seed.UnusedDayName)).Should().BeFalse();
        (await _context.DayTemplates.CountAsync(t => t.Name == Seed.ClinicDayName)).Should().Be(2);
        (await _context.DayTemplates.AnyAsync(t => t.OwnerUserId == Guid.Empty)).Should().BeFalse();

        // Only the leaver used these: they go, with the leaver's rows that pointed at them; the leaver's row for the
        // standard week (still A's) stays.
        (await _context.WeekTemplates.AnyAsync(t => t.Name == Seed.LeaverWeekName)).Should().BeFalse();
        (await _context.DayTemplates.AnyAsync(t => t.Name == Seed.LeaverDayName)).Should().BeFalse();
        (await _context.StaffWeekTemplates.Where(s => s.StaffUserId == Leaver).Select(s => s.WeekTemplateId).ToListAsync())
            .Should().Equal(_seed.StandardWeekId);
        (await _context.CalendarDayOverrides.AnyAsync(o => o.StaffUserId == Leaver)).Should().BeFalse();
        (await _context.CalendarBlockExceptions.AnyAsync(e => e.StaffUserId == Leaver)).Should().BeFalse();
    }

    [Test]
    public async Task Up_ThenATemplateInsertedWithoutAnOwner_IsRefused()
    {
        // As the API from before this migration would, during a rollout or after a rollback: there's no default to make
        // a template owned by nobody.
        await _seed.InsertAsync(_context, withAdmin: true);
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        var act = () => _context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO DayTemplates (Id, Name, IsArchived) VALUES ({Guid.NewGuid()}, {"Synthetic old-code day"}, 0)");

        (await act.Should().ThrowAsync<SqlException>()).Which.Message.Should().Contain("OwnerUserId");
    }

    [Test]
    public async Task Down_AfterUp_KeepsEveryTemplateWithUniqueNamesAndCalendarsPointingWhereTheyDid()
    {
        await _seed.InsertAsync(_context, withAdmin: true);
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);
        var staffWeeksUp = await _context.StaffWeekTemplates.AsNoTracking().OrderBy(s => s.Id).Select(s => s.WeekTemplateId).ToListAsync();

        await migrator.MigrateAsync(Before);

        var names = await QueryAsync("SELECT Name FROM DayTemplates");
        names.Should().OnlyHaveUniqueItems().And.HaveCount(6);
        names.Should().Contain(Seed.ClinicDayName).And.Contain(n => n.StartsWith(Seed.ClinicDayName + " ("));
        (await QueryAsync("SELECT Name FROM WeekTemplates")).Should().OnlyHaveUniqueItems().And.HaveCount(4);
        (await QueryAsync("SELECT CONVERT(nvarchar(36), WeekTemplateId) FROM StaffWeekTemplates ORDER BY Id"))
            .Select(Guid.Parse).Should().Equal(staffWeeksUp);

        // And up again.
        await migrator.MigrateAsync(Migration);
        (await _context.DayTemplates.CountAsync()).Should().Be(6);
    }

    private async Task<List<string>> QueryAsync(string sql)
    {
        var connection = (SqlConnection)_context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            var values = new List<string>();
            while (await reader.ReadAsync())
                values.Add(reader.GetString(0));
            return values;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed record ProjectedDay(DateOnly Date, string? DayTemplateName, bool IsOverridden);

    private sealed record ProjectedTemplateBlock(DateOnly Date, TimeOnly Start, TimeOnly End, DateTime StartUtc, Guid ActivityTypeId, string Note);

    private static (List<ProjectedDay> Days, List<ProjectedTemplateBlock> Blocks) Project(CalendarData data) =>
    (
        CalendarProjection.Days(data, Today).Select(d => new ProjectedDay(d.Date, d.DayTemplateName, d.IsDayTemplateOverridden)).ToList(),
        CalendarProjection.TemplateBlocks(data)
            .Select(b => new ProjectedTemplateBlock(b.Date, b.Source.StartTime, b.Source.EndTime, b.StartUtc, b.Source.ActivityTypeId, b.Source.Note))
            .ToList()
    );

    /// <summary>
    /// The calendars before the migration: one standard week, used by two members of staff (B only from a later date),
    /// whose Monday changed to the theatre day part-way; a clinic day whose lunch moved (a closed row and a new one); A's
    /// override to the theatre day; an exception each; a day and a week template nobody uses; and a leaver (no account)
    /// who used the standard week, then a week and a day template nobody else used, with an override and an exception.
    /// </summary>
    private sealed class Seed
    {
        public const string ClinicDayName = "Synthetic clinic day";
        public const string TheatreDayName = "Synthetic theatre day";
        public const string UnusedDayName = "Synthetic unused day";
        public const string StandardWeekName = "Synthetic standard week";
        public const string UnusedWeekName = "Synthetic unused week";
        public const string LeaverDayName = "Synthetic leaver day";
        public const string LeaverWeekName = "Synthetic leaver week";

        private static readonly DateOnly LongAgo = new(2026, 1, 1);
        private static readonly DateOnly LunchMoved = new(2026, 11, 2);
        private static readonly DateOnly MondayChanged = new(2026, 11, 9);

        public Guid ClinicDayId { get; } = Guid.NewGuid();
        public Guid TheatreDayId { get; } = Guid.NewGuid();
        public Guid UnusedDayId { get; } = Guid.NewGuid();
        public Guid StandardWeekId { get; } = Guid.NewGuid();
        public Guid UnusedWeekId { get; } = Guid.NewGuid();
        public Guid LeaverDayId { get; } = Guid.NewGuid();
        public Guid LeaverWeekId { get; } = Guid.NewGuid();

        /// <summary>A's lunch is removed this day (an exception); B's training on another.</summary>
        public DateOnly ExceptionDate { get; } = new(2026, 11, 18);

        public DateOnly OverrideDate { get; } = new(2026, 10, 28);

        public List<DayTemplateBlock> ClinicBlocks { get; } = [];
        public List<DayTemplateBlock> AllBlocks { get; } = [];
        public List<WeekTemplateDay> WeekDays { get; } = [];
        public List<StaffWeekTemplate> StaffWeeks { get; } = [];
        public List<CalendarDayOverride> Overrides { get; } = [];
        public List<CalendarBlockException> Exceptions { get; } = [];

        public Seed()
        {
            var oldLunch = Block(ClinicDayId, Lunch, 12, 0, 13, 0, LongAgo, LunchMoved.AddDays(-1));
            var newLunch = Block(ClinicDayId, Lunch, 12, 30, 13, 30, LunchMoved, null);
            var training = Block(ClinicDayId, Training, 9, 0, 10, 0, LongAgo, null, "Synthetic training note");
            ClinicBlocks.AddRange([oldLunch, newLunch, training]);
            AllBlocks.AddRange(ClinicBlocks);
            AllBlocks.Add(Block(TheatreDayId, AdminTime, 14, 0, 15, 0, LongAgo, null));
            AllBlocks.Add(Block(UnusedDayId, AdminTime, 8, 0, 9, 0, LongAgo, null));
            var leaverBlock = Block(LeaverDayId, Training, 10, 0, 11, 0, LongAgo, null);
            AllBlocks.Add(leaverBlock);

            foreach (var weekday in CalendarSetupMapper.Weekdays)
            {
                if (weekday == DayOfWeek.Monday)
                {
                    WeekDays.Add(WeekDay(StandardWeekId, weekday, ClinicDayId, LongAgo, MondayChanged.AddDays(-1)));
                    WeekDays.Add(WeekDay(StandardWeekId, weekday, TheatreDayId, MondayChanged, null));
                }
                else
                {
                    WeekDays.Add(WeekDay(StandardWeekId, weekday, ClinicDayId, LongAgo, null));
                }
            }

            var unusedWeekDay = WeekDay(UnusedWeekId, DayOfWeek.Friday, UnusedDayId, LongAgo, null);
            WeekDays.Add(unusedWeekDay);
            WeekDays.Add(WeekDay(LeaverWeekId, DayOfWeek.Wednesday, LeaverDayId, LongAgo, null));

            StaffWeeks.Add(new StaffWeekTemplate { Id = Guid.NewGuid(), StaffUserId = StaffA, WeekTemplateId = StandardWeekId, ValidFrom = LongAgo });
            StaffWeeks.Add(new StaffWeekTemplate { Id = Guid.NewGuid(), StaffUserId = StaffB, WeekTemplateId = StandardWeekId, ValidFrom = new DateOnly(2026, 10, 26) });
            StaffWeeks.Add(new StaffWeekTemplate { Id = Guid.NewGuid(), StaffUserId = Leaver, WeekTemplateId = StandardWeekId, ValidFrom = LongAgo, ValidTo = new DateOnly(2026, 6, 30) });
            StaffWeeks.Add(new StaffWeekTemplate { Id = Guid.NewGuid(), StaffUserId = Leaver, WeekTemplateId = LeaverWeekId, ValidFrom = new DateOnly(2026, 7, 1) });

            Overrides.Add(new CalendarDayOverride { Id = Guid.NewGuid(), StaffUserId = StaffA, Date = OverrideDate, DayTemplateId = TheatreDayId });
            Exceptions.Add(new CalendarBlockException { Id = Guid.NewGuid(), StaffUserId = StaffA, Date = ExceptionDate, DayTemplateBlockId = newLunch.Id });
            Exceptions.Add(new CalendarBlockException { Id = Guid.NewGuid(), StaffUserId = StaffB, Date = new DateOnly(2026, 11, 3), DayTemplateBlockId = training.Id });
            Overrides.Add(new CalendarDayOverride { Id = Guid.NewGuid(), StaffUserId = Leaver, Date = new DateOnly(2026, 11, 2), DayTemplateId = LeaverDayId });
            Exceptions.Add(new CalendarBlockException { Id = Guid.NewGuid(), StaffUserId = Leaver, Date = new DateOnly(2026, 11, 11), DayTemplateBlockId = leaverBlock.Id });
        }

        private Dictionary<Guid, string> Names => new()
        {
            [ClinicDayId] = ClinicDayName,
            [TheatreDayId] = TheatreDayName,
            [UnusedDayId] = UnusedDayName,
            [LeaverDayId] = LeaverDayName
        };

        /// <summary>A person's calendar as the old rows give it, for the projection before the migration.</summary>
        public CalendarData DataFor(Guid staffUserId) =>
            CalendarData.Empty(staffUserId, From, To) with
            {
                StaffWeekTemplates = StaffWeeks.Where(s => s.StaffUserId == staffUserId).ToList(),
                WeekTemplateDays = WeekDays.Where(d => d.WeekTemplateId != UnusedWeekId).ToList(),
                DayTemplateBlocks = AllBlocks,
                DayOverrides = Overrides.Where(o => o.StaffUserId == staffUserId).ToList(),
                BlockExceptions = Exceptions.Where(e => e.StaffUserId == staffUserId).ToList(),
                DayTemplateNames = Names
            };

        public async Task InsertAsync(MedAppDbContext context, bool withAdmin)
        {
            var sql = new List<FormattableString>();

            // A and B are members of staff (the User role); the leaver has no account.
            var userRoleId = Guid.NewGuid();
            sql.Add($"INSERT INTO AspNetRoles (Id, Name, NormalizedName) VALUES ({userRoleId}, N'User', N'USER')");
            var people = new List<(Guid Id, string Name, DateTime Created, Guid RoleId)>
            {
                (StaffA, "synthetic.staff.a", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), userRoleId),
                (StaffB, "synthetic.staff.b", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), userRoleId)
            };
            if (withAdmin)
            {
                var adminRoleId = Guid.NewGuid();
                sql.Add($"INSERT INTO AspNetRoles (Id, Name, NormalizedName) VALUES ({adminRoleId}, N'Admin', N'ADMIN')");
                // The later admin is inserted first, so the earliest is found by when it was created, not by order.
                people.Add((LaterAdminUserId, "synthetic.later.admin", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), adminRoleId));
                people.Add((AdminUserId, "synthetic.first.admin", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), adminRoleId));
            }

            foreach (var (id, name, created, roleId) in people)
            {
                sql.Add($"""
                    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, CreatedAt, EmailConfirmed, PhoneNumberConfirmed,
                        TwoFactorEnabled, LockoutEnabled, AccessFailedCount, MustChangePassword, TwoFactorLockouts)
                    VALUES ({id}, {name}, {name.ToUpperInvariant()}, {created}, 0, 0, 0, 1, 0, 0, 0)
                    """);
                sql.Add($"INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES ({id}, {roleId})");
            }

            foreach (var (id, name) in new[] { (ClinicDayId, ClinicDayName), (TheatreDayId, TheatreDayName), (UnusedDayId, UnusedDayName), (LeaverDayId, LeaverDayName) })
                sql.Add($"INSERT INTO DayTemplates (Id, Name, IsArchived) VALUES ({id}, {name}, 0)");
            foreach (var b in AllBlocks)
                sql.Add($"""
                    INSERT INTO DayTemplateBlocks (Id, DayTemplateId, ActivityTypeId, StartTime, EndTime, Note, ValidFrom, ValidTo)
                    VALUES ({b.Id}, {b.DayTemplateId}, {b.ActivityTypeId}, {b.StartTime.ToTimeSpan()}, {b.EndTime.ToTimeSpan()}, {b.Note},
                        {b.ValidFrom.ToDateTime(TimeOnly.MinValue)}, {b.ValidTo?.ToDateTime(TimeOnly.MinValue)})
                    """);
            foreach (var (id, name) in new[] { (StandardWeekId, StandardWeekName), (UnusedWeekId, UnusedWeekName), (LeaverWeekId, LeaverWeekName) })
                sql.Add($"INSERT INTO WeekTemplates (Id, Name, IsArchived) VALUES ({id}, {name}, 0)");
            foreach (var d in WeekDays)
                sql.Add($"""
                    INSERT INTO WeekTemplateDays (Id, WeekTemplateId, DayOfWeek, DayTemplateId, ValidFrom, ValidTo)
                    VALUES ({d.Id}, {d.WeekTemplateId}, {(int)d.DayOfWeek}, {d.DayTemplateId},
                        {d.ValidFrom.ToDateTime(TimeOnly.MinValue)}, {d.ValidTo?.ToDateTime(TimeOnly.MinValue)})
                    """);
            foreach (var s in StaffWeeks)
                sql.Add($"""
                    INSERT INTO StaffWeekTemplates (Id, StaffUserId, WeekTemplateId, ValidFrom, ValidTo)
                    VALUES ({s.Id}, {s.StaffUserId}, {s.WeekTemplateId}, {s.ValidFrom.ToDateTime(TimeOnly.MinValue)}, {s.ValidTo?.ToDateTime(TimeOnly.MinValue)})
                    """);
            foreach (var o in Overrides)
                sql.Add($"INSERT INTO CalendarDayOverrides (Id, StaffUserId, Date, DayTemplateId) VALUES ({o.Id}, {o.StaffUserId}, {o.Date.ToDateTime(TimeOnly.MinValue)}, {o.DayTemplateId})");
            foreach (var e in Exceptions)
                sql.Add($"INSERT INTO CalendarBlockExceptions (Id, StaffUserId, Date, DayTemplateBlockId) VALUES ({e.Id}, {e.StaffUserId}, {e.Date.ToDateTime(TimeOnly.MinValue)}, {e.DayTemplateBlockId})");

            foreach (var statement in sql)
                await context.Database.ExecuteSqlInterpolatedAsync(statement);
        }

        private DayTemplateBlock Block(
            Guid dayTemplateId, Guid activityTypeId, int startHour, int startMinute, int endHour, int endMinute,
            DateOnly validFrom, DateOnly? validTo, string note = "") => new()
        {
            Id = Guid.NewGuid(),
            DayTemplateId = dayTemplateId,
            ActivityTypeId = activityTypeId,
            StartTime = new TimeOnly(startHour, startMinute),
            EndTime = new TimeOnly(endHour, endMinute),
            Note = note,
            ValidFrom = validFrom,
            ValidTo = validTo
        };

        private static WeekTemplateDay WeekDay(Guid weekTemplateId, DayOfWeek dayOfWeek, Guid dayTemplateId, DateOnly validFrom, DateOnly? validTo) => new()
        {
            Id = Guid.NewGuid(),
            WeekTemplateId = weekTemplateId,
            DayOfWeek = dayOfWeek,
            DayTemplateId = dayTemplateId,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
    }
}
