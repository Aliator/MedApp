using FluentAssertions;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MedApp.IntegrationTests.Migrations;

/// <summary>
/// <c>RenamedProceduresToPathways</c> against a real SQL Server: every row is kept under the new table and column
/// names, the keys and indexes take their new names, the two stored words that are shown to people change (what a file
/// is attached to, and the placeholder title of an anonymised record's procedures) and nothing else does; down puts
/// all of it back, and up again works. The rows are synthetic, inserted with SQL at the migration before, by the old
/// names, and read with SQL, as a later migration may add columns to these tables (AGENTS.md, Migrations).
/// </summary>
[TestFixture]
public sealed class PathwayRenameMigrationTests
{
    private const string Before = "20261118063819_AddedMeetingIdToNotifications";
    private const string Migration = "20261118075440_RenamedProceduresToPathways";

    private static readonly Guid Patient = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid AnonymisedPatient = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Template = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Definition = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Procedure = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid AnonymisedProcedure = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Stage = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Note = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid User = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid Appointment = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    private static readonly Guid FileAdded = Guid.Parse("11110000-0000-0000-0000-000000000002");
    private static readonly Guid StageNotesSaved = Guid.Parse("11110000-0000-0000-0000-000000000004");
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    // A file's audit details as AttachmentAudit writes them, for a file whose own name holds the words being replaced
    // (System.Text.Json escapes its quotes), and stage notes that quote them: neither may change.
    private const string FileDetails =
        """{"attachmentId":"99990000-0000-0000-0000-000000000001","fileName":""attachedTo":"Procedure".pdf","contentType":"application/pdf","size":10,"attachedTo":"Procedure"}""";
    private const string NotesDetails = """{"from":"","to":"\"attachedTo\":\"Procedure\" as someone typed it"}""";

    private MedAppDbContext _context = null!;

