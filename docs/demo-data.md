# The demo data: a guided tour

The demo data is the **light demo's clinic** (`MedApp.Web/src/demo`), in the full app: one clinic, **Dermatology**, and the **lab**, with the same people, templates, patients and pathways at the same points (the owner, 19 November 2026). It's replayed through the app's own handlers by the people who did the work, so its audit trails, notifications, grants and gates are the app's.

Everything is invented: names, `@example.com` patient emails, `@centre.example` staff emails, NHS numbers from the 999 test range, no real people, places or organisations. **Nothing clinical is decided by the seed:** outcomes, results and letters are what a member of staff "entered".

## Loading it

**One script loads the demo data, and it's the only way demo data gets in.** Never add demo or dummy records by hand, with SQL or through the app: change the seeder (`tools/MedApp.DevMcp/DemoData`) and run the script again.

1. Start the AppHost once (`dotnet run --project MedApp.AppHost`), so the SQL Server and Azurite containers exist.
2. Run the script from the repo root:

   ```bash
   tools/seed-demo-data.sh --confirm --keep-users dev-parity --out demo-summary.json     # Git Bash, macOS, Linux
   tools\seed-demo-data.ps1 --confirm --keep-users dev-parity --out demo-summary.json    # PowerShell
   ```

   It builds the seeder, checks the database is a local one, applies any pending migrations, saves a `pre-reset-<timestamp>` snapshot, then **wipes the data** and replays the clinic's story. It takes under a minute. Without `--confirm` it changes nothing.
3. **Keep its summary** (`--out` writes it to a file; without it, it's printed). It holds the generated passwords (`accounts`), `tour` (the IDs of the records below: pathways, samples, lab cases, letters, MDTs) and the counts.

   The summary holds passwords for local accounts: don't commit it.

**Other ways in, all the same seeder:**
- **From Claude Code:** `medapp-dev`'s `reset_demo_data` with `confirm: true` and `keepUsers: ["dev-parity"]` runs the same code as the script (build the tool first: `dotnet build tools/MedApp.DevMcp`). It doesn't apply migrations: it refuses, and names the script, if any are pending.
- **The seeder alone:** `dotnet run --project tools/MedApp.DevMcp -c Release -- seed-demo --confirm`, when the database is already migrated. `seed-demo --help` lists its options (`--keep-users`, `--seed`, `--out`, `--no-snapshot`, `--save-snapshot`, `--check`).
- **On kind:** `deploy/kind/up.sh --demo-data kind-demo` restores a snapshot of a database the script loaded: add `--save-snapshot kind-demo` to the script's options to save one (`deploy/kind/README.md`).

**It only runs against a local database** (LocalDB, or SQL Server on localhost): by default the AppHost's SQL container. `admin` is always kept. To load another local database, set `ConnectionStrings__DefaultConnection` first.

**Times are relative to now,** as the light demo's are: what happened "this morning" is the last weekday's when it's run before then or at a weekend, and visits keep to weekdays and off the closed days.

**Every account has a calendar today and this week,** as in the light demo (the owner, 20 November 2026): blocks for Nadia Thornbury, Ravi Aldous (cut-up, reporting, quality checks) and the admin (rota planning, a governance review) on weekdays this week and next. **Loaded on a Saturday or Sunday,** the centre opens at the weekend and the three work it (09:00 to 13:00), with weekend sessions on their calendars. The light demo also seeds itself again each day; the full app's data stays as loaded until the script runs again.

**Needs your attention shows for every account:** Nadia Thornbury's results, letters and referrals; Ravi Aldous's lab cases at risk, overdue and ready to report; and the admin's data requests, from Tobias Lund's own request (received 26 days ago, identity checked in person, due within the week; `tour.dataRequest` in the summary).

**Files:** the GP practice's report is stored only when Azurite answers on `127.0.0.1:14331`. Otherwise the summary says files were skipped (the result's record stays).

## The clinic

### The people

