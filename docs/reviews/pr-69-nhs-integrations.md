# Review: PR #69, NHS numbers, a patient banner, a FHIR patient API, SNOMED CT codes and a demographics lookup

**PR:** [#69](https://github.com/Aliator/MedApp/pull/69), branch `feature/nhs-integrations`, reviewed at `38df896`. Only the NHS work was reviewed: `git diff 7b09528..38df896`. The branch is built on #68 (the privacy features, not yet merged, whose head is `7b09528`), so that is the base it was compared with, not `main`. `main` is at `29d2d50` (the merge of #66), which the branch doesn't contain yet; it will be brought up to date after #68 merges. Line numbers below are for `38df896`.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a local build and test run, a generated migration script, or a scratch program run against a throwaway SQL Server container. **No finding is high or critical;** one is medium and seven are low.

## Summary

The PR starts roadmap step 9: an optional NHS number on patients (checked, unique, searchable, exported and anonymised), a patient banner on the pages about one patient (the control for the wrong-patient hazards H-01 and H-02), a read-only FHIR R4 Patient API in the UK Core shape, SNOMED CT codes on stage definitions, and a demographics (PDS) lookup that is off by default and has only an offline stub. The core is right: both check-digit algorithms are exact, the migration only adds, the new authorisation handler leaves every other endpoint as it was, and every suite passes. The gaps are at the edges: the banner is missing from Edit Patient, a rare race writes the NHS number into the error log, and the lookup and the FHIR resource each have a case that misleads.

## What changed, and why

169 files, 6,132 lines added and 158 removed, of which 1,230 are generated (the migration's designer file and `src/api/schema.d.ts`).

- **The NHS number** (`Patient.NhsNumber`, `varchar(10)`): the national identifier for patients in England and Wales, which MedApp lacked (hazard H-01's first cause).
  - **Checked** by `NhsNumbers` (`MedApp.Domain/Patients/NhsNumbers.cs`) with its Modulus 11 check digit, a built-in typo check like the last digit of a bank card number, on the server (`NhsNumberRules`) and as it's typed (`MedApp.Web/src/utils/nhsNumber.ts`). Spaces are allowed and removed; dashes are refused.
  - **Unique when present:** a filtered unique index, `IX_Patients_NhsNumber`, which ignores patients without one, like a register where blank lines don't count as duplicates. The handlers check first and return a 409; the repository turns the index's own refusal into the same 409 if two saves race past the check.
  - **Everywhere a patient goes:** the patient response, the list (a column and a partial-match filter), the subject access export, and anonymisation (which removes it). The activity log records the field or filter name `nhsNumber`, never the number.
- **The patient banner** (`MedApp.Web/src/components/patients/PatientBanner.tsx`): `SURNAME, Forename`, born with age, and the NHS number as `999 000 0018`, with tokens for restricted, disputed or anonymised records. It sits under the page header through a new `banner` prop on `Page`, on the patient page, the patient's procedures, the procedure page, Edit Procedure and New Procedure. Like a hospital wristband, it shows the same details in the same place wherever someone acts on a patient. Procedure responses now carry the patient (`ProcedurePatientResponse`), so procedure pages need no second read.
- **A read-only FHIR API** (`FhirController` at `api/fhir`): FHIR is the NHS's standard format for exchanging health records, and UK Core is its UK profile set.
  - `GET api/fhir/Patient/{id}` returns a UK Core Patient: the profile, the NHS number with the system `https://fhir.nhs.uk/Id/nhs-number` and the verification status "02, Number present but not traced", an official name, email as `telecom`, and `birthDate`.
  - `GET api/fhir/Patient?identifier=…|{number}` returns a searchset Bundle (a search's results, with `total`, absolute `fullUrl`s and `search.mode: match`).
  - Errors are OperationOutcomes (FHIR's error resource): `FhirExceptionFilter` for 400 and 404, and a custom `IAuthorizationMiddlewareResultHandler` (`FhirAuthorizationResultHandler`) for 401 and 403 on `/api/fhir` only. Staff only, `Cache-Control: no-store`, and reads and searches logged as the pages' are.
- **SNOMED CT codes on stage definitions:** an optional concept ID (SCTID) with a display term the admin types, both or neither, copied onto each procedure stage when the procedure is made. `SnomedCt` checks the format only: 6 to 18 digits, no leading zero, a Verhoeff check digit, and a partition (the two digits before the check digit) of `00` or `10`, meaning a concept rather than a description or relationship.
- **A demographics lookup** (`IPersonalDemographicsService`): `Pds:Mode` is `Off` by default, or `Offline`, a stub of four synthetic people with 999 numbers. `POST api/demographics/lookup` takes the number in the body so it never sits in a URL, records `DemographicsLookedUp` with only whether anyone was found, and the patient forms get a Look up button that fills the name and date of birth.
- **The migration** `AddedNhsNumberAndSnomedCodes`: five nullable columns, the filtered unique index, and an `UpdateData` that sets the seeded "Definition missing" definition's two new columns to null.
- **Demo data:** `reset_demo_data` gives most patients a valid 999 number.
- **Docs:** both `AGENTS.md` files, the handbook (step 9's status, ADR-23), the hazard log (H-01, H-02 and H-13), the device decision (reassessed, unchanged), the DPIA (new risk 21), the data inventory, the ROPA, the NHS context and `docs/todo.md`.

## Design choices

- **Check, then let the index decide:**
  - **Instead:** rely on the index alone, or take a lock before checking.
  - **Chosen because:** the check gives a clean 409 on the usual path, and the index guarantees uniqueness when two saves race, like an usher who stops a second person sitting in a taken seat.
  - **Worse at:** two database round trips per save, and the index's refusal is logged by EF Core with the number in it (finding 2).
- **Spaces accepted, dashes refused:**
  - **Chosen because:** people write the number `999 000 0018`; one rule on both sides keeps the client and server in step.
  - **Worse at:** a number pasted as `999-000-0018` is refused, with a clear message.
- **A partial-match NHS number filter:**
  - **Chosen because:** it matches the other list filters, and lets staff find a patient from part of a number.
  - **Worse at:** deterministic Always Encrypted only supports exact matches, so the filter would have to change when the number is encrypted (`docs/todo.md`).
- **Hand-built FHIR resources, not the Firely SDK** (ADR-23):
  - **Chosen because:** three shapes don't justify a large dependency.
  - **Worse at:** nothing checks them against the profiles, and the parts of FHIR a library gives for free (a CapabilityStatement, format negotiation, OperationOutcomes for every error) are missing (finding 6).
- **One global authorisation result handler for FHIR's 401 and 403:**
  - **Instead:** let FHIR's refusals be the API's usual empty 401 and 403.
  - **Chosen because:** FHIR clients expect an OperationOutcome, and refusals happen before MVC's filters run.
  - **Worse at:** it replaces the handler for every endpoint. It delegates to the framework's own `AuthorizationMiddlewareResultHandler` for anything outside `/api/fhir`, so behaviour there is unchanged; the integration suite's authorisation matrix passes through it.
- **Verification status always `02`, "not traced":**
  - **Chosen because:** MedApp never confirms a number with PDS, so claiming `01` would be false.
  - **Worse at:** a receiving system can't tell a number checked against the patient's documents from one typed from memory.
- **The banner in each page, through `Page`'s `banner` prop:**
  - **Instead:** a banner in the app shell, driven by the route.
  - **Chosen because:** it works in half views (two pages side by side) and on server-rendered pages without extra plumbing.
  - **Worse at:** each page must opt in, and one that doesn't is silently without it (finding 1). On pages that aren't `fill`, the banner scrolls away with the header (finding 7).
- **SNOMED CT codes checked for format only:**
  - **Chosen because:** no terminology server dependency, and MedApp does nothing with a code.
  - **Worse at:** a valid-looking code can name an inactive or wrong concept, and its term can disagree with it (H-13, documented).
- **A lookup interface with an offline stub, off by default:**
  - **Chosen because:** the form flow can be built and tested before NHS England's onboarding.
  - **Worse at:** the stub's flow becomes the template for the real one, and its edges matter once real people are behind it (finding 3).

## How it was checked

- **Read:** the whole NHS diff, the PR description, both `AGENTS.md` files, `docs/architecture.md` (step 9, ADR-23), the hazard log (H-01, H-02, H-13), the device decision, the DPIA (risks 8 and 21), the data inventory and the NHS context, and the code around the changes: `PatientRules`, `ExceptionHandling`, `ApplicationBuilderExtensions`, `PasswordChangeRequired`, the procedure repository's includes, the favourites store and the list search parsing.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors; 1 warning, the deprecated Blazor client's existing CS0414.
- **.NET tests:** unit, 1,275 passed. Integration (the real API against SQL Server in a Testcontainers container), 217 passed in 2 minutes 13 seconds; the containers were removed afterwards.
- **Web:** `npm run lint` and `npm run typecheck` clean; `npm run coverage`, 1,006 tests passed, with 99.43% statements, 93.48% branches, 99.77% functions and 99.8% lines; `npm run theme:check` up to date; `npm run build` (client, server bundle, page server) succeeded.
- **The migration:** the SQL for `AddedNhsNumberAndSnomedCodes` generated with `dotnet ef migrations script` and read: five `ADD … NULL` columns, an `UPDATE` of the seeded row whose row count isn't checked (so it can't fail if the row is gone), and the filtered index, in one transaction. Additive only, so the previous image still runs against the new schema after a rollback.
- **The algorithms, by hand:**
  - Modulus 11 in both implementations against the published rule, including remainder 10 (invalid) and 11 (check digit 0), and against every repeated-digit number (finding 4).
  - The Verhoeff tables compared entry by entry with the published ones, and run in a scratch script against real SCTIDs, among them `80146002`, `22298006`, `138875005` and `900000000000207008`, and two long-format IDs in the UK extension's namespace (partition `10`): all pass, with the partition read right.
- **The race, reproduced:** a scratch program outside the repository, against a throwaway SQL Server container, saved two patients with the same NHS number through `MedAppDbContext`; EF Core logged the number at Error level (finding 2).
- **FHIR, against UK Core:** the profile URL, the identifier system, the extension URL, the CodeSystem and code `02` all match UK Core; the Bundle has `type`, `total`, `link.self`, absolute `fullUrl`s and `search.mode`.
- **Not done:** the app wasn't run in a browser (the local development database was left alone), so the banner's layout at small heights, zoom and with a screen reader wasn't tried; no FHIR validator was run; no UI parity screenshots were taken.

## Findings

Most severe first. None is high or critical. None was fixed at the time of this review.

### 1. Edit Patient has no patient banner, though it's the page where a patient's identity changes

- **Severity:** medium. **Confidence:** high on the facts; medium on the severity.
- **What could go wrong:** two patients with the same name are open side by side in half views. A member of staff means to add an NHS number to one, chooses Edit on the other, and types the number. Edit Patient shows only the editable fields, which change as they're typed, and nothing fixed that says whose record this is. With the lookup on (finding 3), pressing Look up also replaces the name and date of birth, so after a few seconds no detail of the original patient is on the screen, and the save confirmation shows only the new values. The banner exists to stop exactly this, but the page that changes identity doesn't have it. The rule written for it says "Every page about one patient shows `PatientBanner` … and any new page that shows or changes one patient's record", and `Page`'s and the banner's own comments say the same, so a verification that trusts those docs would expect it here. The admin's export page (`/patients/{id}/export`) is also about one patient and has none, a lesser gap since it changes nothing.
- **Evidence:**
  - `MedApp.Web/src/pages/patients/EditPatientPage.tsx:95-107`: the form's `Page` has no `banner`; `:156` the editable fields; `:119` the confirmation shows only the form's values
  - `MedApp.Web/src/pages/patients/PatientExportPage.tsx:78-84`: no `banner`
  - `MedApp.Web/AGENTS.md:11`: "Every page about one patient shows `PatientBanner` … and any new page that shows or changes one patient's record"
  - `MedApp.Web/src/components/pages/Page.tsx:26`, `MedApp.Web/src/components/patients/PatientBanner.tsx:41`: "on every page about one patient"
  - `MedApp.Web/src/pages/patients/PatientFormFields.tsx:122`: the Look up button is offered on the edit form too
- **Fix:** pass `banner={<PatientBanner patient={patient} />}` on Edit Patient (the saved record, so it stays put while the fields change), and on the export page from `data.patient`. In Edit Patient's confirmation, show the old and new value for the name, date of birth and NHS number when they changed. Add a page test for each. A test that renders every route under `/patients/$id` and `/procedures/$id` and asserts a `Patient` region would stop a future page missing it.
- **What happened to it:** not fixed; recorded for a decision.

### 2. When two saves race on the same NHS number, EF Core writes the number into the error log

- **Severity:** low. **Confidence:** high (reproduced).
- **What could go wrong:** the repository turns the unique index's refusal into a 409 correctly, but EF Core has already logged the failure: `fail: Microsoft.EntityFrameworkCore.Update[10000]`, with SQL Server's message "Cannot insert duplicate key row … with unique index 'IX_Patients_NhsNumber'. The duplicate key value is (9990000018)", twice in the one entry. The API logs at Information and above, so this reaches the container's console today, and Log Analytics once step 5 exports logs (planned to keep them 90 days). The governance pack doesn't expect it: the data inventory's logs row says EF Core logs without parameter values and that error messages can include record IDs, and its list of where the NHS number is held doesn't include logs. The path is rare (two saves of one number within milliseconds), but it's the path the code was written for, and a national identifier is the detail the pack takes most care over.
- **Evidence:**
  - `MedApp.Infrastructure/Data/Repositories/PatientRepository.cs:108-119`: the catch runs after EF Core has logged
  - `MedApp.API/appsettings.json:2-7`: `Default: Information`
  - `docs/governance/data-inventory.md:56`: the logs row; `docs/governance/data-inventory.md:37-44`: where the number is held, without logs
  - The excerpt is in Detail.
- **Fix:** log EF Core's `CoreEventId.SaveChangesFailed` at Debug in `AddDatabase` (`options.ConfigureWarnings(w => w.Log((CoreEventId.SaveChangesFailed, LogLevel.Debug)))`); an unhandled save failure is still logged by the server as an unhandled exception, and the handled 409 then logs nothing with the number. Or filter `Microsoft.EntityFrameworkCore.Update` to `Critical` in `appsettings.json`. Either way, say in the data inventory which events can carry patient values.
- **What happened to it:** not fixed; recorded.

### 3. The lookup's filled details aren't tied to the number, and on Edit Patient it replaces the patient's identity

- **Severity:** low (the lookup is off by default and has only synthetic people; it would be medium with the real PDS). **Confidence:** high.
- **What could go wrong:** three cases:
  - **The number changes after a lookup:** staff look up `999 000 0018`, which fills "Ada Synthetic". They notice a typo and correct the number to `999 000 0026` (Ben's). The "Found" note disappears, because it shows only while the number matches, but Ada's name and date of birth stay, and the save records Ada under Ben's number with nothing to say so. The guard the PR added covers only a change made while the lookup is in flight.
  - **On Edit Patient:** Look up overwrites the existing patient's name and date of birth with whoever has the number. On a new patient that's the point; on an existing one it silently swaps identity, and finding 1 means nothing on the page shows what it was.
  - **The announcement:** when the number changed in flight, nothing is filled, but screen readers still hear "Found: name and date of birth filled in".
- **Evidence:**
  - `MedApp.Web/src/pages/patients/PatientFormFields.tsx:65-77`: fills only if the number is unchanged, then announces "filled in" either way
  - `:121`: the note is hidden once the number differs, while the filled fields stay
  - `:122`: offered on both the add and the edit forms
  - `docs/clinical-safety/hazard-log.md:97`: the planned control (show who was found and ask before filling) is due only "before the real PDS is connected"
- **Fix:** remember what a lookup filled; if the number then changes while those values are still in the fields, show a warning on the NHS number ("The name and date of birth were filled in from 999 000 0018") and refuse the save until the lookup is repeated or the details are confirmed. On Edit Patient, show the found details beside the current ones and fill only on an explicit "Use these details". Announce "filled in" only when it was. Add tests for each.
- **What happened to it:** not fixed; recorded. Due before the real PDS is connected, with the hazard log's own planned control.

### 4. The Modulus 11 check accepts the usual placeholder numbers

- **Severity:** low. **Confidence:** high (computed).
- **What could go wrong:** the check is implemented exactly as the NHS Data Dictionary defines it, including remainder 10 (no valid number) and 11 (check digit 0). But every number of one digit repeated ten times passes it (`0000000000` to `9999999999`: the weights 10 to 2 add up to 54, which makes the check digit equal to the repeated digit), and so do the runs `0123456789` and `9876543210`. These are what people type when a form insists on a number they don't have. One patient saved with `1111111111` then makes the next placeholder a 409 that says "Another patient already has this NHS number. Check you have the right patient.", which points staff at the wrong patient, and the FHIR API sends the placeholder to other systems as a real NHS number.
- **Evidence:**
  - `MedApp.Domain/Patients/NhsNumbers.cs:31-38` and `:46-60`; `MedApp.Web/src/utils/nhsNumber.ts:14-22`
  - `MedApp.Application/Patients/Common/NhsNumberRules.cs:12`: the 409's message
  - Neither test file tries a placeholder (`MedApp.UnitTests/Domain/Patients/NhsNumbersTests.cs`, `MedApp.Web/src/utils/nhsNumber.test.ts`)
- **Fix:** refuse ten identical digits and the two runs on both sides, with their own message ("This looks like a placeholder, not an NHS number. Leave it empty if it isn't known."), and test them. Checking the issued ranges is possible too, but would refuse some numbers from the rest of the UK, so it's better as a later option.
- **What happened to it:** not fixed; recorded.

### 5. An anonymised, restricted or disputed record comes out of the FHIR API looking like any other patient

- **Severity:** low (no system reads the API yet). **Confidence:** high.
- **What could go wrong:** anonymising keeps the row with placeholders, and the mapper sends them as real data: an `official` name "Anonymised Patient", `birthDate` the 1 January the anonymiser invented, and the placeholder email as a `telecom`. A system reading a patient by ID, as the API is meant for, would take 1 January as a date of birth. Restricted and disputed records carry nothing either: in the app a banner says the patient disputes the record's accuracy; through FHIR the same data goes out unmarked. The mapper's comment says "nothing is made up", which holds for the database, not for what a receiving system can tell.
- **Evidence:**
  - `MedApp.Application/Fhir/Common/FhirPatientMapper.cs:9`, `:70-82`
  - `MedApp.Application/Patients/Common/PatientAnonymiser.cs:21-23`, `:41`
  - `MedApp.UnitTests/Application/Fhir/FhirPatientMapperTests.cs`: no anonymised, restricted or disputed case
- **Fix:** for an anonymised patient, use FHIR's own means: `name.use` `anonymous` (or no name), `birthDate` as the year alone (`"1980"` is a valid FHIR date), no `telecom`, and `active: false`; or answer 404. Mark restricted and disputed records with a `meta.tag` from a small MedApp code system, documented. Test all three. Do it before any system client (OAuth) is added.
- **What happened to it:** not fixed; recorded.

### 6. The FHIR API misses some of FHIR's basics

- **Severity:** low (each is small, and the CapabilityStatement is already on the to-do list). **Confidence:** high.
- **What could go wrong:**
  - **No `GET api/fhir/metadata`:** FHIR servers must answer it with a CapabilityStatement, and generic clients call it first. Here it, and any other unknown `/api/fhir` path, gets the API's empty 404, not an OperationOutcome, though `AGENTS.md` says FHIR errors are "OperationOutcomes, never ProblemDetails".
  - **No format negotiation:** `Accept: application/fhir+xml` and `_format=xml` are ignored and JSON comes back with a 200, where FHIR says a server should answer 406 for a format it can't give.
  - **An identifier without a system is always read as an NHS number:** `identifier=MRN123` is a 400 ("NHS number must be 10 digits"), where FHIR says a value without a system matches in any system, so the answer is an empty Bundle.
  - **The log's label:** a search in another system is logged as a search by `nhsNumber`, though no NHS number was searched.
- **Evidence:**
  - `MedApp.API/Common/Extensions/ApplicationBuilderExtensions.cs:54`: the empty 404 for unmatched `/api` paths
  - `MedApp.API/Fhir/FhirJson.cs:27`: always JSON
  - `MedApp.Application/Fhir/Common/FhirIdentifierSearch.cs:22`; `MedApp.Application/Fhir/Queries/SearchFhirPatients/SearchFhirPatientsValidator.cs:26-27`
  - `MedApp.Application/Fhir/Queries/SearchFhirPatients/SearchFhirPatientsHandler.cs:30`
  - `AGENTS.md:189`; `docs/todo.md:50`
- **Fix:** a minimal CapabilityStatement at `GET api/fhir/metadata` (Patient: `read`, `search-type` on `identifier`); map unmatched `/api/fhir/{**path}` to a 404 `not-supported` OperationOutcome before the generic `/api` 404; return 406 when neither `Accept` nor `_format` allows JSON; validate a system-less value as an NHS number only when it's ten digits, otherwise match nothing; log `filters: ["identifier"]` for another system. Run the HL7 validator in CI, as `docs/todo.md:48` plans.
- **What happened to it:** not fixed; recorded.

### 7. The banner falls short of the NHS banner guidance and of "always in view" in small ways

- **Severity:** low. **Confidence:** medium (read from the code; not tried with a screen reader or at small heights).
- **What could go wrong:**
  - **Two "Patient" regions:** in half views with two patients open, both banners are regions named "Patient", so someone moving between landmarks with a screen reader hears the same name twice, at the moment telling them apart matters most.
  - **Capitals in the text:** the surname is upper-cased in the text itself, and some screen readers spell short all-capital words letter by letter. Upper-casing it with CSS keeps the look and reads the name normally.
  - **Infants:** every child under one is "under 1 year". The NHS banner guidance gives young children's ages in months, weeks or days, which matters in paediatric care.
  - **Not always in view:** Edit Procedure isn't a `fill` page, so when its form is taller than the window the whole page scrolls and the banner goes with it, though `Page`'s comment and the hazard log say it's always visible.
  - **No sex or gender:** the guidance shows it, but MedApp doesn't hold it yet (`docs/todo.md`, step 9).
- **Evidence:**
  - `MedApp.Web/src/components/patients/PatientBanner.tsx:57` (the region's fixed name), `:27` (`toLocaleUpperCase`), `:33-38` (`under 1 year`)
  - `MedApp.Web/src/pages/procedures/EditProcedurePage.tsx:93-97`: no `fill`
  - `MedApp.Web/src/components/pages/Page.tsx:27`: "It's always in view"
- **Fix:** name the region by the patient (`aria-labelledby` the name); upper-case the surname with CSS, keeping its own case in the text; give ages under two in months and weeks, under four weeks in days; make Edit Procedure a `fill` page, or make the banner sticky. List the remaining gaps against the guidance for the CSO's verification.
- **What happened to it:** not fixed; recorded.

### 8. Docs that don't match the code

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the governance pack is what an NHS customer's IG team reads, so a wrong line there is believed:
  - `docs/governance/data-inventory.md:57` says the NHS number can appear in a traced URL from "a demographics lookup"; the lookup moved the number into a POST body in `d6da454`.
  - **Saved searches hold NHS numbers:** a favourite saved on the patient list keeps its filters, the NHS number among them, in the browser (`medapp.favourites`) and in the account's settings on the server (`UserSettings.Json`). The inventory's list of where the number is held leaves this out, and those rows still say saved search terms "can be patient names". Anonymisation doesn't clear them (a to-do for names already).
  - The logs row (finding 2), and "every page about one patient" and "always in view" (findings 1 and 7).
- **Evidence:** `docs/governance/data-inventory.md:31`, `:37-44`, `:49`, `:56-57`; `MedApp.Web/src/app/favourites.ts:20-21`; `docs/todo.md:38`.
- **Fix:** correct line 57; add saved searches to the NHS number's list and to the `UserSettings` and `localStorage` rows; note in the DPIA's risk 21 that saved searches can hold the number until they're removed on anonymisation.
- **What happened to it:** not fixed; recorded.

## Remaining risks

- **The banner isn't verified:** H-01's and H-02's "at first live use" ratings depend on the CSO verifying it, and on controls not built yet (the date of birth in lists, the patient's name in the procedure page's title and in the save and finish confirmations).
- **Numbers aren't traced:** a valid number on the wrong patient still looks precise and correct, on screen and to any system reading the FHIR API. Tracing needs NHS England's onboarding.
- **The FHIR API is unvalidated and session-only:** no profile validation, no CapabilityStatement, and no system clients (OAuth); a script using a staff session is the only caller.
- **The NHS number in URLs:** the patient list's filter (kept by a product decision) and the FHIR search's query string, which ingress access logs can record. `POST Patient/_search` is planned.
- **No field-level encryption** for the number yet, and the partial-match filter will have to become exact for deterministic Always Encrypted.
- **Unique across the deployment,** until step 8 makes it unique within an organisation.
- **SNOMED CT:** no terminology server, and changes to stage definitions, codes included, aren't audited (H-13).
- **No limit on lookups:** with the real PDS, a staff session could look up numbers in bulk; each is logged, but nothing slows or alerts on it.
- **`Pds:Mode` with an unknown value** stops the API at start-up, because the setting fails to bind. That fails safe, but isn't documented.
- **Bringing the branch up to date:** after #68 merges, `main`'s #66 has to come in too. It also changed `ProceduresListPage.tsx` and Infrastructure's `ServiceExtensions.cs`, `schema.d.ts` has to be regenerated, and this migration has to stay last.

## Say it like this

"PR #69 gives MedApp the NHS number, checked with its check digit and unique, and puts it in a patient banner on the patient and procedure pages, the control for the wrong-patient hazards. It adds a read-only FHIR API that returns patients in the NHS's UK Core format, SNOMED CT codes on stage definitions, and a PDS lookup that is off by default and only knows four made-up people. The checks are exactly right, the database change only adds, nothing that worked before breaks, and every test suite passes. Nothing blocks it, but one thing should be fixed before the banner is signed off: Edit Patient, where a patient's identity is changed, has no banner. Smaller fixes: a rare race writes the NHS number into the error log; the lookup's filled-in details stay when the number changes, and on Edit Patient it replaces the patient's name; placeholder numbers like 1111111111 pass the check; anonymised records look real through FHIR; and a few FHIR basics, banner details and docs lines are missing."

## Detail

### The race, as logged

From the scratch program (two saves of one number against a throwaway SQL Server 2022 container, the logger at Information as in `appsettings.json`), trimmed:

```
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (3ms) [Parameters=[@p0='?' (DbType = Guid), … @p10='?' (Size = 10) (DbType = AnsiString), …]]
      INSERT INTO [Patients] (…, [NhsNumber], …) VALUES (…);
fail: Microsoft.EntityFrameworkCore.Update[10000]
      An exception occurred in the database while saving changes for context type 'MedApp.Infrastructure.Data.MedAppDbContext'.
      Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. …
       ---> Microsoft.Data.SqlClient.SqlException (0x80131904): Cannot insert duplicate key row in object 'dbo.Patients'
            with unique index 'IX_Patients_NhsNumber'. The duplicate key value is (9990000018).
      Error Number:2601,State:1,Class:14
```

The command log keeps its parameters hidden (`'?'`), as the data inventory says; the save-failure log carries the value in the exception's message. The suggested fix, in `AddDatabase`:

```csharp
services.AddDbContext<MedAppDbContext>(options => options
    .UseSqlServer(
        configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null))
    // A failed save's message can hold a value (a duplicate NHS number). Unhandled failures are still logged as
    // unhandled exceptions; handled ones, like the NHS number's 409, then log nothing with patient data in it.
    .ConfigureWarnings(w => w.Log((CoreEventId.SaveChangesFailed, LogLevel.Debug))));
```

### Modulus 11 and the placeholders

The weights for the first nine digits are 10, 9, …, 2, which add up to 54. For a number made of one digit `d` repeated, the sum is `54 × d`; since 54 leaves 10 when divided by 11, the remainder is `11 − d` (or 0 for `d = 0`), and the check digit `11 − remainder` comes out as `d` itself (11 becoming 0). So all ten repeated-digit numbers pass. Checked in a scratch script against the same rule as `isValidNhsNumber`:

| Number | Passes |
|---|---|
| `0000000000` … `9999999999` | all ten |
| `0123456789` | yes |
| `9876543210` | yes |
| `1234567890` | no (check digit 10) |

### Verhoeff and the partition

`SnomedCt`'s multiplication table (the dihedral group D5) and permutation table match the published ones entry for entry, and `HasValidCheckDigit` walks the digits from the right with `p[i % 8]`, as the algorithm does. Run on real SCTIDs, every one passes and the partition reads right: `80146002` (00), `71388002` (00), `387713003` (00), `22298006` (00), `73211009` (00), `404684003` (00), `138875005` (00) and `900000000000207008` (00), and the long-format `999000011000000103` and `1322781000000102` (10, in the UK extension's namespace `1000000`). The term is required whenever a code is given, and the code whenever a term is (`SnomedRules`).

### The authorisation handler

`FhirAuthorizationResultHandler` is registered before `AddAuthorization`, whose own registration is a `TryAdd`, so it's the only `IAuthorizationMiddlewareResultHandler`. For any path outside `/api/fhir`, and for any request that passes, it calls a `new AuthorizationMiddlewareResultHandler()`, the framework's default, so challenges and forbids still go through the session scheme as before. The session handler has no custom challenge, so skipping it on FHIR's paths loses nothing. `/api/fhirish` doesn't count as FHIR (`StartsWithSegments`). The forced password change's 403 comes earlier, from `PasswordChangeRequired`, and stays a ProblemDetails, as documented.

### The migration's SQL

```sql
ALTER TABLE [Patients] ADD [NhsNumber] varchar(10) NULL;
ALTER TABLE [PatientProcedureStages] ADD [SnomedCode] varchar(18) NULL;
ALTER TABLE [PatientProcedureStages] ADD [SnomedTerm] nvarchar(255) NULL;
ALTER TABLE [PatientProcedureStageDefinitions] ADD [SnomedCode] varchar(18) NULL;
ALTER TABLE [PatientProcedureStageDefinitions] ADD [SnomedTerm] nvarchar(255) NULL;
UPDATE [PatientProcedureStageDefinitions] SET [SnomedCode] = NULL, [SnomedTerm] = NULL
WHERE [Id] = '11111111-1111-1111-1111-111111111111';
SELECT @@ROWCOUNT;
CREATE UNIQUE INDEX [IX_Patients_NhsNumber] ON [Patients] ([NhsNumber]) WHERE [NhsNumber] IS NOT NULL;
```

The `UPDATE` is EF Core recording that the seed now has two more (null) columns: it changes nothing, and touches no other column of the seeded row, so an admin's edits to it survive. Adding nullable columns is a metadata change in SQL Server, and the index is built over a column that's all null at that point.

### An anonymised patient through FHIR, today and as suggested

Today (trimmed of `id` and `meta`):

```json
{ "resourceType": "Patient", "name": [{ "use": "official", "family": "Patient", "given": ["Anonymised"] }],
  "telecom": [{ "system": "email", "value": "anonymised@anonymised.invalid" }], "birthDate": "1980-01-01" }
```

Suggested:

```json
{ "resourceType": "Patient", "active": false, "name": [{ "use": "anonymous", "text": "Anonymised" }], "birthDate": "1980" }
```
