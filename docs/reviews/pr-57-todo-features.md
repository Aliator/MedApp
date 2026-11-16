# Review: PR #57, new features and a long-term goal in the to-do list

**PR:** [#57](https://github.com/Aliator/MedApp/pull/57), branch `feature/todo-features`, reviewed at `78abce1` on top of `main` at `88dc1fa` (the merge of #56).

**Reviewed:** 16 November 2026, independently of the PR's own notes. Every claim below was checked against the files, git or the GitHub API.

**Highest severity:** medium. No finding is high or critical.

## Summary

`docs/todo.md` gains seven larger features (admin insights, appointments, a patient timeline, notification settings for each person, emails with a self-service password reset, file attachments and a customisable home screen) and a "Long term" section about turning MedApp into a platform that companies shape into their own app. The "More notifications" item goes; its notification kinds now sit inside the notification settings item.

## What changed, and why

Only `docs/todo.md` changes: 28 lines added and 1 removed, in four commits.

- **Seven new items at the top of Features** (`docs/todo.md:17-32`):
  - **Admin insights:** tiles and charts for open, overdue and in-progress procedures, overdue work by assignee and priority, and the time spent in each stage.
  - **Appointments and booking:** a day and week calendar for each member of staff, showing appointments and due dates, tied into the procedure workflow.
  - **A patient timeline tab:** one feed, grouped by day, of everything that happened across a patient's procedures, built from the audit log (the record of every change to a procedure).
  - **Notification settings for each person:** admins choose which events notify each member of staff, which needs new notification kinds.
  - **Emails, and resetting your own password:** notification and welcome emails, and a password reset by emailed link.
  - **File attachments:** files on a patient, procedure or stage, dragged in, with a preview and a download, kept in Azure Blob Storage when deployed.
  - **A customisable home screen:** widgets each person picks, moves and resizes, with the layout saved like their other settings.
- **"More notifications" removed** (the last Features item on `main`). Its kinds (overdue, blocked, stage unlocked) come back as examples in the notification settings item, and its "optionally an email for each" in the emails item.
- **A new "Long term" section** (`docs/todo.md:58-68`): MedApp as a generic engine whose records, workflows and pages each company customises, built and hosted by MedApp, with today's medical app as one ready-made pack and multi-tenancy (many companies' data kept apart in one deployment) as the first step.
- **Why:** to record product ideas before they're lost. The PR description gives no other reason.

## Design choices

- **Big features in the to-do list, not in the roadmap:**
  - **Instead:** add them to, or link them from, the roadmap in `docs/architecture.md`, which already plans several of them (steps 4 and 9), and multi-tenancy (step 8).
  - **Chosen because:** the to-do list is short and quick to add to.
  - **Worse at:** the same work is now described in two places, with different scope and no shared order (finding 1).
- **New notification kinds folded into per-person settings:**
  - **Instead:** keep "More notifications" as its own small item, with settings as a later one.
  - **Chosen because:** one item tells the whole notification story.
  - **Worse at:** the small, shippable part (new kinds) now waits on an admin settings screen, and the roadmap's pointer to those kinds lands on examples (finding 7).
- **Admins choose each person's notifications:**
  - **Instead:** each person chooses their own, with defaults an admin sets (as the home screen item does for layouts).
  - **Chosen because:** admins can make sure the right people hear about overdue or blocked work.
  - **Worse at:** nobody can quieten something noisy for themselves, and admins set up every account by hand.
- **The timeline built from the audit log:**
  - **Instead:** build it from the procedures and stages themselves, which hold start and finish times (`PatientProcedureStage.cs:27-32`).
  - **Chosen because:** the audit log already has every event with who and when, and keeps the history of deleted procedures.
  - **Worse at:** it brings along what the audit log holds, which only admins may read today (finding 3).
- **The platform goal as a to-do item:**
  - **Instead:** write it as a direction in `docs/architecture.md`, beside the roadmap's multi-tenancy step it depends on.
  - **Chosen because:** it's long term, not yet a plan.
  - **Worse at:** it doesn't say how it fits the roadmap's clinical direction or its step 8 (finding 5).

## How it was checked

