# MedApp: a multi-clinic centre with an in-house lab

Status: **planned** · 2026-11-18 · the final theme · source of truth: the owner's plan and decisions (18 November 2026, "MedApp multi-clinic plan", kept outside the repo); every open technical question decided as recommended (section 9), for the owner to overrule

**In one line:** separate clinics that can't see each other's clinical details, one lab that serves them all and a nearby hospital, and pathways that pass work between clinic and lab like a relay.

This builds on `docs/centre-phase2-plan.md` and `docs/centre-phases-3-5-plan.md`: their names, decisions 1 to 180 and the code they built. **Numbering continues: decisions from 181, hazards from H-65, DPIA risks from 41.** PRs are numbered **MC1 to MC29** (Stage 1) so they can't be confused with the earlier plans' PRs. Stored numbers are in section 7; if another branch takes one first, take the next free one and correct this document.

**Stage 1:** Phase 1 the foundation (MC1 to MC12), Phase 2 the lab (MC13 to MC22), Phase 3 Dermatology (MC23 to MC29). **Stage 2:** one phase per other clinic (section 11).

## 1. The owner's decisions (encode exactly)

181. **One concept, two words.** Today's specialties become **teams**: each clinic, and the lab. Screens say **"team"** looking inward (team calendar, team worklist, your team) and **"clinic"** or **"the lab"** when sending outward ("Pass to the lab", "Refer to Dermatology", "a pathway in Dermatology"). It stays a **centre**, never a company.
182. **Account types:**
    - **Admin:** overview, Insights, **read-only access to all data**, account management, setup. **No clinical changes.**
    - **Staff,** in a team (a clinic or the lab): their team decides what they see; their roles decide what they do. Clinic and lab staff are the same type.
    - **Clinic admin:** staff plus minor extra rights for their own team. **Built now as an account type**; its extras (an oversight tab, the team's own setup) are a Stage 2 to-do (S2-9).
    - **Only staff do clinical work.**
183. **Patients are centre-wide.** One record per person; every member of staff can find any patient.
    - **Other teams' pathways** show only as "a pathway in <clinic>, in progress since <date>" or "completed <date>": no title, stages, notes, files, ID or link.
    - **Other teams' appointments** are hidden; on other teams' calendars they show as **busy, with no patient**.
184. **The lab** sees **all** of a pathway once it's passed to it, **keeps read access afterwards**, and can **never** look up a pathway it wasn't passed.
185. **Meetings** are booked only with your own team, **except** consults (MC25) and MDTs (MC11, MC26).
186. **Hospital clocks:**
    - **What arrives:** the deadline comes in the hospital's request, shaped on FHIR UK Core `ServiceRequest-Lab` (priority, `authoredOn`, `occurrence`), `Specimen` (collection time, SNOMED CT type) and `DiagnosticReport-Lab`.
    - **The lab's own limit:** set per test, defaulting to the RCPath histopathology KPI (80% within 7 calendar days, 90% within 10). **Whichever deadline is sooner applies.**
    - **Service alerts only** (time), never alerts from clinical values (`docs/clinical-safety/intended-purpose-and-device-decision.md`).
187. **Existing features are reworked to fit.** **Keep:** referrals, letters, consent, results sign-off, MDT, reminders, data requests, retention. **Rework:** specimens become the lab's samples. **Drop:** the external labs list.
188. **The light demo** has **three accounts:** an admin, a **Dermatology clinic admin** and a lab staff member; the tour explains clinic admin versus staff.
189. **Calendars:** every member of staff can view any team's calendar, with a team picker.
190. **Order:** the foundation, then the lab, then Dermatology. Every other clinic is its own Stage 2 to-do.

## 2. Decisions for every PR

191. **The clinician decides; the app records** (decision 69 stands). MedApp never suggests a branch, a team, a priority or a test, never pre-selects one, and orders lists only by dates, times, staff-chosen labels or names. Clocks are date arithmetic.
192. **The data boundary is enforced in code, not by convention** (section 3). Every PR that adds a query on team data takes a `TeamScope` and adds its endpoints to the leak tests (decision 205).
193. **One audit log** (decision 70 stands): new clinical items write `PatientPathwayAuditEntry` rows with new actions (section 7); a hospital case, which has no pathway, uses `PatientPathwayAuditEntry.NoPathway` with the patient. Details hold IDs, enums by name, admin-written names and dates, **never free text**.
194. **Every new table of patient data** follows decision 76: reads logged (`RecordViewsAsync`, `EndpointCoverageTests`), changes audited, `PatientRecordRepository` (export and anonymisation), free text replaced on anonymisation, the timeline's staff canary, retention, `docs/governance/`.
195. **Restricted and anonymised records** follow decision 75: work that protects the patient can finish (a sample already taken is processed and reported, a hospital report is sent, a handover is picked up); new work can't (409, `PatientRules.EnsureCanChange`).
196. **Light demo parity** (`docs/agents/light-demo.md`): every PR updates `MedApp.Web/src/demo` handlers and seed in the same PR (simplified rules), or lists what it can't do in `notInLightDemo.ts` and the Read Me's "What doesn't work". Each PR below says what.
197. **Full demo data** (`tools/MedApp.DevMcp/DemoData`) keeps running in every PR (`DemoActivityParityTests` green); each phase's demo data PR adds the story.
198. **Governance in the same PR:** each PR updates the DPIA, data inventory and hazard log entries it builds controls for (drafted in MC1), and `docs/agents/*.md` for its area.

## 3. The data boundary (Phase 1's riskiest change)

### 3.1 The model

199. **The viewer's scope** (`TeamScope`, `MedApp.Application/Common/Teams`): built per request from the session, read on every request like permissions (`SessionService` reads memberships with the roles; a `medapp:team` claim each, `SessionAuthenticationDefaults.TeamClaim`).
    - `TeamIds` (the viewer's memberships), `SeesAllTeams` (holds the new Administrator-only permission `ViewAllTeams`), `IsSystem` (jobs, the seed, `medapp-dev`: unscoped, never from a request).
    - Carried on `PathwayActor.Scope` and `CalendarActor.Scope` (`User.ToPathwayActor()`), and on every query that reads team data.
200. **Every clinical row gets its team** (`TeamId`, not null, indexed, foreign key to `Teams` with `NoAction`), set at creation, never edited by hand:

    | Table | Its team |
    |---|---|
    | `PatientPathways` | its template's (today's `SpecialtyId`, renamed) |
    | `Referrals` | the team it's referred to (today's `SpecialtyId`); moves when redirected |
    | `Appointments` | the owner team of its stage (MC7), else the clinician's team chosen at booking |
    | `Attachments` | its pathway's or referral's; a patient-level file the uploader's team (chosen if several) |
    | `Letters`, `Results`, `ConsentRecords`, `MdtItems`, `Specimens` | their pathway's (a hospital sample: the lab's) |
    | `PatientMessages` | their appointment's |
    | `PatientPathwayAuditEntries`, `AppointmentEvents`, `ReferralEvents` | copied from their subject, for the timeline's filter |