    [SetUp]
    public async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>()
            .UseSqlServer(TestEnvironment.ServerConnectionString($"MedApp_PathwayRename_{Guid.NewGuid():N}"))
            .Options;
        _context = new MedAppDbContext(options);
        await _context.GetService<IMigrator>().MigrateAsync(Before);
        await SeedAsync();
    }

    [TearDown]
    public async Task DropDatabaseAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Test]
    public async Task Up_EveryRow_IsKeptUnderTheNewNames()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await TextAsync($"SELECT Title AS Value FROM PatientPathways WHERE Id = {Procedure} AND TemplateId = {Template} AND PatientId = {Patient}"))
            .Should().Be("Synthetic procedure title");
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE Id = {Stage} AND PatientPathwayId = {Procedure} AND StageDefinitionId = {Definition}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayAssignments WHERE PatientPathwayId = {Procedure} AND UserId = {User}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayNotes WHERE Id = {Note} AND PatientPathwayId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayNoteVersions WHERE NoteId = {Note} AND PatientPathwayId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayNoteDeletionRequests WHERE NoteId = {Note} AND PatientPathwayId = {Procedure} AND Status = 0")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayAuditEntries WHERE PatientPathwayId = {Procedure} AND PathwayTitle = N'Synthetic procedure title'")).Should().Be(3);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStageDefinitions WHERE Id = {Definition}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplates WHERE Id = {Template} AND Name = N'Synthetic template'")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PathwayTemplateMaps WHERE TemplateId = {Template} AND StageDefinitionId = {Definition} AND StageOrder = 1")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Appointments WHERE Id = {Appointment} AND PathwayId = {Procedure} AND StageId = {Stage}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM AppointmentEvents WHERE AppointmentId = {Appointment} AND PathwayId = {Procedure} AND PathwayTitle = N'Synthetic procedure title'")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Attachments WHERE PatientId = {Patient} AND PathwayId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Notifications WHERE UserId = {User} AND PathwayId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM ActivityEntries WHERE PatientId = {Patient} AND PathwayId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM NotificationPreferences WHERE UserId = {User} AND PathwayAssigned = 0")).Should().Be(1);

        // The seeded stage definition is still there, and nothing is left under an old name.
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStageDefinitions WHERE Id = '11111111-1111-1111-1111-111111111111'")).Should().Be(1);
        (await NamesAsync("%Procedure%")).Should().BeEmpty();
    }

    [Test]
    public async Task Up_KeysForeignKeysAndIndexes_TakeTheNewNamesWithTheSameRules()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        (await NamesAsync("%Pathway%")).Should().Contain(
        [
            "PK_PatientPathways", "PK_PathwayTemplates", "PK_PathwayTemplateMaps", "PK_PathwayNoteDeletionRequests",
            "FK_PatientPathways_PathwayTemplates_TemplateId", "FK_Appointments_PatientPathways_PathwayId",
            "FK_Appointments_PatientPathwayStages_StageId", "IX_PatientPathwayAuditEntries_PatientPathwayId_AtUtc",
            "IX_PathwayNoteDeletionRequests_NoteId_Pending", "IX_Attachments_PathwayId"
        ]);

        // Deleting a pathway still takes its stages, assignments and notes with it (cascade), as before.
        (await _context.Database.SqlQuery<string>(
                $"SELECT name AS Value FROM sys.foreign_keys WHERE delete_referential_action_desc = 'CASCADE' AND name LIKE '%Pathway%'")
            .ToListAsync()).Should().BeEquivalentTo(
        [
            "FK_PatientPathwayAssignments_PatientPathways_PatientPathwayId",
            "FK_PatientPathwayNotes_PatientPathways_PatientPathwayId",
            "FK_PatientPathwayStages_PatientPathways_PatientPathwayId",
            "FK_PathwayTemplateMaps_PathwayTemplates_TemplateId"
        ]);

        // One waiting deletion request per note, still: the filtered unique index refuses a second.
        var second = () => _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO PathwayNoteDeletionRequests (Id, NoteId, PatientId, PatientPathwayId, Reason, RequestedAtUtc, RequestedByUserId, RequestedByUsername, Status)
            VALUES ({Guid.NewGuid()}, {Note}, {Patient}, {Procedure}, 1, {Now}, {User}, N'synthetic.user', 0)
            """);
        (await second.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("IX_PathwayNoteDeletionRequests_NoteId_Pending");
    }

    [Test]
    public async Task Up_StoredWords_ChangeOnlyWhereTheyAreTheAppsOwn()
    {
        await _context.GetService<IMigrator>().MigrateAsync(Migration);

        // What a file is attached to: the enum member's name, in the audit entries about files only.
        (await TextAsync($"SELECT Details AS Value FROM PatientPathwayAuditEntries WHERE Id = {FileAdded}"))
            .Should().Be(FileDetails.Replace("\"attachedTo\":\"Procedure\"", "\"attachedTo\":\"Pathway\""))
            .And.Contain("\\u0022attachedTo\\u0022:\\u0022Procedure\\u0022.pdf", "a file's own name is free text");
        (await TextAsync($"SELECT Details AS Value FROM PatientPathwayAuditEntries WHERE Id = {StageNotesSaved}")).Should().Be(NotesDetails);

        // The placeholder title, on an anonymised record only: someone's own title that happens to match stays.
        (await TextAsync($"SELECT Title AS Value FROM PatientPathways WHERE Id = {AnonymisedProcedure}")).Should().Be("Anonymised pathway");
        (await TextAsync($"SELECT PathwayTitle AS Value FROM PatientPathwayAuditEntries WHERE PatientPathwayId = {AnonymisedProcedure}")).Should().Be("Anonymised pathway");
        (await TextAsync($"SELECT PathwayTitle AS Value FROM AppointmentEvents WHERE PathwayId = {AnonymisedProcedure}")).Should().Be("Anonymised pathway");
        (await TextAsync($"SELECT Title AS Value FROM Notifications WHERE PathwayId = {AnonymisedProcedure}")).Should().Be("Anonymised pathway");
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathways WHERE Title = N'Anonymised procedure' AND PatientId = {Patient}")).Should().Be(1);

        // A notification already sent keeps its words.
        (await TextAsync($"SELECT Message AS Value FROM Notifications WHERE PathwayId = {Procedure}")).Should().Be("synthetic.admin assigned you to this procedure.");
    }

    [Test]
    public async Task Down_AfterUp_PutsTheOldNamesAndWordsBack_AndUpAgainWorks()
    {
        var migrator = _context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);

        await migrator.MigrateAsync(Before);

        (await NamesAsync("%Pathway%")).Should().BeEmpty();
        (await TextAsync($"SELECT Details AS Value FROM PatientProcedureAuditEntries WHERE Id = {FileAdded}")).Should().Be(FileDetails);
        (await TextAsync($"SELECT Title AS Value FROM PatientProcedures WHERE Id = {AnonymisedProcedure}")).Should().Be("Anonymised procedure");
        (await TextAsync($"SELECT ProcedureTitle AS Value FROM AppointmentEvents WHERE ProcedureId = {AnonymisedProcedure}")).Should().Be("Anonymised procedure");
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientProcedureStages WHERE Id = {Stage} AND PatientProcedureId = {Procedure}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientProcedureStageTemplateMaps WHERE TemplateId = {Template}")).Should().Be(1);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM Appointments WHERE Id = {Appointment} AND ProcedureId = {Procedure}")).Should().Be(1);

        await migrator.MigrateAsync(Migration);
        (await CountAsync($"SELECT COUNT(*) AS Value FROM PatientPathwayStages WHERE Id = {Stage} AND PatientPathwayId = {Procedure}")).Should().Be(1);
        (await TextAsync($"SELECT Title AS Value FROM PatientPathways WHERE Id = {AnonymisedProcedure}")).Should().Be("Anonymised pathway");
    }

    private Task<string> TextAsync(FormattableString sql) => _context.Database.SqlQuery<string>(sql).SingleAsync();

    private Task<int> CountAsync(FormattableString sql) => _context.Database.SqlQuery<int>(sql).SingleAsync();

    /// <summary>The tables, columns, keys, foreign keys and indexes whose names are like the pattern.</summary>
    private Task<List<string>> NamesAsync(string pattern) =>
        _context.Database.SqlQuery<string>($"""
            SELECT name AS Value FROM sys.tables WHERE name LIKE {pattern}
            UNION ALL SELECT c.name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE c.name LIKE {pattern}
            UNION ALL SELECT name FROM sys.key_constraints WHERE name LIKE {pattern}
            UNION ALL SELECT name FROM sys.foreign_keys WHERE name LIKE {pattern}
            UNION ALL SELECT i.name FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE i.name LIKE {pattern}
            """).ToListAsync();

    /// <summary>One of everything the rename touches, by the names before it: two patients, one of them anonymised.</summary>
    private async Task SeedAsync()
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Patients (Id, FirstName, LastName, DateOfBirth, CreatedAt, LastUpdated, AnonymisedAtUtc) VALUES
                ({Patient}, N'Synthetic', N'Person', '1980-05-06', {Now}, {Now}, NULL),
                ({AnonymisedPatient}, N'Anonymised', N'Patient', '1980-01-01', {Now}, {Now}, {Now});
            INSERT INTO PatientProcedureStageDefinitions (Id, Name, Description, Instructions) VALUES ({Definition}, N'Synthetic stage', N'Synthetic description', N'Synthetic instructions');
            INSERT INTO PatientProcedureStageTemplates (Id, Name) VALUES ({Template}, N'Synthetic template');
            INSERT INTO PatientProcedureStageTemplateMaps (TemplateId, StageDefinitionId, StageOrder) VALUES ({Template}, {Definition}, 1);
            INSERT INTO PatientProcedures (Id, Title, Notes, DueDateUtc, Priority, Status, PatientId, TemplateId, TemplateName, CreatedAt, LastUpdated) VALUES
                ({Procedure}, N'Synthetic procedure title', N'', {Now}, 1, 1, {Patient}, {Template}, N'Synthetic template', {Now}, {Now}),
                ({Guid.NewGuid()}, N'Anonymised procedure', N'', {Now}, 1, 1, {Patient}, NULL, N'', {Now}, {Now}),
                ({AnonymisedProcedure}, N'Anonymised procedure', N'', {Now}, 1, 6, {AnonymisedPatient}, NULL, N'', {Now}, {Now});
            INSERT INTO PatientProcedureStages (Id, PatientProcedureId, StageDefinitionId, StageOrder, IsCompleted, Name, CreatedAt, LastUpdated)
                VALUES ({Stage}, {Procedure}, {Definition}, 1, 0, N'Synthetic stage', {Now}, {Now});
            INSERT INTO PatientProcedureAssignments (PatientProcedureId, UserId, AssignedByUserId, AssignedAtUtc) VALUES ({Procedure}, {User}, {User}, {Now});
            INSERT INTO PatientProcedureNotes (Id, PatientProcedureId, Text, Version, CreatedAtUtc, AuthorUserId) VALUES ({Note}, {Procedure}, N'Synthetic note', 1, {Now}, {User});
            INSERT INTO PatientProcedureNoteVersions (Id, NoteId, PatientProcedureId, PatientId, Text, Username, Version, AtUtc)
                VALUES ({Guid.NewGuid()}, {Note}, {Procedure}, {Patient}, N'Synthetic note', N'synthetic.user', 1, {Now});
            INSERT INTO ProcedureNoteDeletionRequests (Id, NoteId, PatientId, PatientProcedureId, Reason, RequestedAtUtc, RequestedByUserId, RequestedByUsername, Status)
                VALUES ({Guid.NewGuid()}, {Note}, {Patient}, {Procedure}, 1, {Now}, {User}, N'synthetic.user', 0);
            INSERT INTO PatientProcedureAuditEntries (Id, PatientProcedureId, ProcedureTitle, PatientId, Action, UserId, Username, AtUtc, Details) VALUES
                ({Guid.NewGuid()}, {Procedure}, N'Synthetic procedure title', {Patient}, 0, {User}, N'synthetic.user', {Now}, {"{}"}),
                ({FileAdded}, {Procedure}, N'Synthetic procedure title', {Patient}, 10, {User}, N'synthetic.user', {Now}, {FileDetails}),
                ({StageNotesSaved}, {Procedure}, N'Synthetic procedure title', {Patient}, 6, {User}, N'synthetic.user', {Now}, {NotesDetails}),
                ({Guid.NewGuid()}, {AnonymisedProcedure}, N'Anonymised procedure', {AnonymisedPatient}, 0, {User}, N'synthetic.user', {Now}, {"{}"});
            INSERT INTO Appointments (Id, PatientId, ProcedureId, StageId, Status, IsAutomatic, CreatedByUserId, CreatedAtUtc, LastUpdated)
                VALUES ({Appointment}, {Patient}, {Procedure}, {Stage}, 0, 1, {User}, {Now}, {Now});
            INSERT INTO AppointmentEvents (Id, AppointmentId, PatientId, ProcedureId, ProcedureTitle, Action, UserId, Username, AtUtc, Details) VALUES
                ({Guid.NewGuid()}, {Appointment}, {Patient}, {Procedure}, N'Synthetic procedure title', 0, {User}, N'synthetic.user', {Now}, {"{}"}),
                ({Guid.NewGuid()}, {Guid.NewGuid()}, {AnonymisedPatient}, {AnonymisedProcedure}, N'Anonymised procedure', 0, {User}, N'synthetic.user', {Now}, {"{}"});
            INSERT INTO Attachments (Id, PatientId, ProcedureId, FileName, ContentType, SizeBytes, Sha256, ScanStatus, HasThumbnail, UploadedAtUtc, UploadedByUserId, UploadedByUsername)
                VALUES ({Guid.NewGuid()}, {Patient}, {Procedure}, N'scan.pdf', N'application/pdf', 10, {new string('a', 64)}, 0, 0, {Now}, {User}, N'synthetic.user');
            INSERT INTO Notifications (Id, UserId, Kind, Title, Message, ProcedureId, CreatedAtUtc) VALUES
                ({Guid.NewGuid()}, {User}, 0, N'Synthetic procedure title', N'synthetic.admin assigned you to this procedure.', {Procedure}, {Now}),
                ({Guid.NewGuid()}, {User}, 0, N'Anonymised procedure', N'synthetic.admin assigned you to this procedure.', {AnonymisedProcedure}, {Now});
            INSERT INTO NotificationPreferences (UserId, ProcedureAssigned, UpdatedAtUtc) VALUES ({User}, 0, {Now});
            INSERT INTO ActivityEntries (Id, Action, AtUtc, Details, Username, PatientId, ProcedureId) VALUES ({Guid.NewGuid()}, 13, {Now}, {"{}"}, N'synthetic.user', {Patient}, {Procedure});
            """);
    }
}