- **Read:** the diff, the PR description, the whole of `docs/todo.md`, and in `docs/architecture.md` the roadmap (steps 2, 4, 6, 7, 8 and 9), section 3.3 (scope), the personal data table and every line that cites `docs/todo.md`. Also `AGENTS.md` (Auth, Procedures, Notifications and the deploy notes) and `docs/procedures-ui-plan.md`.
- **Each item's claims against the code:**
  - **"Only 'assigned to a procedure' exists today":** true. `NotificationKind` has one value (`MedApp.Domain/Notifications/NotificationKind.cs:7`), sent from one handler (`AssignPatientProcedureHandler.cs:42`).
  - **"The audit log already records when each stage starts and finishes":** true (`MedApp.Application/Procedures/Common/ProcedureChange.cs:63` and `:91`).
  - **Priorities, the Blocked status and due dates** exist (`PatientProcedure.cs:16-20`, `PatientProcedureStatus.cs:15`). Audit entries carry the patient's ID (`PatientProcedureAuditEntry.cs:16`), so a timeline per patient can be built from them.
  - **"Saved with their account, like their other settings":** true. `UserSettings` is one JSON object per account (`MedApp.Domain/Authentication/UserSettings.cs:3-6`). Home is a welcome title today (`MedApp.Web/src/pages/home/HomePage.tsx:4-7`), and My Tasks exists (`MedApp.Web/src/pages/tasks/MyTasksPage.tsx`).
  - **"ASP.NET Identity's password reset tokens fit":** partly. The token providers are registered (`MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs:77`), and admin resets use a token within one request (`IdentityUserService.cs:65`). A link that comes back later is another matter (finding 2).
  - **Staff email addresses:** none. `CreateUserRequest` takes only a username (`MedApp.Contracts/Authentication/Requests/CreateUserRequest.cs:4-5`), and no Application or Infrastructure code sets one.
  - **Scheduled work:** none. The API's only hosted service is the page server (`MedApp.API/Common/Extensions/ServiceExtensions.cs:30`).
  - **Security headers:** the API sends no Content Security Policy or `nosniff` header today. The roadmap's step 2 lists both (`docs/architecture.md:1545-1547`).