201. **Pathway access grants** (`PathwayAccess`: `Id`, `PathwayId`, `PatientId`, `TeamId?`, `UserId?`, `Reason`, `CanChange`, `StartedAtUtc`, `EndedAtUtc?`; no foreign key to the pathway, so history outlives deletes):
    - `Owner` (the pathway's team; `CanChange`), written with the pathway.
    - `StageOwner` (the lab, when a stage it owns is first reached, MC7): **never ended** (decision 184). `CanChange` only while it owns a current stage (computed, not stored).
    - `Consult` (MC25): the asked team, read-only, ended when the consult closes.
    - `Mdt` (MC26): each attendee by `UserId`, read-only, ended when the item is discussed or withdrawn.
    - Grants are written **only** in the same save as the event that causes them (`PathwayChange`, `ConsultChange`, `MdtChange`); nothing else writes them.
    - **Visible in full** = `SeesAllTeams`, or a live grant for one of `TeamIds` or the viewer's own ID.
    - **Visible as a summary** = any other pathway of a patient the viewer can see (decision 183).
202. **The one place the rules live:** `TeamAccess` (`MedApp.Application/Common/Teams`) holds the LINQ expressions every query uses, so the same code runs in SQL and unit tests:
    - `TeamAccess.Pathways(scope)`: `p => scope.SeesAllTeams || db.PathwayAccess.Any(a => a.PathwayId == p.Id && a.EndedAtUtc == null && (scope.TeamIds.Contains(a.TeamId) || a.UserId == scope.UserId))`.
    - `TeamAccess.ByPathway<T>(scope)` for anything on a pathway (notes, stages, files, letters, results, consent, MDT items, samples); `TeamAccess.ByTeam<T>(scope)` for items without one (a patient-level file, a referral, an appointment without a stage, a hospital case).
    - `TeamAccess.CanChange(pathway, scope)` and `TeamAccess.CanWorkStage(stage, scope)` for writes (decision 210).
203. **Repository signatures take the scope,** so a forgotten filter doesn't compile: every repository method returning a team-scoped entity or response has a `TeamScope scope` parameter (`TeamScopeConventionTests`, a reflection test over every repository interface in `Application`, against the list of team-scoped types). Loads for update (`GetForUpdateOrThrowAsync`) take it too.
204. **Across the boundary is a 404,** never a 403, so existence isn't confirmed. The refusal records `ActivityAction.TeamBoundaryRefused = 54` (`{targetKind, targetId}`, the viewer as actor; once per target per `Activity:ViewWindowMinutes`), so the privacy officer can see probing.
205. **The leak tests** (MC4, completed by MC5), like `EndpointCoverageTests`:
    - **`TeamBoundaryCoverageTests`** (integration): every endpoint from the API's action descriptors is classified `CentreWide` (setup lists, patients' demographics, own account), `TeamScoped` (needs a probe), `AdminOnly` (an Administrator-only permission) or `NotData`. An unclassified endpoint, or a `TeamScoped` one without a probe, fails.
    - **The probes:** signed in as a team B member holding **every** non-admin permission, each `TeamScoped` endpoint is called with team A's IDs (GET, PUT, POST, DELETE): a 404, an empty list, or the masked shape (busy time, the summary). Lists and counts run with no filter and must not include team A's rows.
    - **`TeamLeakCanaryTests`** (integration): team A's records carry marker text in every free-text field (`CANARY-A-…`) and known IDs; every GET and search endpoint, called as team B, as the lab before a handover, and as a consulted team after the consult closed, must return no marker and no team A ID except where the summary allows (it has none).
    - **Unit:** `TeamAccessTests` (each expression in memory), `TeamAccessSqlTests` (`ToQueryString`: one `EXISTS` on `PathwayAccess`, seeking `IX_PathwayAccess_PathwayId_TeamId`).
206. **Not global query filters** (section 9, Q1): the clash checks, jobs, monitoring, the seed and uniqueness checks must read every team's rows; hidden filters would make an appointment in another team stop counting as busy.
207. **The activity log records why access was allowed:** every `PatientViewed`, `PathwayViewed`, `PatientListed`, `SpecimenViewed`, `ResultViewed`, `LetterViewed` and `LabCaseViewed` detail gains `access` (`Owner`, `StageOwner`, `Consult`, `Mdt` or `Admin`; the strongest that applied), and an admin's list views `teamFilter` (the clinic picker's choice, or `all`). The activity log page filters by `access`.
208. **Admins are read-only** (decision 182): `Permissions.NotForAdmin` gains `EditPatients`, `DeletePatients`, `RegisterReferrals`, `CreatePathways`, `WorkPathways`, `AssignPathways`, `UnlockStages`, `DeletePathways`, `BookAppointments`, `AttachFiles` and Phase 2's `AuthoriseLabReports`. Admin keeps setup, accounts, oversight, `ArrangeMeetings`, `ManageAnyCalendar`, `ManageClinics`, and the governance actions it already owns (`ManagePrivacy`, `CorrectRecords`, `ExportPatientRecords`).
209. **An Admin is in no team, and a team member isn't Admin** (409 either way, `TeamRules.AdminHasNoTeam`); someone who is both has two accounts.
210. **Who changes what in a visible pathway:**
    - The **owner team** (with `WorkPathways` and the rest, as today) changes everything it owns: details, notes, assignments, its own stages.
    - The **stage owner** (the lab) works only the stages it owns while they're current: notes on the pathway, samples, its case, finishing its stage. Everything else is read-only.
    - **Consulted teams and MDT attendees** only read, and write their reply or outcome.
    - Response flags (`CanWorkOnStages`, a stage's `CanEdit`, `CanAddNotes`…) follow `TeamAccess.CanChange` as well as permissions.

### 3.2 Every existing query and its filter

| Area | Queries and endpoints | Filter (MC4 or MC5) | Activity log |
|---|---|---|---|
| Patients | `GET api/patients/search`, `GET api/patients/{id}`, overview | **None** on demographics (decision 183). The pathways card: own in full, others as `OtherTeamPathwaySummary` (team name, `InProgress`/`Completed`, since date). Counts in list columns count visible pathways only. Hospital-only patients hidden from non-lab staff (MC18). MC4 | `PatientViewed`/`PatientListed` + `access` |
| Patient timeline | `GET api/patients/{id}/timeline` | `TeamId` of each entry in scope, or its pathway visible; other teams' entries left out (not summarised). Admins see all. MC4 | `PatientViewed` |
| Pathways | search, get, edit, stages, notes, note deletion requests, assignments, `PathwaySearch`, My Worklist | `TeamAccess.Pathways`; writes `TeamAccess.CanChange` / `CanWorkStage`. MC4 | `PathwayViewed` + `access` |
| Pathway audit | `GET api/pathways/audit/search` (`ViewAudit`) | Admin only: unfiltered. MC4 | as today |
| Attachments | list by patient, pathway, stage, referral; download; thumbnail; delete | `ByPathway` / `ByTeam`. MC4 | as today + `access` |
| Insights | `GET api/pathways/insights` | `InsightsScope.TeamIds` forced to the viewer's teams unless `SeesAllTeams`; an admin's picker narrows. MC5 | none (aggregates) |
| Referrals | search, get, count, history, letters' files, triage | `ByTeam` (the referral's current team); registering for another team allowed, then not visible to the registrar (Q12). MC5 | `ReferralViewed`, `PatientListed` + `access` |
| Appointments | search, get, by pathway, by patient, Did not attend view, patient messages | `ByTeam`. MC5 | `PatientListed` + `access` |
| Calendar | calendar data, team view, availability, clinic sessions (deprecated) | **Unfiltered for clashes**; other teams' appointments **masked** in the response (MC11): no patient, pathway, stage, notes or appointment ID; "Busy". Clash messages name no patient for another team's. MC5 masks, MC11 adds the team picker | none (no patient data once masked) |
| Meetings | list, get, MDT meetings | Visible to all staff as busy time with title hidden for other teams (Q16); booking rules MC11 | none |
| Specimens (samples) | search, find, get, print | `ByPathway`; hospital samples: lab only. MC5 (MC14 reworks) | `SpecimenViewed` + `access` |
| Results | search, count, get, sign-off | `ByPathway`. MC5 | `ResultViewed` + `access` |
| Letters | search, get, PDF, counts | `ByPathway` or the referral's `ByTeam`. MC5 | `LetterViewed` + `access` |
| Consent | by pathway, changes | `ByPathway`. MC5 | `PathwayViewed` |
| MDT | a meeting's list, awaiting list, a pathway's items | items whose pathway is visible; attendees through `Mdt` grants (MC26). MC5 | `PatientListed` |
| Side nav counts | results, referrals awaiting triage, letters awaiting approval, worklists | the viewer's teams. MC5 | none |
| Notifications | own | unchanged (addressed to a person); a link to something no longer visible is a 404 | none |
| Monitoring rules, activity log, jobs, data requests, retention review, export, anonymisation | admin only | unfiltered (`SeesAllTeams`) | as today |
| FHIR `Patient`, demographics lookup, practices, setup lists | centre-wide | none | as today |
| Notepad | own | "Add note to pathway" needs the pathway visible and changeable | as today |