| Who | Username | Roles and team |
|---|---|---|
| The centre administrator | `admin` / `admin` | Admin alone: read only, every team's records, accounts and setup; in no team |
| Nadia Thornbury, consultant dermatologist | `n.thornbury` | Clinician; Dermatology's clinic admin |
| Ravi Aldous, consultant histopathologist | `r.aldous` | Lab and Pathologist; the lab |
| Jo Whitlock, service manager | `j.whitlock` | Manager; Dermatology (she assigns its pathways) |
| Bea Sandiford, clinical nurse specialist | `b.sandiford` | Nurse; Dermatology (out all day on the next two open days) |
| Hannah Marsh, booking coordinator | `h.marsh` | Booking; Dermatology (she registers its referrals and books its visits) |
| Tom Hallworth, GP with an extended role | `t.hallworth` | Clinician; Dermatology (Tuesday to Thursday) |

The light demo has Jo Whitlock and Hannah Marsh in no team; the full app's team walls need them in Dermatology for the work they do.

### Templates

Dermatology's ten reviewed templates (`docs/pathways/dermatology.yaml`) and the tour's "Skin sample to the lab" (Triage, the lab's Testing, Results appointment, Refer to GP for medication), mapped as the light demo maps them (`DemoDermatology`): choice nodes and outcomes, the lab's stages, addable and repeat routes, letters and consent on the shared stage definitions (one per name).

### Patients and where they are

| Patient | Pathway | Where it is |
|---|---|---|
| Ellis Varden | Suspected skin cancer (from his referral) | Take biopsy to book; consent withdrawn; Nadia Thornbury's draft letter; on today's MDT |
| Priya Holloway | Suspected skin cancer | The lab's Histology, its case worked up to the report; Tom Hallworth's letter to approve |
| Samuel Okafor | Suspected skin infection (Tom Hallworth's) | On hold; his scraping overdue at the lab; letter approved, to send; two hospital requests |
| Tobias Lund | Changing mole | Unassigned, at Triage; a GP report for Tom Hallworth to sign off |
| Ilona Petrova | Inflammatory skin, and Suspected skin infection | Bloods: first sample rejected, repeat in the lab, follow-up booked, letter sent. Scraping: taken this morning, not sent |
| Harriet Quill | Suspected skin cancer | Refer to hospital, waiting for the onward referral (a draft); result signed off; presented at today's MDT by the lab |
| Felix Moreton | Suspected skin cancer | Dermoscopy outcome to record; a consult open with the lab; on the MDT ahead |
| Aisha Khoury | Suspected skin cancer | A second lesion's route added; its sample too small, so a repeat requested; discussed at today's MDT; a referral awaiting triage |
| Callum Ashby | Suspected skin cancer | Back from the lab: its result awaits Nadia Thornbury's sign-off |
| Nora Fennick | Changing mole | Take photographs, needing her photo consent |
| Leah Corrin | Skin sample to the lab (the tour's relay) | Triage seen yesterday, needing her consent; her sample's case ready for results |
| Oliver Maddox | — | An urgent referral awaiting triage |
| Grace Abernethy | — | A record with nothing on it |
| Wilfred Ashdown, Maya Thistlewood | — | The hospital's patients only: requests in the lab (one closed, one awaiting surgery details, one at "Needs more work?", one just received) |

## What the full app can't hold as the light demo does

- **A template stage's own notes:** the model has a description per stage definition, shared by name, so stages show the definition's.
- **Leah Corrin's Triage needing a visit and consent:** there's no command to change a pathway's stage gates, so the seed sets them on her stage, as the light demo's seed does.
- **Tom Hallworth at the morning MDT:** it's before his working day, which the app refuses, so he isn't invited.
- **Calendar blocks over a visit or meeting:** the app refuses them, so those are left out.
- **Visits and consent the gates need:** where the light demo finishes a stage without them, the story adds them.
- **Ellis Varden's target date** is his referral's waiting-time clock's, not a date typed in.
- **The retention review's made-up record** (Grace Abernethy's care ended eight years ago): the full app works it out from the record, so she isn't due.