- **What depended on the dropped item:** `git grep` for "More notifications" and for `docs/todo.md`. Only `docs/architecture.md:2045` relies on its content.
- **Personal or sensitive details:** none in the diff: no names, contact details, credentials, hostnames or patient data. "Employee" and "Animal" are generic examples.
- **Merging with the other open PR:** `git merge-tree --write-tree` of this branch with #58's (`e6f8699`) reports a conflict in `docs/todo.md` (finding 10).
- **CI** (the checks GitHub runs on every PR): run [43825584963](https://github.com/Aliator/MedApp/actions/runs/43825584963) on `78abce1` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`).
- **Not done:** no local build or test run. The change is docs-only, and CI ran both.

## Findings

Most severe first: four medium and six low. None is high or critical. All are open: this review changes nothing in `docs/todo.md`, and each fix is for the PR, before it merges, or for the to-do list later.

### 1. The new features repeat the roadmap, without its prerequisites or its order

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the roadmap in `docs/architecture.md` already plans three of the seven (admin insights, the emailed reset and attachments) and parts of two more (the new notification kinds, by email, and scheduling, which it names), in more detail and in a set order: after multi-tenancy (step 8) and the clinical safety work (step 6), so that each is built once, for many organisations, with its hazards assessed. The to-do list says it holds work "outside the phased plans", and the new items don't mention the roadmap. File attachments are picked from the to-do list and built to its shorter description: no malware scanning, downloads not audited, nothing per organisation. Step 8 then adds an organisation to every table, and step 9 adds scanning and audited downloads: the same feature built twice. And when one list's item is done, the other still shows it as open. It's two shopping lists on two fridges: both get shopped from, and neither knows what the other bought.
- **Evidence:**
  - `docs/todo.md:3`: "outside the phased plans"
  - admin insights, `docs/todo.md:17-21`, against reporting, `docs/architecture.md:2043`
  - attachments, `docs/todo.md:31`, against documents, `docs/architecture.md:2040-2042`
  - the self-service reset, `docs/todo.md:30`, against `docs/architecture.md:1725`
  - notification kinds and emails, `docs/todo.md:29-30`, against `docs/architecture.md:2044-2045`
  - appointments, `docs/todo.md:22`, against scheduling, named among what comparable products offer, `docs/architecture.md:2002`
  - multi-tenancy, `docs/todo.md:68`, against step 8, `docs/architecture.md:1945-1992`
  - the order: `docs/architecture.md:2014` ("after tenancy and the clinical safety system, each feature is built once") and `:1992` ("Adding tenancy after step 9's features costs more")
- **Fix:** give each overlapping item a pointer and the roadmap's must-haves, for example "(roadmap step 9, item 4, in `docs/architecture.md`: malware scanning, audited downloads)". Or keep here only what the roadmap lacks (appointments tied to stages, the timeline, the home screen, per-person settings) and add the rest to the roadmap. Either way, say whether these come before or after step 8.
- **What happened to it:** partly fixed in the PR: the insights, email and attachments items now point at their roadmap step. The order relative to multi-tenancy was left to the roadmap.

### 2. The email item leaves out what reset links and safe emails need

- **Severity:** medium. **Confidence:** high on the facts.
- **What could go wrong:**
  - **No address to send to.** Staff accounts have no email address: an account is made from a username alone. A welcome email or a reset link has nowhere to go, and the stand-in "Forgot password?" dialog already asks for an email address that no account has.
  - **Links that fail half the time.** Identity's reset tokens are sealed with ASP.NET Core's data protection keys (the keys the framework uses to sign and encrypt values it hands out). The API doesn't share or keep them, so each copy makes its own, in memory, and kind and Azure both run two copies. A link made on one copy and opened on the other is refused as invalid, and after a restart every outstanding link is. Development runs one copy, so it works there and fails only once deployed. It's a key cut by one locksmith that the other locksmith's lock won't take.
  - **Clinical text in an inbox.** A notification's title is the procedure's title, free text that in practice holds clinical information. Emailing notifications as they are copies it out of the app into mailboxes. The roadmap says "no patient identifiers in notification text or emails".
  - **The welcome email and the first password.** `AGENTS.md` says the generated first password is shown to the admin once, to pass on securely. A welcome email that carries it isn't secure; a single-use "set your password" link is.
- **Evidence:**
  - `MedApp.Contracts/Authentication/Requests/CreateUserRequest.cs:4-5`; `MedApp.Infrastructure/Common/Identity/ApplicationUser.cs:5-8`
  - `MedApp.Web/src/pages/login/ForgotPasswordDialog.tsx:7` and `:37-38`: the stand-in asks for an email address
  - `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs:77` (`AddDefaultTokenProviders`), with no `AddDataProtection` in the API or Infrastructure; `deploy/kind/values.yaml:9` and `deploy/azure/values.yaml:6` (two copies); `docs/reviews/kubernetes.md:118`; `docs/architecture.md:1282` and `:1560`
  - `MedApp.Application/Procedures/PatientProcedures/Commands/AssignPatientProcedure/AssignPatientProcedureHandler.cs:47` (`Title = procedure.Title`); `docs/architecture.md:1105`, `:1926` and `:2045`
  - `AGENTS.md:154`
- **Fix:** add to the item: "Needs an email address on each staff account (and on the Users page's create form), data protection keys shared between copies (roadmap step 2), and emails that only say something happened, with a link: never a procedure title or patient details. The welcome email sends a single-use set-your-password link, not the password."
- **What happened to it:** fixed in the PR: the email item now notes the missing email addresses, the shared data protection keys, no clinical free text in emails, and no password in a welcome email.

### 3. A timeline built from the audit log would show admin-only history to all staff

- **Severity:** medium. **Confidence:** medium (it depends on how it's built).
- **What could go wrong:** every member of staff can open a patient's page, but only admins can read the audit log. A timeline on the patient page, built from the audit log, either stays admin-only or opens the log to everyone, and the item doesn't say which. The log keeps the old and new text of every notes change, and the history of deleted procedures. A stage note written on the wrong patient's procedure is corrected, and the timeline still shows the original text to every member of staff who opens that patient. A procedure deleted because it was made for the wrong patient shows up there too. It's like pinning what went into the shredder to the noticeboard.
- **Evidence:**
  - `docs/todo.md:23-28`: "On the patient page", notes, "Built from the audit log"
  - `AGENTS.md:150` (patients: staff; the procedure audit log: admin only); `MedApp.API/Controllers/ProcedureAuditController.cs:20`
  - `MedApp.Application/Procedures/Common/ProcedureChange.cs:79` (the notes, from and to); `MedApp.Domain/Procedures/PatientProcedures/PatientProcedureAuditEntry.cs:4-5` and `:16`; `docs/architecture.md:1105`
- **Fix:** add the access rule to the item, for example: "Staff see what happened, who did it and when, without old notes text or deleted procedures. Admins, and the roadmap's `Auditor` role (`docs/architecture.md:1736`), see everything." Opening the timeline is also a read of patient data, which the roadmap's step 7 audits.
- **What happened to it:** fixed in the PR: the timeline item says staff see only what they can see today, and the audit log stays admin-only. The timeline being built follows that rule.

### 4. File attachments don't say how uploaded files are served safely

- **Severity:** medium. **Confidence:** medium.
- **What could go wrong:** the item asks for a preview, and limits only types and sizes. MedApp serves its pages and its API from one origin (one web address) with one session cookie, and sends no Content Security Policy (the header that tells a browser which scripts may run) or `nosniff` (which stops a browser guessing a file's type) today. An uploaded SVG image or HTML "letter" with a script in it, previewed from MedApp's own origin, runs as whoever opens it. If that's an admin, the script can call `/api/auth/users` and create an account, or page through every patient. Checking the file name's extension alone doesn't stop it, because without `nosniff` browsers go by the content. Infected scans are another way in: the roadmap's version scans for malware and audits downloads, and this one audits only adding and removing files.
- **Evidence:**
  - `docs/todo.md:31`
  - `AGENTS.md:149` (one origin)
  - no Content Security Policy or `nosniff` header in `MedApp.API`; `docs/architecture.md:1545-1547` lists both as still to do
  - `docs/architecture.md:2042` (malware scanning and audited downloads)
- **Fix:** add: "Files checked by content against an allow-list and scanned for malware; downloads sent with `Content-Disposition: attachment` and `nosniff`; previews only for images and PDFs, or from a separate origin; every download audited (roadmap step 9, item 4)."
- **What happened to it:** fixed in the PR: the attachments item now asks for safe serving (download by default, `nosniff`, no inline SVG or HTML), malware scanning and audited downloads.

### 5. The long-term goal mixes two hosting models, and doesn't meet the roadmap

- **Severity:** low. **Confidence:** medium.
- **What could go wrong:**
  - **Two models in one item.** "The build and the hosting ... for each company" reads as one deployment per company; "multi-tenancy, so each company's data stays separate inside one deployment" is the opposite. With a deployment each, data is apart already, and multi-tenancy isn't the first step. With one shared deployment, renamed records, custom fields and custom pages must be settings read while the app runs, not a separate build. These are different designs, with different first steps.
  - **The roadmap goes the other way.** Its step 8 already plans multi-tenancy, with a dedicated deployment as an option, for fixed tables; per-company fields and renamed records would change that design. Its steps 6 and 9 take MedApp further into clinical specifics: the NHS number on the patient, FHIR UK Core, SNOMED CT, and an intended purpose (what the software is for, which decides whether it's a medical device) written for one product. A generic engine in which Patient can become Animal pulls the core the other way, so step 9's patient identity work would put NHS-only fields into the core that the platform wants generic.
- **Evidence:** `docs/todo.md:66` and `:68`; `docs/architecture.md:1945-1992` (step 8) and `:1974` (the dedicated option); `:1817` and `:1856` (step 6, the intended purpose); `:2017` (step 9, patient identity)
- **Fix:** pick one model: one shared deployment with settings per company (keep multi-tenancy first, drop "the build"), or a deployment per company (say so, and drop "inside one deployment"). Then add a line such as "Builds on roadmap step 8; the medical pack keeps the clinical specifics of steps 6 and 9." Or move the goal into `docs/architecture.md`, beside the roadmap.
- **What happened to it:** fixed in the PR: the long-term item now says to decide between a deployment for each company and multi-tenancy (step 8).

### 6. The to-do list's own description no longer fits it

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** line 3 says the list holds "Smaller improvements to pick up later", deleted once merged. It now holds features of months each, and a goal that never merges. Features mixes them with follow-up fixes (the forced password change, the `DefaultPassword1` check, a `medapp-dev` bug, a duplicate label), and the quick fixes now sit under seven large features. Two lines of `docs/architecture.md` describe the list as it was: "Smaller open items", and a summary of its features list that names only what #53 left (already behind since #55).
- **Evidence:** `docs/todo.md:3`, `:15-36` and `:58`; `docs/architecture.md:1387` and `:2473`
- **Fix:** reword line 3 (for example "Open work outside the phased plans: fixes, features and longer-term goals"); split Features into "Fixes and follow-ups" and "New features", fixes first; give "Long term" a line saying its items stay until they're dropped. Then update `docs/architecture.md:1387` and `:2473` to match.
- **What happened to it:** partly fixed in the PR: the list's opening line no longer says "smaller". The handbook's description of the list is left as it is.

### 7. Dropping "More notifications" loses detail the roadmap points to

- **Severity:** low. **Confidence:** high.
- **What could go wrong:**
  - **A pointer that lands on examples.** The roadmap sends emails for "the notification kinds in `docs/todo.md` (overdue, blocked, stage unlocked)". They're now examples in an admin settings item, and "one of yours" (who is told) is gone.
  - **The overdue check isn't mentioned.** The item names two pieces of work, new kinds and a per-person setting. "Overdue" needs a third: nothing happens when a due date passes, and the API runs no scheduled work. With two copies, the check must run once, not once per copy, or everyone is told twice.
  - **Where the setting lives.** It must be on the server, where only admins can change it. In the user's own settings (one JSON object that each person saves whole), anyone could undo an admin's choice for themselves.
- **Evidence:** `docs/architecture.md:2045`; `docs/todo.md:29`; the removed line (`main`'s `docs/todo.md:21`); `MedApp.API/Common/Extensions/ServiceExtensions.cs:30` (the only hosted service); `MedApp.Domain/Authentication/UserSettings.cs:3-6` and `:13-17`
- **Fix:** in the item, say who is told for each event (assignees, and perhaps admins for blocked work); add "a scheduled check for overdue work that runs once across copies (a lease, or a Kubernetes CronJob)" and "kept apart from each person's own settings". Optionally keep the new kinds as their own, smaller item.
- **What happened to it:** partly fixed in the PR: the notification settings item now notes the once-only scheduled job and keeps admins' choices apart from personal settings. The handbook's pointer is left.

### 8. Two features rely on dragging alone

- **Severity:** low. **Confidence:** high on the wording, medium on the outcome.
- **What could go wrong:** files are attached "by dragging them in", and widgets are moved and resized. WCAG 2.2 AA, MedApp's aim, needs another way for each: success criterion 2.5.7 (Dragging Movements) asks for a single click or tap instead, and 2.1.1 for keyboard access to everything. The axe audit can't tell whether a drag has an alternative, so a drag-only build would pass it, and still shut out anyone using a keyboard, a switch or a screen reader.
- **Evidence:** `docs/todo.md:31` and `:32`; `docs/todo.md:7`; `MedApp.Web/AGENTS.md:35`
- **Fix:** add "with a file picker too" and "with buttons and the keyboard to move and resize".
- **What happened to it:** fixed in the PR: the attachments and home screen items ask for a click or keyboard alternative to dragging.

### 9. Email and file storage are new outside connections the items don't plan for

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the chart lets the API connect out only to DNS, the database and the backplane, and every container's filesystem is read-only, so files can't go on local disk. The first deploy with email or attachments fails on its first send or upload, unless the chart, kind and the AppHost (with a local stand-in for each, such as Azurite or a mail catcher) change with it. An email provider and Blob Storage also join the list of outside services that handle patient data (roadmap step 3).
- **Evidence:** `deploy/helm/medapp/templates/networkpolicy.yaml:7-8` and `:27-48`; `deploy/helm/medapp/templates/_helpers.tpl:72`; `AGENTS.md:35`; `docs/architecture.md:1725`
- **Fix:** add to each item: "a chart change (the connection out, and its secret), and a local stand-in in the AppHost and on kind".
- **What happened to it:** left for the builds: development uses local stand-ins (Mailpit for email, Azurite for files), and the chart's network policy gets the new connections when those features land.

### 10. The PR conflicts with #58, and its description is behind

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** #58 deletes the duplicate-label item, and this PR deletes the line right after it. Git can't merge the two on its own, so whichever merges second stops on a conflict, and a careless resolution brings a deleted item back. Separately, the PR description lists six features (not the home screen) and doesn't mention dropping "More notifications", so the merged PR describes less than it changed.
- **Evidence:** `git merge-tree --write-tree 78abce1 e6f8699` reports "CONFLICT (content): Merge conflict in docs/todo.md"; `main`'s `docs/todo.md:20-21`; the PR description.
- **Fix:** after the first of the two merges, merge `main` into the other and keep both deletions. Update the description: seven features, and "More notifications" folded into the notification settings item.
- **What happened to it:** handled at merge: #58 merges first, then this branch is brought up to date. The PR description is updated.

## Remaining risks

- **Two lists of the same work** until finding 1 is resolved: the roadmap and the to-do list will drift apart.
- **A list, not a plan:** no item has an effort, an order or a dependency, unlike the roadmap's steps. The big ones (appointments, the timeline, the home screen) each need a plan of their own, as procedures had (`docs/procedures-ui-plan.md`).
- **Other work edits the same file:** #58 now, and the branch that finishes the forced password change (`docs/todo.md:33`) will delete that line too.
- **Checked against today's code only:** a docs change has nothing for CI to test, and the items' claims are true of the code as it stands at `78abce1`.

## Say it like this

"This PR adds seven larger features and a long-term platform goal to MedApp's to-do list, and folds the old 'more notifications' item into per-person notification settings. The ideas fit the app, but several are already in the architecture roadmap, which plans them after multi-tenancy and with more safeguards, so the items should point there. Before anything is built from them, the email, timeline and attachment items need their missing prerequisites written in: staff email addresses and shared keys for reset links, who may see audit history, and serving uploaded files safely."

## Detail

- **Each new item against today's code:**

  | Item | What it relies on | Today |
  |---|---|---|
  | Admin insights | Statuses, priorities, due dates, stage start and finish times | All there. Two gaps: a procedure can have several assignees or none (`PatientProcedure.cs:38`), so "by assignee" counts it under each and needs an "unassigned" group; and "completed this week" needs when it reached Completed, which only the audit log's status changes record (`PatientProcedure` has no completed-at field). |
  | Appointments | Due dates, a calendar per member of staff | Procedures have one due date each, whole days in UK time (`AGENTS.md`, Procedures); stages have none. No scheduling exists (`docs/architecture.md:213`). |
  | Patient timeline | The audit log, by patient | Entries carry the patient's ID and outlive deletes, but the log is admin-only (finding 3). Blocks and unblocks are status changes, not actions of their own (`PatientProcedureAuditAction.cs:4-16`). |
  | Notification settings | New kinds, a per-person setting | One kind today; no scheduled work for overdue (finding 7). |
  | Emails and reset | Staff addresses, an email service, reset tokens | No staff addresses; keys not shared between copies; the stand-in dialog sends nothing (finding 2). |
  | File attachments | Blob Storage, type and size limits | No storage, no security headers, no outside connection allowed (findings 4 and 9). |
  | Home screen | Per-account settings, the other features as widgets | Settings exist; the calendar and insights widgets depend on two items that don't exist yet. |

- **Suggested wording for findings 2 to 4**, as sentences to add to each item:

  ```markdown
  - **Emails, and resetting your own password.** … It needs an email address on each staff account, an email
    service, data protection keys shared between the API's copies (roadmap step 2), and single-use, expiring links
    (roadmap step 4).
    Emails say only that something happened, with a link: never a procedure's title or a patient's details. A new
    account's welcome email carries a set-your-password link, not the password.
  - **A patient timeline tab.** … Built from the audit log. Staff see what happened, who and when; old notes text and
    deleted procedures only for admins.
  - **File attachments.** … Files are checked by content against an allow-list and scanned for malware; downloads are
    sent as attachments with `nosniff`; previews only for images and PDFs; every download is audited (roadmap step 9,
    item 4). A file picker as well as dragging.
  ```

- **The commands:**
  - `git diff origin/main...HEAD` (1 file, 28 lines added, 1 removed)
  - `git grep -n -i "more notifications\|todo.md"`: only `docs/architecture.md:2045` relies on the removed item's content
  - `git grep -n "AddDataProtection"` over `MedApp.API` and `MedApp.Infrastructure`: no match
  - `git merge-tree --write-tree --name-only HEAD origin/feature/duplicate-favourite-labels`: conflict in `docs/todo.md`
  - `gh run view 43825584963`: `head_sha` `78abce1`, conclusion `success`