## 4. Phase 1: the foundation (MC1 to MC12)

**MC1. Safety and governance first** (docs only, S). ∥ with MC2.
- **Scope:** drafts for all of Stage 1, so every feature PR builds its controls: hazards **H-65 to H-76** and DPIA risks **41 to 48** (section 8), the device reassessment (section 8's table), `dcb0160-deploying-organisations.md` duties (review each team's members and each template's stage owners before go-live; a daily check of the lab's clocks and handovers not picked up), `roles-and-lawful-bases.md` (the lab working for the hospital: controller or processor, for the DPO), `ropa.md` (the hospital as a source and recipient), `retention-schedule.md` (lab cases and hospital samples: a DPO question), `docs/glossary.md` (team, clinic, the lab, relay, fork, join, sample, lab case, hospital request, clock).
- **Light demo:** none.

**MC2. Specialties renamed to teams** (mechanical, M). After MC1 or ∥. **Merge it alone:** no other code PR open meanwhile.
- **Scope:** `Specialty` → `Team`, `StaffSpecialty` → `TeamMembership`, every `SpecialtyId` → `TeamId`, `ISpecialtyRepository` → `ITeamRepository`, `SpecialtyRules`/`Mapper`/`Usage` → `Team…`, `api/specialties` → `api/teams` (no alias, as `api/procedures`), `api/auth/users/{u}/specialties` → `…/teams`, `api/auth/me/specialties` → `me/teams`, the Specialties page → Teams (`/admin/teams`). `Specialty.GeneralId` → `Team.GeneralId`.
- **Migration** `RenamedSpecialtiesToTeams`: `RenameTable`, `RenameColumn`, `RenameIndex`, keys dropped and re-added with the same `onDelete` (as `RenamedTasksToProcedures`); no data loss. `has-pending-model-changes` clean; up and down on LocalDB.
- **Stored names that keep the old word** (glossary): audit and activity details' keys (`specialtyId`, `specialty`); `ActivityAction` 42's member is renamed `TeamMembershipChanged` (number kept); account settings keys.
- **Wording:** screens say "team" inward, "clinic"/"the lab" outward (decision 181); `docs/agents/specialties.md` → `teams.md`; AGENTS.md's Specialties row (under 40,000 characters); a script `tools/rename-specialties-to-teams.py` brings open branches across.
- **Tests:** existing tests renamed; `SpecialtyMapperTests` → `TeamMapperTests`.
- **Light demo:** handlers' paths and seed names (`slices/setup.ts`), `parity.test.ts` green; words in the tour.

**MC3. Team kinds, account types and read-only admins** (L). After MC2.
- **Scope:**
  - `Team.Kind` (`TeamKind`: `Clinic = 0`, `Lab = 1`); **at most one lab team that isn't archived** (filtered unique index); the migration adds it with a fixed ID (`Team.LabId`, "Lab", renameable).
  - `TeamMembership.Role` (`TeamMemberRole`: `Member = 0`, `ClinicAdmin = 1`). **Account type** is computed, not stored (`AccountType` in Contracts: Admin if they hold Admin; Clinic admin if any membership is `ClinicAdmin`; else Staff), on `whoami`, `UserResponse` and Account Settings.
  - `PUT api/auth/users/{u}/teams` takes `{teams: [{teamId, role}]}` (`ManageUsers`); `TeamMembershipChanged = 42` details gain each role.
  - New Administrator-only permission **`ViewAllTeams`** (group AdministratorOnly; stored by name).
  - Decision 208's `NotForAdmin` additions; decision 209's rule.
  - Session: memberships read with roles; `medapp:team` claims; `whoami.teams` (`{id, name, kind, role}`).
  - The side nav shows only what each type uses (admins: no clinical worklists).
  - **Clinic admin's extras:** none yet beyond staff (S2-9); the type shows on the Users page and in Account Settings.
- **Migration** `AddedTeamKindsAndAccountTypes`: `Teams.Kind`, `TeamMemberships.Role`, the lab team; **every account holding a staff role but no team joins General** (nobody loses access on upgrade); every holder of the Lab role joins the lab team; accounts holding Admin keep their memberships until an admin moves them (a 409 only on the next change), with a startup warning listing them.
- **Also:** `medapp-dev`'s `ensure_test_admin` stops adding `admin` to teams; new `ensure_test_staff` (a Dermatology clinician for E2E and UI parity). **E2E** journeys sign in as a staff account they create (`CreateStaffAsync`), since `admin` can no longer do clinical work; `tools/ui-parity/.env` gains a staff account.
- **Tests:** `PermissionCatalogueTests` (Admin's new set), `AccessBeforeAndAfterPermissionsTests` rows, `TeamRulesTests`, `AccountTypeTests`, the migration's data step (integration).
- **Light demo:** **the three accounts** (`accounts.ts`): `admin` (Admin only, read-only, the clinic picker), `n.thornbury` (Clinician, **Dermatology clinic admin**), `r.aldous` (Lab, lab team staff). Tour steps that used `admin` for clinical work switch to `n.thornbury`; the Read Me's Two accounts card becomes Three accounts; `SEED_VERSION` bumped.

**MC4. The data boundary: the model, pathways and the leak tests** (XL). After MC3. **The riskiest PR: a full review, `ci:full`.**
- **Scope:** section 3.1 entire; section 3.2's MC4 rows (patients, timeline, pathways, pathway audit, attachments).
  - `TeamScope`, `TeamAccess`, `PathwayAccess`, the repository signatures (decision 203), 404s with `TeamBoundaryRefused`, `access` in activity details.
  - `OtherTeamPathwaySummary` on the patient response (`otherTeamPathways`: team name, `state` `InProgress` or `Completed`, `since`); cancelled, archived and deleted pathways aren't summarised.
  - The leak test harness (decision 205) with **every endpoint classified**: endpoints MC5 filters are listed in `PendingTeamFilters` (a fixed list the test reports; MC5 empties it and deletes it).
- **Migration** `AddedTeamBoundaries`: `TeamId` on every table in decision 200's table (backfilled from pathway, referral, appointment stage, uploader's first team, else General), `PathwayAccess` with `Owner` grants for every pathway and `StageOwner` grants for the lab team on every pathway with a specimen, indexes. Tested up and down on LocalDB with synthetic rows.
- **Docs:** new `docs/agents/teams.md` (the boundary rules: read before any query on team data) and its AGENTS.md row ("Never miss: every query on team data takes a `TeamScope`; across the boundary is a 404"); `privacy.md`, `timeline.md`, `pathways.md`.
- **Governance:** DPIA 41 to 44 move from draft to built controls; H-65, H-66.
- **Tests:** `TeamScopeConventionTests`, `TeamAccessTests`, `TeamAccessSqlTests`, the two integration suites, handler tests for each changed handler (a scope from another team: 404), `TimelineStaffCanaryTests` extended.
- **Light demo (built first, demo-first rule):** `handlers/teamWalls.ts` (walls on pathways, appointments, letters, results, consent, specimens and MDT items; a visible pathway the viewer's team doesn't own answers 409 on a change); Priya Holloway has a Gynaecology pathway and Ellis Varden a completed Diabetes one beside their Dermatology ones. Was: one `canSee(pathway)`/`canChange(pathway)` helper over seeded grants in `handlers/pathways.ts` and `patients.ts`; the patient page's other-clinic summaries (seed a patient with a Gynaecology pathway beside a Dermatology one); `n.thornbury` gets 404s for Gynaecology's.

