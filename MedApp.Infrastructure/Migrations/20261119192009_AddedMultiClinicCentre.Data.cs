namespace MedApp.Infrastructure.Migrations
{
    /// <summary>
    /// <see cref="AddedMultiClinicCentre"/>'s data steps. Every value is written here, not read from the code's
    /// constants, so the migration never changes after it ships (as <c>SplitOverrideStages</c>).
    /// </summary>
    public partial class AddedMultiClinicCentre
    {
        private const string GeneralId = "33333333-0000-0000-0000-000000000001";
        private const string LabId = "33333333-0000-0000-0000-000000000002";

        private const string AdminRoleId = "7a3b0000-0000-4000-8000-000000000001";
        private const string AllStaffRoleId = "7a3b0000-0000-4000-8000-000000000002";
        private const string LabRoleId = "7a3b0000-0000-4000-8000-000000000006";
        private const string SeniorClinicianRoleId = "7a3b0000-0000-4000-8000-000000000008";
        private const string PathologistRoleId = "7a3b0000-0000-4000-8000-000000000009";

        private const string PathologistName = "Pathologist";
        private const string PathologistDescription = "Pathologists: as Lab, and they enter lab cases’ results and authorise their reports.";

        private static readonly string[] PathologistPermissions =
        [
            "UseApp", "ViewPatients", "WorkPathways", "AttachFiles", "RecordSpecimens", "ProcessSpecimens", "AuthoriseLabReports",
        ];

        private const string AdminDescriptionBefore = "Runs MedApp: accounts, roles, setup and privacy. Built in, with fixed permissions.";
        private const string AdminDescription = "Runs MedApp: accounts, roles, setup, privacy and oversight, reading every team’s records; no clinical changes. Built in, with fixed permissions.";
        private const string SeniorClinicianDescriptionBefore = "Senior clinicians: as Clinician, and they unlock stages and finish one without its visit.";
        private const string SeniorClinicianDescription = "Senior clinicians: the same permissions as Clinician, unlocking stages and finishing one without its visit included.";

        /// <summary>The lab every sample points at going down (the labs list was dropped going up).</summary>
        private const string RestoredLaboratoryId = "5ab50000-0000-4000-8000-000000000001";

        private static string Text(string value) => "N'" + value.Replace("'", "''") + "'";

        /// <summary>
        /// General (every database has it; put back if an admin deleted it, as rows fall back to it) and the lab team
        /// (MC3), with their fixed IDs. A team already called "Lab" is renamed, as names are unique. Going down leaves
        /// the lab as an ordinary team, so up again finds it.
        /// </summary>
        private const string TeamsSql = $"""
            IF NOT EXISTS (SELECT 1 FROM Teams WHERE Id = '{GeneralId}')
                INSERT INTO Teams (Id, Name, IsArchived, Version, TargetDaysLow, TargetDaysMedium, TargetDaysHigh, TargetDaysUrgent, Kind, CreatedAt, LastUpdated)
                VALUES ('{GeneralId}', CASE WHEN EXISTS (SELECT 1 FROM Teams WHERE Name = N'General') THEN N'General (restored)' ELSE N'General' END,
                        0, 1, 126, 126, 28, 14, 0, '2026-11-18', '2026-11-18');

            UPDATE Teams SET Name = LEFT(Name, 90) + N' (clinic)', Version = Version + 1, LastUpdated = SYSUTCDATETIME()
            WHERE Name = N'Lab' AND Id <> '{LabId}';

            IF NOT EXISTS (SELECT 1 FROM Teams WHERE Id = '{LabId}')
                INSERT INTO Teams (Id, Name, IsArchived, Version, TargetDaysLow, TargetDaysMedium, TargetDaysHigh, TargetDaysUrgent, Kind, CreatedAt, LastUpdated)
                VALUES ('{LabId}', N'Lab', 0, 1, 126, 126, 28, 14, 1, '2026-11-18', '2026-11-18');
            ELSE
                UPDATE Teams SET Kind = 1 WHERE Id = '{LabId}';
            """;

        /// <summary>
        /// Memberships (MC3), so nobody loses access on upgrade: every holder of the Lab role joins the lab team; then
        /// every account holding a role but in no team joins General. Accounts holding Admin are left as they are (they
        /// keep any memberships until an admin moves them, and gain none).
        /// </summary>
        private const string MembershipsSql = $"""
            INSERT INTO TeamMemberships (UserId, TeamId, Role)
            SELECT DISTINCT ur.UserId, '{LabId}', 0
            FROM AspNetUserRoles ur
            JOIN AspNetRoles r ON r.Id = ur.RoleId
            WHERE (r.Id = '{LabRoleId}' OR r.NormalizedName = N'LAB')
              AND NOT EXISTS (SELECT 1 FROM TeamMemberships m WHERE m.UserId = ur.UserId AND m.TeamId = '{LabId}')
              AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles a JOIN AspNetRoles ar ON ar.Id = a.RoleId
                              WHERE a.UserId = ur.UserId AND (ar.Id = '{AdminRoleId}' OR ar.NormalizedName = N'ADMIN'));

            INSERT INTO TeamMemberships (UserId, TeamId, Role)
            SELECT DISTINCT ur.UserId, '{GeneralId}', 0
            FROM AspNetUserRoles ur
            WHERE NOT EXISTS (SELECT 1 FROM TeamMemberships m WHERE m.UserId = ur.UserId)
              AND NOT EXISTS (SELECT 1 FROM AspNetUserRoles a JOIN AspNetRoles ar ON ar.Id = a.RoleId
                              WHERE a.UserId = ur.UserId AND (ar.Id = '{AdminRoleId}' OR ar.NormalizedName = N'ADMIN'));
            """;

        /// <summary>
        /// The starting role Pathologist (MC15) with a fixed ID, unless a role of that name or ID exists already; the
        /// built-in Admin role's new description (read only, decision 182), and Senior clinician's where an admin hasn't
        /// changed it; and "All staff (before roles)" archived now its holders are in teams (its holders keep its
        /// permissions; nobody new is given it).
        /// </summary>
        private static string RolesSql
        {
            get
            {
                var values = string.Join(", ", PathologistPermissions.Select(p => $"('{p}')"));
                var normalized = Text(PathologistName.ToUpperInvariant());
                return $"""
                    DECLARE @now datetime2 = SYSUTCDATETIME();

                    IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE NormalizedName = {normalized} OR Id = '{PathologistRoleId}')
                    BEGIN
                        INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, IsArchived, IsBuiltIn, CreatedAt, LastUpdated)
                        VALUES ('{PathologistRoleId}', {Text(PathologistName)}, {normalized}, LOWER(CONVERT(nvarchar(36), NEWID())), {Text(PathologistDescription)}, 0, 0, @now, @now);
                        INSERT INTO RolePermissions (RoleId, Permission)
                        SELECT '{PathologistRoleId}', permission.Name FROM (VALUES {values}) AS permission(Name);
                    END;

                    UPDATE AspNetRoles SET Description = {Text(AdminDescription)}, LastUpdated = @now
                    WHERE Id = '{AdminRoleId}' AND IsBuiltIn = 1;

                    UPDATE AspNetRoles SET Description = {Text(SeniorClinicianDescription)}, LastUpdated = @now
                    WHERE Id = '{SeniorClinicianRoleId}' AND Description = {Text(SeniorClinicianDescriptionBefore)};

                    UPDATE AspNetRoles SET IsArchived = 1, LastUpdated = @now
                    WHERE Id = '{AllStaffRoleId}' AND IsBuiltIn = 0 AND IsArchived = 0;
                    """;
            }
        }

        /// <summary>
        /// Every new <c>TeamId</c> (decision 200), as the code sets it on a new row: its pathway's, else its referral's,
        /// else its appointment's, else a person's first team (after the membership steps: an appointment's clinician,
        /// so it stays on their team's calendar, else its booker; a file's uploader; a letter's author; a result's
        /// reporter), else General. An audit entry without a pathway takes the team of the letter, result,
        /// file, consent record, sample or referral its details name. Existing samples all came from a clinic's pathway.
        /// </summary>
        private const string TeamBackfillSql = $"""
            SELECT UserId, CAST(MIN(CAST(TeamId AS char(36))) AS uniqueidentifier) AS TeamId
            INTO #PersonTeam
            FROM TeamMemberships
            GROUP BY UserId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, s.TeamId, b.TeamId, c.TeamId, '{GeneralId}')
            FROM Appointments x
            LEFT JOIN PatientPathways p ON p.Id = x.PathwayId
            LEFT JOIN #PersonTeam b ON b.UserId = x.BookedByUserId
            LEFT JOIN #PersonTeam c ON c.UserId = x.CreatedByUserId
            LEFT JOIN #PersonTeam s ON s.UserId = x.StaffUserId;

            UPDATE x SET TeamId = COALESCE(a.TeamId, '{GeneralId}')
            FROM AppointmentEvents x LEFT JOIN Appointments a ON a.Id = x.AppointmentId;

            UPDATE x SET TeamId = COALESCE(a.TeamId, '{GeneralId}')
            FROM PatientMessages x LEFT JOIN Appointments a ON a.Id = x.AppointmentId;

            UPDATE x SET TeamId = COALESCE(r.TeamId, '{GeneralId}')
            FROM ReferralEvents x LEFT JOIN Referrals r ON r.Id = x.ReferralId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, r.TeamId, u.TeamId, '{GeneralId}')
            FROM Attachments x
            LEFT JOIN PatientPathways p ON p.Id = x.PathwayId
            LEFT JOIN Referrals r ON r.Id = x.ReferralId
            LEFT JOIN #PersonTeam u ON u.UserId = x.UploadedByUserId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, '{GeneralId}')
            FROM ConsentRecords x LEFT JOIN PatientPathways p ON p.Id = x.PathwayId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, r.TeamId, u.TeamId, '{GeneralId}')
            FROM Letters x
            LEFT JOIN PatientPathways p ON p.Id = x.PathwayId
            LEFT JOIN Referrals r ON r.Id = x.ReferralId
            LEFT JOIN #PersonTeam u ON u.UserId = x.AuthorUserId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, '{GeneralId}'), RequestingTeamId = p.TeamId, Source = 0
            FROM Specimens x LEFT JOIN PatientPathways p ON p.Id = x.PathwayId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, sp.TeamId, u.TeamId, '{GeneralId}')
            FROM Results x
            LEFT JOIN PatientPathways p ON p.Id = x.PathwayId
            LEFT JOIN Specimens sp ON sp.Id = x.SpecimenId
            LEFT JOIN #PersonTeam u ON u.UserId = x.ReportedByUserId;

            UPDATE x SET TeamId = COALESCE(p.TeamId, l.TeamId, rs.TeamId, f.TeamId, c.TeamId, sp.TeamId, rf.TeamId, '{GeneralId}')
            FROM PatientPathwayAuditEntries x
            CROSS APPLY (SELECT CASE WHEN ISJSON(x.Details) = 1 THEN x.Details END AS J) d
            LEFT JOIN PatientPathways p ON p.Id = x.PatientPathwayId
            LEFT JOIN Letters l ON l.Id = TRY_CAST(JSON_VALUE(d.J, '$.letterId') AS uniqueidentifier)
            LEFT JOIN Results rs ON rs.Id = TRY_CAST(JSON_VALUE(d.J, '$.resultId') AS uniqueidentifier)
            LEFT JOIN Attachments f ON f.Id = TRY_CAST(JSON_VALUE(d.J, '$.attachmentId') AS uniqueidentifier)
            LEFT JOIN ConsentRecords c ON c.Id = TRY_CAST(JSON_VALUE(d.J, '$.consentRecordId') AS uniqueidentifier)
            LEFT JOIN Specimens sp ON sp.Id = TRY_CAST(JSON_VALUE(d.J, '$.specimenId') AS uniqueidentifier)
            LEFT JOIN Referrals rf ON rf.Id = TRY_CAST(JSON_VALUE(d.J, '$.referralId') AS uniqueidentifier);

            DROP TABLE #PersonTeam;
            """;

        /// <summary>
        /// Pathway grants (decision 201): every pathway's Owner grant to its team, from its creation; the lab's
        /// StageOwner grant on every pathway with a sample (not the lab's own), from its first sample (never ended,
        /// decision 184); and each attendee's Mdt grant on every item of an MDT still open or whose meeting hasn't ended
        /// (MC21, MC26, as <c>MdtGrants.Holders</c>).
        /// </summary>
        private const string GrantsSql = $"""
            DECLARE @now datetime2 = SYSUTCDATETIME();

            INSERT INTO PathwayAccess (Id, PathwayId, PatientId, TeamId, UserId, Reason, MdtItemId, StartedAtUtc, EndedAtUtc)
            SELECT NEWID(), p.Id, p.PatientId, p.TeamId, NULL, 0, NULL, p.CreatedAt, NULL
            FROM PatientPathways p;

            INSERT INTO PathwayAccess (Id, PathwayId, PatientId, TeamId, UserId, Reason, MdtItemId, StartedAtUtc, EndedAtUtc)
            SELECT NEWID(), p.Id, p.PatientId, '{LabId}', NULL, 1, NULL, s.FirstAt, NULL
            FROM PatientPathways p
            CROSS APPLY (SELECT MIN(x.TakenAtUtc) AS FirstAt, COUNT(*) AS Samples FROM Specimens x WHERE x.PathwayId = p.Id) s
            WHERE s.Samples > 0 AND p.TeamId <> '{LabId}';

            INSERT INTO PathwayAccess (Id, PathwayId, PatientId, TeamId, UserId, Reason, MdtItemId, StartedAtUtc, EndedAtUtc)
            SELECT NEWID(), i.PathwayId, i.PatientId, NULL, a.StaffUserId, 3, i.Id, i.ListedAtUtc, NULL
            FROM MdtItems i
            JOIN Meetings m ON m.Id = i.MeetingId AND m.IsMdt = 1
            JOIN MeetingAttendees a ON a.MeetingId = m.Id
            WHERE i.Status IN (0, 2) OR m.EndUtc > @now;
            """;

        /// <summary>
        /// Branch points into choice nodes (MC8, MC10b) for one set of stage lists, read from <c>#In_{x}</c>
        /// (<c>OwnerId</c>, the list's template or pathway; <c>O</c>, the stage order; <c>Bp</c>, a branch point;
        /// <c>Br</c>, its branch name). The old rules: a branch point's branches are the named stages straight after it
        /// (a plain stage, or the next branch point, ends them: the join); a branch point needs two branches or more.
        /// Writes <c>#Br_{x}</c> (each branch: its branch point, first stage and name as first written), <c>#L_{x}</c>
        /// (the links: each stage to the next, but a branch point to each branch's first stage, each branch's stages
        /// in a chain, and each branch's last to the join, or nothing at the end), <c>#O_{x}</c> (each branch point's
        /// outcomes as JSON) and <c>#LJ_{x}</c> (each list's links as JSON).
        /// </summary>
        private static string ChoiceNodes(string x) => $$"""
            SELECT OwnerId, O, Bp,
                   CASE WHEN Bp = 1 THEN NULL ELSE NULLIF(LTRIM(RTRIM(Br)), N'') END AS Br,
                   CAST(NULL AS int) AS BpO, CAST(0 AS bit) AS InRun
            INTO #W_{{x}}
            FROM #In_{{x}};

            UPDATE w SET BpO = (SELECT MAX(b.O) FROM #W_{{x}} b WHERE b.OwnerId = w.OwnerId AND b.Bp = 1 AND b.O < w.O)
            FROM #W_{{x}} w;

            UPDATE w SET InRun = 1
            FROM #W_{{x}} w
            WHERE w.Br IS NOT NULL AND w.BpO IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM #W_{{x}} y WHERE y.OwnerId = w.OwnerId AND y.O > w.BpO AND y.O < w.O AND y.Br IS NULL);

            SELECT OwnerId, BpO, MIN(O) AS FirstO
            INTO #B0_{{x}}
            FROM #W_{{x}}
            WHERE InRun = 1
            GROUP BY OwnerId, BpO, UPPER(Br);

            SELECT b.OwnerId, b.BpO, b.FirstO,
                   (SELECT w.Br FROM #W_{{x}} w WHERE w.OwnerId = b.OwnerId AND w.O = b.FirstO) AS Name
            INTO #Br_{{x}}
            FROM #B0_{{x}} b
            WHERE (SELECT COUNT(*) FROM #B0_{{x}} c WHERE c.OwnerId = b.OwnerId AND c.BpO = b.BpO) >= 2;

            UPDATE w SET InRun = 0
            FROM #W_{{x}} w
            WHERE w.InRun = 1 AND NOT EXISTS (SELECT 1 FROM #Br_{{x}} b WHERE b.OwnerId = w.OwnerId AND b.BpO = w.BpO);

            SELECT w.OwnerId, w.O AS F, n.O AS T
            INTO #L_{{x}}
            FROM #W_{{x}} w
            CROSS APPLY (SELECT MIN(y.O) AS O FROM #W_{{x}} y WHERE y.OwnerId = w.OwnerId AND y.O > w.O) n
            WHERE n.O IS NOT NULL AND w.InRun = 0
              AND NOT EXISTS (SELECT 1 FROM #Br_{{x}} b WHERE b.OwnerId = w.OwnerId AND b.BpO = w.O)
            UNION ALL
            SELECT b.OwnerId, b.BpO, b.FirstO FROM #Br_{{x}} b
            UNION ALL
            SELECT w.OwnerId, w.O, COALESCE(ns.O, j.O)
            FROM #W_{{x}} w
            CROSS APPLY (SELECT MIN(y.O) AS O FROM #W_{{x}} y
                         WHERE y.OwnerId = w.OwnerId AND y.InRun = 1 AND y.BpO = w.BpO AND UPPER(y.Br) = UPPER(w.Br) AND y.O > w.O) ns
            CROSS APPLY (SELECT MIN(y.O) AS O FROM #W_{{x}} y WHERE y.OwnerId = w.OwnerId AND y.O > w.BpO AND y.Br IS NULL) j
            WHERE w.InRun = 1 AND COALESCE(ns.O, j.O) IS NOT NULL;

            SELECT OwnerId, BpO,
                   N'[' + STRING_AGG(CAST(N'{"name":"' + STRING_ESCAPE(Name, 'json') + N'","nextStageOrders":[' + CAST(FirstO AS nvarchar(10)) + N']}' AS nvarchar(max)), N',')
                          WITHIN GROUP (ORDER BY FirstO) + N']' AS Outcomes
            INTO #O_{{x}}
            FROM #Br_{{x}}
            GROUP BY OwnerId, BpO;

            SELECT OwnerId,
                   N'[' + STRING_AGG(CAST(N'{"fromStageOrder":' + CAST(F AS nvarchar(10)) + N',"toStageOrder":' + CAST(T AS nvarchar(10)) + N'}' AS nvarchar(max)), N',')
                          WITHIN GROUP (ORDER BY F, T) + N']' AS Links
            INTO #LJ_{{x}}
            FROM #L_{{x}}
            GROUP BY OwnerId;
            """;

        private static string DropChoiceNodes(string x) =>
            $"DROP TABLE #In_{x}; DROP TABLE #W_{x}; DROP TABLE #B0_{x}; DROP TABLE #Br_{x}; DROP TABLE #L_{x}; DROP TABLE #O_{x}; DROP TABLE #LJ_{x};";

        /// <summary>
        /// A template's branch points become choice nodes (kind Assessment), each branch an outcome leading to its first
        /// stage, and the template gets its links (a template with none stays a chain, as before).
        /// </summary>
        private static string TemplateChoiceNodesSql => $"""
            SELECT TemplateId AS OwnerId, StageOrder AS O, ChoosesBranch AS Bp, Branch AS Br
            INTO #In_t
            FROM PathwayTemplateMaps;

            {ChoiceNodes("t")}

            UPDATE m SET ChoiceKind = 0, Outcomes = o.Outcomes
            FROM PathwayTemplateMaps m
            JOIN #O_t o ON o.OwnerId = m.TemplateId AND o.BpO = m.StageOrder;

            UPDATE t SET Links = lj.Links
            FROM PathwayTemplates t
            JOIN #LJ_t lj ON lj.OwnerId = t.Id
            WHERE EXISTS (SELECT 1 FROM #O_t o WHERE o.OwnerId = t.Id);

            {DropChoiceNodes("t")}
            """;

        /// <summary>
        /// A pathway's stages: each its template order (they all came from the template). A branch point whose branch was
        /// chosen (the BranchChosen entry's <c>branch</c>, else the branch with no stage skipped as BranchNotChosen) is a
        /// choice node with that outcome recorded; its other branches' stages stay skipped, as they were.
        /// <para>
        /// An open pathway (not completed, cancelled or archived) whose first branch point still to be chosen has nothing
        /// after it yet (nothing started, finished, skipped or noted, and no visit, file, consent or sample on those
        /// stages) is made a grown pathway: its plan is the tree it was made with (its stages' own copies, its branch
        /// points as choice nodes), and the stages after that choice node go, to be grown back by the outcome the
        /// clinician records. Any other branch point not yet chosen (a closed pathway's, or one with work after it)
        /// becomes a plain stage, and the stages after it run in order (staff skip the optional ones that don't apply).
        /// </para>
        /// Then every pathway's stages are linked: in a chain, but from each chosen branch point to each branch's first
        /// stage, along each branch, and from each branch's last stage to the stage after the branches.
        /// </summary>
        private static string PathwayChoiceNodesSql => $"""
            SELECT PatientPathwayId AS OwnerId, StageOrder AS O, ChoosesBranch AS Bp, Branch AS Br
            INTO #In_p
            FROM PatientPathwayStages;

            {ChoiceNodes("p")}

            SELECT v.OwnerId, v.BpO, s.Id AS StageId, s.IsCompleted, s.SkippedAtUtc,
                   COALESCE(
                       (SELECT TOP 1 b.Name FROM #Br_p b
                        WHERE b.OwnerId = v.OwnerId AND b.BpO = v.BpO AND UPPER(b.Name) = UPPER(LTRIM(RTRIM(chosen.Branch)))),
                       CASE WHEN s.IsCompleted = 1 THEN
                           (SELECT TOP 1 b.Name FROM #Br_p b
                            WHERE b.OwnerId = v.OwnerId AND b.BpO = v.BpO
                              AND NOT EXISTS (SELECT 1 FROM #W_p w
                                              JOIN PatientPathwayStages y ON y.PatientPathwayId = w.OwnerId AND y.StageOrder = w.O
                                              WHERE w.OwnerId = b.OwnerId AND w.BpO = b.BpO AND w.InRun = 1
                                                AND UPPER(w.Br) = UPPER(b.Name) AND y.SkipReason = 0)
                            ORDER BY b.FirstO)
                       END) AS Recorded
            INTO #R_p
            FROM #O_p v
            JOIN PatientPathwayStages s ON s.PatientPathwayId = v.OwnerId AND s.StageOrder = v.BpO
            OUTER APPLY (SELECT TOP 1 CASE WHEN ISJSON(e.Details) = 1 THEN JSON_VALUE(e.Details, '$.branch') END AS Branch
                         FROM PatientPathwayAuditEntries e
                         WHERE e.StageId = s.Id AND e.Action = 36
                         ORDER BY e.AtUtc DESC) chosen;

            SELECT r.OwnerId, MIN(r.BpO) AS U
            INTO #U
            FROM #R_p r
            JOIN PatientPathways p ON p.Id = r.OwnerId
            WHERE r.Recorded IS NULL AND p.Status NOT IN (5, 6, 8)
            GROUP BY r.OwnerId;

            DELETE u FROM #U u
            WHERE EXISTS (SELECT 1 FROM #R_p r WHERE r.OwnerId = u.OwnerId AND r.BpO = u.U AND (r.IsCompleted = 1 OR r.SkippedAtUtc IS NOT NULL))
               OR EXISTS (SELECT 1 FROM PatientPathwayStages s
                          WHERE s.PatientPathwayId = u.OwnerId AND s.StageOrder > u.U
                            AND (s.IsCompleted = 1 OR s.StartedAtUtc IS NOT NULL OR s.SkippedAtUtc IS NOT NULL
                                 OR NULLIF(LTRIM(RTRIM(s.Notes)), N'') IS NOT NULL
                                 OR EXISTS (SELECT 1 FROM Appointments a WHERE a.StageId = s.Id)
                                 OR EXISTS (SELECT 1 FROM Attachments a WHERE a.StageId = s.Id)
                                 OR EXISTS (SELECT 1 FROM ConsentRecords c WHERE c.StageId = s.Id)
                                 OR EXISTS (SELECT 1 FROM Specimens sp WHERE sp.StageId = s.Id)));

            UPDATE p SET [Plan] = (
                SELECT
                    JSON_QUERY((
                        SELECT s.StageOrder AS templateStageOrder, s.StageDefinitionId AS stageDefinitionId, s.Name AS name,
                               s.Description AS description, s.Instructions AS instructions, s.SnomedCode AS snomedCode,
                               s.SnomedTerm AS snomedTerm, s.VisitRequirement AS visitRequirement, s.NeedsLetterKind AS needsLetterKind,
                               s.WaitsForResults AS waitsForResults, s.NeedsConsent AS needsConsent, s.IsOptional AS isOptional,
                               s.StopsClock AS stopsClock, CASE WHEN o.Outcomes IS NOT NULL THEN 0 END AS choiceKind,
                               JSON_QUERY(COALESCE(o.Outcomes, N'[]')) AS outcomes, CAST(NULL AS uniqueidentifier) AS ownerTeamId
                        FROM PatientPathwayStages s
                        LEFT JOIN #O_p o ON o.OwnerId = s.PatientPathwayId AND o.BpO = s.StageOrder
                        WHERE s.PatientPathwayId = p.Id
                        ORDER BY s.StageOrder
                        FOR JSON PATH, INCLUDE_NULL_VALUES)) AS stages,
                    JSON_QUERY(COALESCE((SELECT lj.Links FROM #LJ_p lj WHERE lj.OwnerId = p.Id), N'[]')) AS links,
                    JSON_QUERY(N'[]') AS branches
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)
            FROM PatientPathways p
            JOIN #U u ON u.OwnerId = p.Id;

            DELETE s FROM PatientPathwayStages s
            JOIN #U u ON u.OwnerId = s.PatientPathwayId AND s.StageOrder > u.U;

            UPDATE PatientPathwayStages SET TemplateStageOrder = StageOrder;

            UPDATE s SET ChoiceKind = 0, Outcome = r.Recorded
            FROM PatientPathwayStages s
            JOIN #R_p r ON r.OwnerId = s.PatientPathwayId AND r.BpO = s.StageOrder
            WHERE r.Recorded IS NOT NULL;

            UPDATE s SET ChoiceKind = 0
            FROM PatientPathwayStages s
            JOIN #U u ON u.OwnerId = s.PatientPathwayId AND u.U = s.StageOrder;

            DROP TABLE #R_p;
            DROP TABLE #U;
            {DropChoiceNodes("p")}

            SELECT PatientPathwayId AS OwnerId, StageOrder AS O, CAST(CASE WHEN Outcome IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Bp, Branch AS Br
            INTO #In_q
            FROM PatientPathwayStages;

            {ChoiceNodes("q")}

            INSERT INTO PatientPathwayStageLinks (FromStageId, ToStageId, PatientPathwayId)
            SELECT f.Id, t.Id, l.OwnerId
            FROM #L_q l
            JOIN PatientPathwayStages f ON f.PatientPathwayId = l.OwnerId AND f.StageOrder = l.F
            JOIN PatientPathwayStages t ON t.PatientPathwayId = l.OwnerId AND t.StageOrder = l.T;

            {DropChoiceNodes("q")}
            """;

        /// <summary>
        /// Going down, before the new columns go: hospital samples (no pathway) can't be held by the older model, so
        /// their results lose the link and they go; every sample points at one placeholder lab; branch points come back
        /// from choice nodes (a template's outcomes name the branch of each stage on their route, up to a join or the
        /// next choice node; a pathway's stages take their template's); stage orders are made unique again where a
        /// pathway grew; "All staff (before roles)" and the Admin description are as they were.
        /// </summary>
        private static string DownSql => $"""
            UPDATE Results SET SpecimenId = NULL
            WHERE SpecimenId IN (SELECT Id FROM Specimens WHERE PathwayId IS NULL OR StageId IS NULL);

            DELETE FROM Specimens WHERE PathwayId IS NULL OR StageId IS NULL;

            IF EXISTS (SELECT 1 FROM Specimens)
            BEGIN
                INSERT INTO Laboratories (Id, Name, UsesMedApp, IsArchived, CreatedAt, LastUpdated)
                VALUES ('{RestoredLaboratoryId}', N'Lab (restored)', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
                UPDATE Specimens SET LaboratoryId = '{RestoredLaboratoryId}';
            END;

            UPDATE PathwayTemplateMaps SET ChoosesBranch = 1 WHERE ChoiceKind IS NOT NULL;

            WITH L AS (
                SELECT t.Id AS TemplateId,
                       CAST(JSON_VALUE(l.value, '$.fromStageOrder') AS int) AS F,
                       CAST(JSON_VALUE(l.value, '$.toStageOrder') AS int) AS T
                FROM PathwayTemplates t
                CROSS APPLY OPENJSON(CASE WHEN ISJSON(t.Links) = 1 THEN t.Links ELSE N'[]' END) l),
            Ins AS (SELECT TemplateId, T, COUNT(*) AS N FROM L GROUP BY TemplateId, T),
            Outs AS (SELECT TemplateId, F, COUNT(*) AS N FROM L GROUP BY TemplateId, F),
            Starts AS (
                SELECT m.TemplateId, CAST(n.value AS int) AS O, CAST(JSON_VALUE(o.value, '$.name') AS nvarchar(50)) AS Name
                FROM PathwayTemplateMaps m
                CROSS APPLY OPENJSON(CASE WHEN ISJSON(m.Outcomes) = 1 THEN m.Outcomes ELSE N'[]' END) o
                CROSS APPLY OPENJSON(o.value, '$.nextStageOrders') n
                WHERE m.ChoiceKind IS NOT NULL),
            Walk AS (
                SELECT s.TemplateId, s.O, s.Name, 0 AS Depth
                FROM Starts s
                WHERE NOT EXISTS (SELECT 1 FROM PathwayTemplateMaps y WHERE y.TemplateId = s.TemplateId AND y.StageOrder = s.O AND y.ChoiceKind IS NOT NULL)
                UNION ALL
                SELECT w.TemplateId, L.T, w.Name, w.Depth + 1
                FROM Walk w
                JOIN Outs o ON o.TemplateId = w.TemplateId AND o.F = w.O AND o.N = 1
                JOIN L ON L.TemplateId = w.TemplateId AND L.F = w.O
                JOIN Ins i ON i.TemplateId = w.TemplateId AND i.T = L.T AND i.N = 1
                JOIN PathwayTemplateMaps y ON y.TemplateId = w.TemplateId AND y.StageOrder = L.T AND y.ChoiceKind IS NULL
                WHERE w.Depth < 500)
            UPDATE m SET Branch = w.Name
            FROM PathwayTemplateMaps m
            JOIN (SELECT TemplateId, O, MIN(Name) AS Name FROM Walk GROUP BY TemplateId, O) w
                ON w.TemplateId = m.TemplateId AND w.O = m.StageOrder
            OPTION (MAXRECURSION 1000);

            UPDATE s SET ChoosesBranch = CASE WHEN s.ChoiceKind IS NOT NULL THEN 1 ELSE 0 END, Branch = m.Branch
            FROM PatientPathwayStages s
            JOIN PatientPathways p ON p.Id = s.PatientPathwayId
            LEFT JOIN PathwayTemplateMaps m ON m.TemplateId = p.TemplateId AND m.StageOrder = s.TemplateStageOrder;

            WITH Numbered AS (
                SELECT StageOrder, ROW_NUMBER() OVER (PARTITION BY PatientPathwayId ORDER BY StageOrder, CreatedAt, Id) AS N
                FROM PatientPathwayStages
                WHERE PatientPathwayId IN (SELECT PatientPathwayId FROM PatientPathwayStages GROUP BY PatientPathwayId, StageOrder HAVING COUNT(*) > 1))
            UPDATE Numbered SET StageOrder = N;

            UPDATE AspNetRoles SET IsArchived = 0 WHERE Id = '{AllStaffRoleId}';
            UPDATE AspNetRoles SET Description = {Text(AdminDescriptionBefore)} WHERE Id = '{AdminRoleId}' AND IsBuiltIn = 1;
            """;
    }
}
