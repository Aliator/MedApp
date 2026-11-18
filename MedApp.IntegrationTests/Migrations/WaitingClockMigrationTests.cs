using FluentAssertions;
using MedApp.Domain.Specialties;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>AddedWaitingTimeClock</c> against a real SQL Server: every specialty gets the default targets; every referral its
/// clock from today's table, counted from the day received; one closed without a pathway is stopped on the UK day it
/// was decided, for the matching reason; one accepted into a pathway since discharged is stopped as discharged; one
/// still open keeps running. Down takes the clock away and keeps the referrals. Rows are synthetic, inserted and read
/// with SQL, as later migrations may add columns (AGENTS.md, Migrations).
/// </summary>
[TestFixture]
public sealed class WaitingClockMigrationTests
{
    private const string Before = "20261118151952_AddedClinicSessions";
    private const string Migration = "20261118154709_AddedWaitingTimeClock";

    private static readonly Guid Patient = Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000c1");
    private static readonly Guid Pathway = Guid.Parse("cccccccc-0000-0000-0000-0000000000c1");
    private static readonly Guid Awaiting = Guid.Parse("dddddddd-0000-0000-0000-0000000000c1");
    private static readonly Guid Rejected = Guid.Parse("dddddddd-0000-0000-0000-0000000000c2");
    private static readonly Guid Discharged = Guid.Parse("dddddddd-0000-0000-0000-0000000000c3");
    private static readonly Guid Registrar = Guid.Parse("eeeeeeee-0000-0000-0000-0000000000c1");

    // 23:30 UTC on 1 July is 00:30 on 2 July in the UK (BST): the stop day is the UK day.
    private static readonly DateTime DecidedAt = new(2026, 7, 1, 23, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 7, 10, 10, 0, 0, DateTimeKind.Utc);

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_WaitingClock_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Patients (Id, FirstName, LastName, DateOfBirth, CreatedAt, LastUpdated) VALUES
                ({Patient}, N'Synthetic', N'Person', '1980-05-06', {Now}, {Now});
            INSERT INTO PatientPathways (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TemplateName, CreatedAt, LastUpdated) VALUES
                ({Pathway}, N'Synthetic pathway', N'', {Now}, 2, 5, {Patient}, N'Synthetic template', {Now}, {DecidedAt});
            INSERT INTO Referrals (Id, PatientId, SpecialtyId, ReceivedOn, ReferrerPractice, RequestedPriority, Reason, Status,
                                   RegisteredAtUtc, RegisteredByUserId, RegisteredByUsername, DecidedAtUtc, PathwayId, Version, LastUpdated) VALUES
                ({Awaiting}, {Patient}, {Specialty.GeneralId}, '2026-07-01', N'Synthetic Lane Surgery', 3, N'Synthetic reason', 0,
                 {Now}, {Registrar}, N'b.synthetic', NULL, NULL, 1, {Now}),
                ({Rejected}, {Patient}, {Specialty.GeneralId}, '2026-06-20', N'Synthetic Lane Surgery', 1, N'Synthetic reason', 2,
                 {Now}, {Registrar}, N'b.synthetic', {DecidedAt}, NULL, 2, {Now}),
                ({Discharged}, {Patient}, {Specialty.GeneralId}, '2026-06-01', N'Synthetic Lane Surgery', 2, N'Synthetic reason', 1,
                 {Now}, {Registrar}, N'b.synthetic', {DecidedAt}, {Pathway}, 2, {Now});
            """);
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_GivesSpecialtiesTheirTargetsAndReferralsTheirClocks()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await TextAsync($"""
            SELECT CONCAT(TargetDaysUrgent, ',', TargetDaysHigh, ',', TargetDaysMedium, ',', TargetDaysLow) AS Value
            FROM Specialties WHERE Id = {Specialty.GeneralId}
            """)).Should().Be("14,28,126,126");

        (await ClockAsync(Awaiting)).Should().Be("14,2026-07-01,2026-07-15,0,,");
        (await ClockAsync(Rejected)).Should().Be("126,2026-06-20,2026-10-24,0,2026-07-02,3");
        (await ClockAsync(Discharged)).Should().Be("28,2026-06-01,2026-06-29,0,2026-07-02,1");
    }

    [Test]
    public async Task Down_RemovesTheClock_KeepsTheReferrals_AndUpAgainWorks()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.tables WHERE name = N'ReferralClockPauses'")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM sys.columns WHERE name IN (N'TargetDays', N'StopsClock', N'TargetDaysUrgent')")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Referrals")).Should().Be(3);

        await migrator.MigrateAsync(Migration);
        (await ClockAsync(Awaiting)).Should().Be("14,2026-07-01,2026-07-15,0,,");
    }

    private Task<string> ClockAsync(Guid id) => TextAsync($"""
        SELECT CONCAT(TargetDays, ',', CONVERT(char(10), WaitStartOn, 23), ',', CONVERT(char(10), TargetOn, 23), ',', PausedDays, ',',
                      CONVERT(char(10), StoppedOn, 23), ',', StopReason) AS Value
        FROM Referrals WHERE Id = {id}
        """);

    private Task<string> TextAsync(FormattableString sql) => _context.Database.SqlQuery<string>(sql).SingleAsync();

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();
}