**MC5. The data boundary: every other area** (L). After MC4. `ci:full`.
- **Scope:** section 3.2's MC5 rows: referrals, appointments (masked on calendars), specimens, results, letters, consent, MDT, Insights, side nav counts, patient messages. Empties `PendingTeamFilters`.
- **Also:** booking needs the appointment's team in scope and its clinician a member of that team (400 on `StaffUserId`); a referral redirected to another team ends the old team's view.
- **Migration:** none (MC4 added the columns).
- **Tests:** the probes for each endpoint; handler tests; `PathwayInsightsSqlTests` with a team scope.
- **Light demo (built first, demo-first rule):** referrals walled by team (or their pathway's), redirected or registered elsewhere leaves the old team; home counts and Insights by team; other teams' appointments as `otherTeamsBusy` on `CalendarResponse` ("Busy, another team"); booking 400s a clinician outside the appointment's team, and only its own team changes an appointment or referral (409). Tom Hallworth also in Gynaecology, with a Gynaecology appointment, for the tour's busy-calendar step.

**MC6. The clinic picker on every grid** (M). After MC5. ∥ MC7 to MC11.
- **Scope:** a `teamId` filter on Patients, Pathways, Referrals, Appointments, Results, Letters and Insights searches: **"All clinics"** or one team. Shown to admins (`SeesAllTeams`) and to staff in more than one team (their own only; another team is a 400). Kept in the URL (`clinic=<id>`) and remembered per page in account settings (`medapp.clinicPicker.<page>`). Patients: "has a pathway, referral or appointment in that clinic".
- **Light demo:** the picker on the same pages for `admin`.

**MC7. The relay: stages owned by a team** (L). After MC4 (MC5 for the worklist counts). ∥ MC8.
- **Scope:**
  - `PathwayTemplateMap.OwnerTeamId` (null = the pathway's team; Stage 1 allows only null or the lab team: 400 otherwise), copied onto `PatientPathwayStage.OwnerTeamId`. The template editor shows each stage's owner (a column and a select); the stage library shows none (ownership is per template).
  - `PatientPathwayStage.ReachedAtUtc`, `PickedUpAtUtc`, `PickedUpByUserId`.
  - **Handover:** when a finish (or skip, or branch choice) makes a stage current whose owner differs from the last finished stage's, `PathwayChange.HandOver` writes `StageHandedOver = 47` (`{stageId, fromTeamId, from, toTeamId, to}`), the `StageOwner` grant (if new), and tells the receiving team's members holding `WorkPathways` (`PathwayHandedOver = 44`, "Pathway handed over", "<username> passed a pathway to your team.": no patient, no stage).
  - **Pick up:** `POST …/stages/{sid}/pick-up` (`WorkPathways`, receiving team; idempotent; audit `StagePickedUp = 48`); the lab's is automatic when the case's first sample is received (MC16).
  - **Worklists** (`PathwaySearch` views, My Worklist's team tab): **Our turn** (a current stage owned by my team), **New for us** (not picked up), **Awaiting the lab** (mine, a current stage owned by the lab), **Back from the lab** (mine, reached from a lab stage, not picked up).
  - **Service alert** `MonitoringEvent.HandoverNotPickedUp = 15` (threshold in days, team condition) → `MonitoredHandoverNotPickedUp = 45`.
- **Migration** `AddedStageOwners`.
- **Governance:** H-67, H-68 controls built.
- **Tests:** `PathwayChangeHandOverTests`, the worklist SQL, `PathwayCommandsMonitoringTests`, template validators.
- **Light demo:** "Histology (lab)" and "Lab tests (lab)" stages owned by the lab in the seed's templates; handover and pick up in `handlers/pathways.ts`; the worklist views for `n.thornbury` and `r.aldous`.

**MC8. Tree pathways: the model** (XL). After MC4. ∥ MC7 (both touch `PathwayChange`: rebase carefully).
- **Scope:**
  - **Stage links:** `PathwayTemplateLinks` and `PatientPathwayStageLinks` (`FromStageId`, `ToStageId`): a directed graph with no cycles. `StageOrder` stays, for display (a topological order).
  - **Fork:** a stage with several successors, all to do. **Join:** a stage with several predecessors waits until each is finished or skipped. **Branch point** (today's `ChoosesBranch`): finishing it chooses one successor; the others' routes are skipped up to their join (`BranchNotChosen`). Branches nest.
  - `PathwayWorkflow.CurrentStages` replaces `CurrentStage` (to do, every predecessor done); `CurrentStage` stays as the first of them for callers not yet moved. The status rules count every route.
  - **Last-stage gates** (specimens, results) apply to the stage whose finish leaves none to do on any route.
  - Responses: `currentStageIds`, each stage's `predecessorIds`. Search's stage reached matches any current stage.
  - **Insights' stage times:** "reached" = the last predecessor finished or skipped.
- **Migration** `AddedStageLinks`: links built from `StageOrder` and today's branch metadata (branch point → each branch's first stage; each branch's last → the stage after the branches), so every existing pathway behaves exactly as before.
- **Tests:** `PathwayWorkflowEquivalenceTests` (every existing workflow test against a linear graph), fork and join tests, the migration's conversion (integration), `PathwaySearchSqlTests`, `PathwayInsightsSqlTests`.
- **Light demo:** the demo's workflow helper uses links; one seeded pathway with a fork (histology and bloods side by side) and its join.

**MC9. Tree pathways: the editor and the tree view** (L). After MC8.
- **Scope:** the template editor draws routes (add a parallel route, add a branch point and its branches, nest, join); `TemplateGraphRules` (one start, one end, no cycles, every stage reachable, a branch point has two named branches, owners per stage). The pathway page shows the current stage(s) first and the tree folded away (an indented outline, Astryx only, no canvas), each node with its owner team and status.
- **API:** templates take `stages` (`{stageDefinitionId, options, ownerTeamId}`) and `links`; `stageDefinitionIdsInOrder`/`stageOptions` still accepted (a chain) for one release.
- **Light demo:** the same editor and tree view against the demo's templates.

**MC10. Branches added on new information** (M). After MC9.
- **Scope:** a template's **addable branches** (`PathwayTemplateBranches`: name, its stages and links, where it may attach, `IsRepeatable`), not created with the pathway. `POST …/patient-pathways/{id}/branches` `{templateBranchId, afterStageId, reason}` (`WorkPathways`, owner team; reason required, 500) adds its stages after that stage, in parallel with what's left; the pathway joins them before its last stage. Audit **`BranchAdded = 49`** (`{templateBranchId, branch, afterStageId, stageIds}`, never the reason; the reason on a `PathwayBranchAddition` row, replaced on anonymisation). **Never suggested:** the page lists the allowed branches in template order.
- **Migration** `AddedAddableBranches`.
- **Governance:** H-69, H-70.
- **Light demo:** "Add the melanoma route" on the seeded skin pathway.

**MC11. Team calendars and the meeting rule** (M). After MC5. ∥ MC7 to MC10.
- **Scope:** the Calendar page's **team picker** (any team; defaults to your first; the team view shows its members); other teams' appointments as "Busy" (MC5's masking), their meetings as "Meeting" without title or attendees (Q16). **Meetings:** attendees must share a team with the organiser (400 naming who), **except** an MDT (`IsMdt`) and a consult meeting (`ConsultId`, MC25). Admins arrange any meeting. Booking stays limited to your team's members (MC5).
- **Light demo:** the picker and the masking in `handlers/calendar.ts`.
- **Built (demo first):** `GET api/calendar/team` takes `TeamId` (default your first team; an admin's the first by name) and answers `teamId`, `teamName`, `teamMembers`; other teams' meetings go in `otherTeamsBusy` as `what: "Meeting"` (no meeting ID); opening one is a 404 (`TeamBoundaryRefused`). A meeting is "yours" when you're in it, it's your team's MDT, or anyone in it shares a team with you. The Team view and picker are for everyone (no longer `ArrangeMeetings` only). The meeting rule's 400 is on `attendeeUserIds`. Backend to follow.

**MC12. Phase 1 demo data** (M). Last in the phase.
- **Full demo** (`docs/demo-data.md`): the five clinics plus the lab as teams; each person in their team (`r.aldous` and `c.dunmore`'s lab work in the lab team; `d.quince` in three teams); `n.thornbury` Dermatology's clinic admin; `admin` a pure admin and `j.whitlock` Admin (their clinical work moves to `m.carrow` and the clinicians); stage owners on every lab stage; handovers picked up and one not (on time for the alert); one forked pathway and one with an added branch; a patient seen by two clinics (the summary). Through the commands, as now.
- **Light demo:** the tour rewritten: who's who (three accounts), **clinic admin versus staff** (what `n.thornbury` has that staff don't, and that the extras are coming), the other-clinic summary, the relay (finish the biopsy stage, switch to `r.aldous`, see it arrive), the tree view, a team calendar with busy time. `tour.test.ts` green.

## 5. Phase 2: the lab (MC13 to MC22)

**Words:** screens say **sample** (glossary, screens only); the code keeps `Specimen` (FHIR's word; Q5). A **lab case** is the lab's unit of work: one request, one or more samples, its steps, one report.

**MC13. The test catalogue, and the external labs list dropped** (M). After Phase 1. ∥ MC18.
- **Scope:** `LabTest` (`ManageSetup`, `api/lab-tests`): name, `Kind` (`LabTestKind`: `Histology = 0`, `Cytology = 1`, `Haematology = 2`, `ClinicalChemistry = 3`, `Microbiology = 4`, `Other = 5`), SNOMED CT procedure code (optional), **`TargetDays`** (default 7) and **`LimitDays`** (default 10) in calendar days, `AtRiskDays` (default 2 before the due date), and ordered **steps** (`LabTestSteps`: name, `Kind` (`LabStepKind`: `Standard = 0`, `ExtraTests = 1`, `SentForSecondOpinion = 2`, `SecondOpinionReturned = 3`), optional or not). Archived once used. `SpecimenType` gains a SNOMED CT specimen code (FHIR `Specimen.type`).
- **Dropped:** `Laboratory`, `api/laboratories`, `Specimens.LaboratoryId` (the audit log keeps each lab's name as written); a "second opinion" step records where it went as text.
- **Migration** `AddedLabTestsAndDroppedLaboratories` (**drops data on purpose**: say so in the PR; reviewers check it's the labs list only).
- **Light demo:** lab tests in `slices/lab.ts` and the setup page; the Labs page removed.

**MC14. The samples database** (L). After MC13.
- **Scope:** `Specimen.Source` (`SpecimenSource`: `Clinic = 0`, `Hospital = 1`), `RequestingTeamId` (a clinic), `HospitalId` (MC18), `LabTestId`, `LabCaseId` (MC15); `PathwayId` and stage nullable (a hospital sample has none). Numbers, labels and statuses carry over unchanged. **The Samples page** (the Lab page reworked): grid tabs (To receive, In the lab, Reported, Rejected and lost, All) and search (number, patient, source, clinic, test, status, due), **where it came from** on every row (clinic name or hospital). Clinic staff see their team's samples (decision 200); the lab sees all.
- **Migration** `ReworkedSpecimensIntoSamples` (existing rows: `Source = Clinic`, the pathway's team).
- **Light demo:** sources in the seed; the reworked page.

**MC15. Lab cases and their steps** (XL). After MC14 and MC7.
- **Scope:** `LabCase` (number from the specimen sequence's prefix, `LabTestId`, patient, `PathwayId?` and stage, `HospitalRequestId?`, `Status` (`LabCaseStatus`: `Open = 0`, `Reported = 1`, `AwaitingSurgeryDetails = 2`, `Closed = 3`, `RepeatRequested = 4`, `EnteredInError = 5`), received time, due dates (MC19), `Version`); `LabCaseSteps` (each recorded step: who, when; optional ones added from the test's list). `LabCaseChange` is the only way to change one. The report: a file (`AttachmentIntake`), **authorised** by a holder of the new permission **`AuthoriseLabReports`** (new starting role **Pathologist**; not Admin), which reports the case's samples and, for a clinic case, makes the result (as today's specimen report). **Insufficient or rejected sample:** `RepeatRequested`, telling the clinic (`RepeatSampleRequested = 46`); the clinic adds a repeat branch (MC10).
- **Audit:** `LabCaseOpened = 50`, `LabStepRecorded = 51`, `LabStepEnteredInError = 52`, `LabReportAuthorised = 53`, `RepeatSampleRequested = 54`, `LabCaseEnteredInError = 55`. Reads: `LabCaseViewed = 55`.
- **Migration** `AddedLabCases`.
- **Light demo:** cases and steps in `slices/lab.ts`; authorising without a file (the report file is "not in the light demo").

**MC16. Lab stages in clinic pathways** (M). After MC15.
- **Scope:** a lab-owned stage reached by a handover opens its case (one per stage; the samples taken at the clinic's stage join it); receiving the first sample picks it up; **authorising the report finishes the lab stage** in the same save (`PathwayChange`, `StageFinished` then `StageHandedOver` back), so the pathway lands on the clinic's "Back from the lab". The clinic signs the result off (today's sign-off). Gate: a lab stage can't be finished by hand while its case is open (`StageGates.WhyLabCaseOpen`, 409).
- **Light demo:** the loop in the handlers.

**MC17. The lab's home and worklists** (M). After MC16.
- **Scope:** the lab team's home (`/lab`): **To receive**, **In progress**, **To report**, **Overdue**, **Back to the clinic or hospital** (last 7 days); clinic pathways' lab stages appear when it's the lab's turn. The side nav for lab staff leads with it.
- **Light demo:** the home for `r.aldous`.

**MC18. Hospitals and hospital requests** (L). After Phase 1. ∥ MC13.
- **Scope:** `Hospitals` (`ManageSetup`, `api/hospitals`): name, ODS code, contact, report delivery method (`LetterDispatchMethod`), default deadline days. **The hospital request** (`HospitalRequest`, FHIR `ServiceRequest-Lab`'s shape): hospital, the hospital's patient number, NHS number (999 range in tests), surgeon (name as written), `Priority` (`LabRequestPriority`: `Routine = 0`, `Urgent = 1`, `Asap = 2`, `Stat = 3`, FHIR's codes), `IsCancerPathway`, `AuthoredOnUtc`, **`ReportNeededBy`** (`occurrence`), collection time and SNOMED CT specimen type (`Specimen`). Registered by hand (`ProcessSpecimens`; `POST api/hospital-requests`); it opens a lab case with no pathway. **The patient:** matched to an existing record by a person (never automatically, H-72), or created; **a patient whose only link is a hospital case is hidden from non-lab staff's search** (`Patient` search excludes them unless they have a pathway, referral or appointment) until they do.
- **Migration** `AddedHospitalRequests`.
- **Governance:** DPIA 45, H-72; the hospital in the ROPA.
- **Light demo:** one hospital and requests in the seed; the intake form.

**MC19. Strict clocks** (M). After MC15 and MC18.
- **Scope:** on each case: `LabDueOn` = received + the test's `TargetDays` (calendar days, UK dates); `HospitalDueOn` = the request's `ReportNeededBy`, else for a cancer pathway collection + 10 days, else received + the hospital's default; **`DueOn` = the sooner**, and which one set it. **At risk** from `DueOn` − `AtRiskDays`; **breached** after `DueOn` until reported. Countdown on the case and the worklists; the Overdue list; every clock for the lab team (and its clinic admin). Service alerts `LabCaseAtRisk = 16`, `LabCaseOverdue = 17` → `MonitoredLabCaseAtRisk = 47`, `MonitoredLabCaseOverdue = 48`. Insights: % reported within 7 and within 10 days, by test and source (counts and spans only). **The second opinion keeps the clock running.**
- **Migration** `AddedLabClocks`.
- **Governance:** H-71; the device table's clock row.
- **Light demo:** due dates computed client-side; seeded cases on time, at risk and breached.

**MC20. Reports to the hospital and surgery details** (M). After MC19.
- **Scope:** **the report letter:** `LetterKind.LabReport = 6`, `LetterRecipientKind.Hospital = 4`, rendered by the letters engine with the authorised report attached, **recorded as sent** (`LabReportSent = 56`); secure delivery later. The case goes `AwaitingSurgeryDetails`. **Surgery details:** a lab member adds the date, procedure and outcome **as written** (`SurgeryDetailsAdded = 57`, no text in the audit); the case closes (`LabCaseClosed = 58`); "No surgery expected" closes it with a detail. Nothing is calculated from them.
- **Migration** `AddedSurgeryDetails`.
- **Governance:** H-74, DPIA 46.
- **Light demo:** surgery details and closing; the PDF and sending are "not in the light demo" (record as sent works).

**MC21. Pathology at MDTs** (S). After MC5. ∥ any Phase 2 PR.
- **Scope:** lab members (Pathologist) can attend any MDT (the MDT exception, MC11) and be an item's **presenter** (`MdtItem.PresenterUserId`); the item shows its lab case and report.
- **Migration** `AddedMdtPresenters`.
- **Light demo:** a presenter on a seeded item.

**MC22. Phase 2 demo data** (M). Last in the phase.
- **Full demo:** the test catalogue (histology, bloods, microbiology), one hospital (invented, "Riverside General (demo)"), samples in every state from clinics and the hospital, clocks on time, at risk and breached, a second opinion, a repeat requested, reports sent, surgery details added and one awaited.
- **Light demo:** the same, smaller; tour steps for the lab's home and a hospital case as `r.aldous`.

## 6. Phase 3: Dermatology (MC23 to MC29)

**MC23. Dermatology pathways as trees** (M). After Phase 2.
- **Scope:** templates through the commands (`DemoData`, and the light demo's seed): **Suspected skin cancer** (triage, consultation and dermoscopy, biopsy, lab histology, results review → benign: GP letter and discharge | low-risk BCC: excision, lab margins, follow-up | melanoma or high-risk SCC: MDT with pathology, refer to hospital surgery, surveillance), **Lump and bump removal** (… unexpected malignancy: an addable branch joining the skin cancer route), **Inflammatory skin** (bloods before treatment; monitoring bloods as a repeatable addable branch; responding or not → MDT).
- **Light demo:** the same three templates.

**MC24. The team home and clinic worklist** (M). ∥ MC23.
- **Scope:** a home for a clinic team (`/team`): **Triage**, **My pathways**, **Back from the lab**, **Results to sign off**, **Appointments to book**; built for every clinic team, Dermatology first.
- **Light demo:** for `n.thornbury`.

**MC25. Consults between clinics, and with the lab** (L). ∥ MC23.
- **Scope:** `Consult` (pathway, asking team, asked team (a clinic or the lab), question (2,000), reply (4,000), `Status` (`ConsultStatus`: `Open = 0`, `Replied = 1`, `Closed = 2`, `Withdrawn = 3`), `Version`); the asked team gets a read-only `Consult` grant **until the consult closes**; their reply is recorded on the pathway; a consult meeting may include the asked team (MC11). Audit `ConsultRequested = 59`, `ConsultReplied = 60`, `ConsultClosed = 61`, `ConsultWithdrawn = 62` (no text). Notifications `ConsultRequested = 49`, `ConsultReplied = 50`. Restricted: closing and withdrawing allowed.
- **Migration** `AddedConsults`.
- **Governance:** DPIA 47, H-75.
- **Light demo:** a consult from Dermatology to the lab (`r.aldous` replies).

**MC26. Cross-clinic MDTs** (M). After MC25.
- **Scope:** an MDT's attendees from several teams and the lab get an `Mdt` grant per listed item, by `UserId`, read-only, from listing until the item is discussed or withdrawn (and at least to the meeting's end). **Attendees see the listed cases only.**
- **Light demo:** one cross-clinic MDT.

**MC27. Clinical photos** (S). ∥.
- **Scope:** an attachment marked **clinical photo** (`Attachment.IsClinicalPhoto`), only with a valid photo consent for the pathway (a consent form "Clinical photography", `ConsentRecord`), shown as now (thumbnail or download, never inline at full size). No new enum.
- **Migration** `AddedClinicalPhotoFlag`.
- **Governance:** DPIA 48, H-76.
- **Light demo:** files aren't in the light demo; the consent form is seeded and the reason listed.

**MC28. Letters to the GP and the hospital** (S). ∥.
- **Scope:** Dermatology letter templates (seeded), `LetterKind.OnwardReferral = 7` (a referral to hospital surgery), the hospital as a recipient (MC20's `Hospital = 4`).
- **Light demo:** the templates and an onward referral letter (no PDF).

**MC29. Phase 3 demo data** (M). Last.
- **Full demo:** Dermatology patients on every route, one going to the lab and back, one at a cross-clinic MDT, one consult, photos with consent.
- **Light demo:** the tour's Dermatology story, start to end, across the three accounts.

## 7. Stored numbers

Never renumber; add new values with new numbers. Reserved by the earlier plan and not built: `ActivityAction` 49, 51, 52; `PatientPathwayAuditAction` 45, 46; `NotificationKind` 43; `AppointmentEventAction` 10; `AttachmentScanStatus` 3. Permissions are stored by name.

| Enum | Values | PR |
|---|---|---|
| `ActivityAction` (added) | `TeamMembershipChanged = 42` (renamed member, same number), `TeamBoundaryRefused = 54` | MC2, MC4 |
| | `LabCaseViewed = 55` | MC15 |
| `PatientPathwayAuditAction` (added) | `StageHandedOver = 47`, `StagePickedUp = 48` | MC7 |
| | `BranchAdded = 49` | MC10 |
| | `LabCaseOpened = 50`, `LabStepRecorded = 51`, `LabStepEnteredInError = 52`, `LabReportAuthorised = 53`, `RepeatSampleRequested = 54`, `LabCaseEnteredInError = 55` | MC15 |
| | `LabReportSent = 56`, `SurgeryDetailsAdded = 57`, `LabCaseClosed = 58` | MC20 |
| | `ConsultRequested = 59`, `ConsultReplied = 60`, `ConsultClosed = 61`, `ConsultWithdrawn = 62` | MC25 |
| `NotificationKind` (added) | `PathwayHandedOver = 44`, `MonitoredHandoverNotPickedUp = 45` | MC7 |
| | `RepeatSampleRequested = 46` | MC15 |
| | `MonitoredLabCaseAtRisk = 47`, `MonitoredLabCaseOverdue = 48` | MC19 |
| | `ConsultRequested = 49`, `ConsultReplied = 50` | MC25 |
| `MonitoringEvent` (added) | `HandoverNotPickedUp = 15` | MC7 |
| | `LabCaseAtRisk = 16`, `LabCaseOverdue = 17` | MC19 |
| `LetterKind` (added) | `LabReport = 6` | MC20 |
| | `OnwardReferral = 7` | MC28 |
| `LetterRecipientKind` (added) | `Hospital = 4` | MC20 |
| `TeamKind` | `Clinic = 0`, `Lab = 1` | MC3 |
| `TeamMemberRole` | `Member = 0`, `ClinicAdmin = 1` | MC3 |
| `PathwayAccessReason` | `Owner = 0`, `StageOwner = 1`, `Consult = 2`, `Mdt = 3` | MC4 |
| `LabTestKind` | `Histology = 0`, `Cytology = 1`, `Haematology = 2`, `ClinicalChemistry = 3`, `Microbiology = 4`, `Other = 5` | MC13 |
| `LabStepKind` | `Standard = 0`, `ExtraTests = 1`, `SentForSecondOpinion = 2`, `SecondOpinionReturned = 3` | MC13 |
| `SpecimenSource` | `Clinic = 0`, `Hospital = 1` | MC14 |
| `LabCaseStatus` | `Open = 0`, `Reported = 1`, `AwaitingSurgeryDetails = 2`, `Closed = 3`, `RepeatRequested = 4`, `EnteredInError = 5` | MC15 |
| `LabRequestPriority` | `Routine = 0`, `Urgent = 1`, `Asap = 2`, `Stat = 3` | MC18 |
| `ConsultStatus` | `Open = 0`, `Replied = 1`, `Closed = 2`, `Withdrawn = 3` | MC25 |

**New permissions** (stored by name): `ViewAllTeams` (MC3, Administrator-only), `AuthoriseLabReports` (MC15, group Lab; new starting role **Pathologist**; in `NotForAdmin`). Everything else reuses existing permissions: hospital intake, steps and surgery details `ProcessSpecimens`; consults and adding branches `WorkPathways`; lab tests and hospitals `ManageSetup`; memberships `ManageUsers`.

**Migrations, in order:** `RenamedSpecialtiesToTeams` (MC2), `AddedTeamKindsAndAccountTypes` (MC3), `AddedTeamBoundaries` (MC4), `AddedStageOwners` (MC7), `AddedStageLinks` (MC8), `AddedAddableBranches` (MC10), `AddedLabTestsAndDroppedLaboratories` (MC13), `ReworkedSpecimensIntoSamples` (MC14), `AddedLabCases` (MC15), `AddedHospitalRequests` (MC18), `AddedLabClocks` (MC19), `AddedSurgeryDetails` (MC20), `AddedConsults` (MC25), `AddedClinicalPhotoFlag` (MC27)., `AddedMdtPresenters` (MC21).

## 8. Safety, governance and the device position

**Hazards (drafted in MC1; each PR builds its controls):**

| Hazard | Cause | Controls | PR |
|---|---|---|---|
| **H-65 Wrong team** | A pathway, referral or appointment is in the wrong team, so its clinicians don't see it, or another team does | A pathway takes its template's team; the team on every page header; redirecting a referral is explicit and audited; the leak tests | MC4, MC5 |
| **H-66 Hidden by the boundary** | A clinician acts without knowing of relevant care in another clinic | The other-clinic summary on the patient page; consults (MC25); the deploying organisation's sharing policy (DCB0160) | MC4, MC25 |
| **H-67 A missed handover** | A pathway handed to a team sits unseen | The handover notification; New for us and Back from the lab views; pick-up; the `HandoverNotPickedUp` alert (a convenience, never the safety net); the daily check (DCB0160) | MC7 |
| **H-68 A stage owned by the wrong team** | A template error sends a stage to the wrong worklist | The owner shown in the template editor and on each stage; only the pathway's team or the lab allowed in Stage 1; the go-live template review (DCB0160) | MC7 |
| **H-69 A parallel route forgotten** | A pathway is discharged, or a join waits forever, with a route still open | Discharge needs every route finished or skipped; the tree view shows every current stage; the last-stage gates across routes | MC8, MC9 |
| **H-70 A branch added wrongly, or not added** | New information isn't acted on, or the wrong route is added | Branches only from the template's list, with a reason, audited; never suggested; the repeat request tells the clinic | MC10, MC15 |
| **H-71 A hospital clock wrong or missed** | A deadline mistyped or missing, the wrong one taken as the sooner, a late tumour report delays surgery | Both dates and which one applies shown; UK dates, end of day; at risk before breach; the Overdue list; alerts; the second opinion keeps the clock running; the daily check | MC19 |
| **H-72 A hospital sample on the wrong patient** | Matched by NHS or hospital number to the wrong record, or reported to the wrong hospital | Matching by a person, never automatic; the banner with both numbers on every case; the report addressed from the request's hospital | MC18, MC20 |
| **H-73 A sample mix-up across sources** | A clinic's and the hospital's samples confused in one database | The source on every row and label; one number sequence; authorising names the case and patient | MC14, MC15 |
| **H-74 Surgery details on the wrong case** | Details typed against another case | The case's banner and number in the dialog; entered in error | MC20 |
| **H-75 A consult reply missed** | The asking team decides before or without the reply | The reply notification; open consults on the pathway; the consult closes only by the asking team | MC25 |
| **H-76 A clinical photo on the wrong patient, or without consent** | A photo filed to another pathway, or taken without photo consent | The consent gate; the banner on upload; entered in error | MC27 |

**DPIA risks 41 to 48 (drafted in MC1):** **41** clinic separation fails (a missed filter shows another clinic's details; controls: `TeamScope` in signatures, the leak and canary tests, 404s logged); **42** centre-wide patient search reveals that someone attends a clinic (the summary names it; Stage 2 hides sensitive clinics' names, Q18); **43** the lab's lasting read access to passed pathways, later notes included (logged with `access`, reviewable); **44** admins' read-all access (no clinical changes, two-factor, every read logged with `access: Admin`); **45** hospital patients as new data subjects and a new source (the controller or processor question, retention, hidden from non-lab search); **46** reports sent to the hospital (recorded as sent, no secure transport yet); **47** consult and MDT grants widen access (read-only, ended when closed); **48** clinical photos (special category images; consent; files only through the API).

**Data inventory:** `TeamMemberships.Role`, `PathwayAccess`, `HospitalRequests` (hospital patient number, surgeon's name), `LabCases` and steps, surgery details (free text), `Consults` (free text), clinical photos; the hospital as a source and recipient.

**The device position:**

| Feature | Position |
|---|---|
| Teams, boundaries, the relay, worklists | Not a device: access control and workflow |
| Tree pathways, added branches | Not a device: staff choose; nothing suggested |
| Hospital clocks and lab alerts | Not a device: **service alerts on time**, never on values (decision 186) |
| Lab cases, steps, reports | Not a device: workflow; reports stay documents |
| Surgery details | Not a device: recorded as written; nothing calculated |
| Consults, MDTs, photos | Not a device: communication and storage; photos not analysed |

**Claims the manufacturer must not make** gain "MedApp prioritises samples by clinical urgency" and "MedApp decides which route a pathway takes".

## 9. Open technical questions (each decided as recommended)

1. **Explicit scopes or EF global query filters?** **Explicit** `TeamScope` parameters (compile-time) plus the leak tests. Global filters would hide other teams' appointments from the clash checks, and jobs and the seed need everything.
2. **A grants table or visibility computed from history?** **A table** (`PathwayAccess`): one indexed `EXISTS`, one place the reason comes from for the activity log; written only with the event.
3. **404 or 403 across the boundary?** **404**, logged as `TeamBoundaryRefused`.
4. **Rename `Specialty` to `Team` in code?** **Yes**, one mechanical PR (MC2), merged alone; stored detail keys keep `specialty`.
5. **Rename `Specimen` to `Sample` in code?** **No**: screens only (FHIR's word is Specimen).
6. **Lab cases as lab-team pathways, or their own entity?** **Their own entity:** fixed steps per test, clocks, and hospital cases without a pathway; pathways' assignments, referrals and clocks don't fit.
7. **Hospital patients as `Patient` records?** **Yes** (export, anonymisation, retention and data requests work), hidden from non-lab search until they have care at the centre.
8. **An admin who also does clinical work?** **Two accounts;** admins hold no team.
9. **Staff in several teams?** **Allowed** (visiting consultants, pharmacists); most have one; the picker shows for several.
10. **The tree model?** **Directed links** between stages, `StageOrder` kept for display; today's branch metadata converted.
11. **Stage owner as a team ID or a "clinic or lab" enum?** **A nullable team ID** (null = the pathway's team), limited to the lab in Stage 1, so a shared-care clinic owning a stage needs no migration later.
12. **Registering a referral for another team?** **Allowed** (sending outward); the registrar doesn't keep access.
13. **What the lab keeps after hand-back:** **full read of the pathway as it goes on** (decision 184), logged with `access: StageOwner`; freezing it at hand-back is the alternative if the DPO prefers.
14. **How long an MDT grant lasts:** **until the item is discussed or withdrawn**, at least to the meeting's end.
15. **Clock details:** calendar days; the lab's from receipt (RCPath), the hospital's as given (end of that UK day); a cancer pathway with no date: collection + 10 days (NHS England's standard); at risk 2 days before by default; a hospital request can be entered without a date (the lab's applies).
16. **Other teams' meetings on a calendar:** **"Meeting", busy,** without title or attendees.
17. **A file on the patient (no pathway):** the uploader's team; shown to that team only.
18. **Sensitive clinics** (psychology): a Stage 2 team flag `HiddenInSummaries` shows "a pathway in another clinic".
19. **The General team:** stays; staff without a team join it on upgrade; an admin archives it once its pathways have moved (moving a pathway between teams is a Stage 2 to-do).
20. **Hospital requests through FHIR?** **By hand now,** DTOs named after FHIR's elements; a FHIR intake and `DiagnosticReport` output are later work with the hospital's agreement.
21. **E2E and UI parity sign in as `admin` today:** after MC3 they make and use a staff account (`CreateStaffAsync`, `ensure_test_staff`).

## 10. The order, and what runs in parallel

- **MC1 ∥ MC2.** MC2 merges **alone** (no other code PR open).
- **MC3** after MC2. **MC4** after MC3 (alone: it changes most repositories).
- After MC4: **MC5 ∥ MC7 ∥ MC8** (MC7 and MC8 both touch `PathwayChange`: whoever merges second rebases).
- After MC5: **MC6 ∥ MC11**. After MC8: **MC9**, then **MC10**.
- **MC12** last in Phase 1.
- Phase 2: **MC13 ∥ MC18 ∥ MC21**; MC14 after MC13; MC15 after MC14; **MC16 ∥ MC19** after MC15 (MC19 also after MC18); MC17 after MC16; MC20 after MC19; **MC22** last.
- Phase 3: **MC23 ∥ MC24 ∥ MC25 ∥ MC27 ∥ MC28**; MC26 after MC25; **MC29** last.
- Testing once per phase (the build agents' rules): the full suite, the leak tests and the owner's look at the screens at each phase's end; MC4 and MC5 get `ci:full` regardless.
- **Demo first** (the owner, 18 November 2026): each Stage 1 PR is **web and light demo only** (`MedApp.Web`, its hand-edited `schema.d.ts`, `src/demo`). A final backend stage implements the same API shape, with one combined migration, the leak tests and the dev tooling's switch to a staff account (`ensure_test_staff`, E2E, UI parity); the local dev app is updated once, at the end. Tests that compare the web with the API (such as `permissionCatalogue.test.ts`) are expected to fail until then. MC3's web side: `src/auth/accountTypes.ts`, `ViewAllTeams` and `NOT_FOR_ADMIN` in `permissions.ts`, whoami's `accountType` and `teams`, `PUT …/teams` taking `{teams: [{teamId, role}]}`, and the demo's three accounts.

## 11. Stage 2 (one entry per clinic; each its own plan later)

- **S2-1 Dietetics:** weight and BMI recorded over time (as written, never banded), repeat appointment series (S2-8).
- **S2-2 Eye clinic:** eye images as files, visual acuity records, recalls.
- **S2-3 Audiology:** audiograms as files, hearing aid fittings and repairs; children's hearing by consult with Paediatrics.
- **S2-4 Biomechanics and podiatry:** gait assessments, orthotics (prescribed, made, fitted), nail surgery, diabetic foot reviews.
- **S2-5 Psychiatry and psychology:** sealed notes (tighter access within the team), session series, outcome forms as given; the team's `HiddenInSummaries` flag (Q18).
- **S2-6 Respiratory nursing:** readings over time (oxygen, peak flow), charted, **no alerts from values**; repeat and home visits.
- **S2-7 Community paediatrics:** parents and carers with parental responsibility, safeguarding flags, school letters, consults with other clinics.
- **S2-8 Repeat appointment series** (shared, built once): a series on a stage (every N weeks, M times), booked and moved as one.
- **S2-9 Clinic admin extras:** a clinic oversight tab (the team's Insights and workload), and the team's own setup (its pathway and letter templates), scoped to their team.
- **Also later:** moving a pathway between teams; a stage owned by another clinic (shared care); a FHIR intake from the hospital; secure delivery of reports.
