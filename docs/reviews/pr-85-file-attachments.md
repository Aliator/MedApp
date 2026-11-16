# Review: PR #85, file attachments on patients, procedures and stages

**PR:** [#85](https://github.com/Aliator/MedApp/pull/85), branch `feature/file-attachments`, reviewed at `e8d55ce`. Compared with `main` at `16ee213` (the merge of #69, the branch's merge base): `git diff origin/main...HEAD`. `main` has since moved to `173f7fe` (#83, the page server sidecar, and #84, admin monitoring), which the branch doesn't contain and conflicts with (finding 5). Line numbers below are for `e8d55ce`.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the code, a local build and test run, Helm renders, or a scratch program run in Docker. **Two findings are high:** a crafted 1 MB image gets a copy of the API killed for using too much memory (finding 1), and the first deploy to the kind cluster after merging stalls, because its secret store has no attachments secret (finding 2). Three are medium and six are low.

## Summary

The PR lets staff attach files (scans, photos, results, letters) to a patient, a procedure or a stage, with the bytes in blob storage and the details in SQL, and joins them to the audit log, the timeline and the privacy controls. The serving side is right: files only leave as downloads with headers that stop a browser showing or running them, which was confirmed in Chromium, and the privacy joins (anonymisation, export, activity log, timeline) are complete for the ordinary path. The weak points are around the edges of an upload: decoding an image isn't bounded the way the code says, a deploy step is missing for kind, downloads reset the retention clock, and the security documents weren't updated.

## What changed, and why

194 files, 10,498 lines added and 295 removed, of which about 1,380 are generated (the migration's designer file and `src/api/schema.d.ts`). Staff had no way to keep the documents a procedure produces with the record; the to-do list and roadmap step 9 asked for this.

- **The API** (`MedApp.API/Controllers/AttachmentsController.cs`, all for staff): list (`GET api/attachments?patientId=` or `procedureId=` or `stageId=`, exactly one), upload (`POST`, same query), download (`GET {id}/content`), thumbnail (`GET {id}/thumbnail`) and delete (`DELETE {id}`).
- **Uploading** (`UploadAttachmentHandler`): the body is the file's raw bytes as `application/octet-stream`, with its name percent-encoded in an `X-File-Name` header. Anything else is a 415, which also keeps other sites out, since a cross-site page can't send that type without asking first (CORS), and nothing allows it. In order:
  - storage set up and the declared size under 20 MiB;
  - the target exists, its patient's record can be changed (not restricted or anonymised), and it takes files now;
  - the body streamed into a temporary file (`TempFileUploadStaging`, in `/tmp`) while it's counted and hashed, so 20 MiB is never in memory;
  - the type worked out from the contents (`FileTypeSniffer`), and the name's extension checked against it;
  - the malware scan (a stub today that marks everything "Not scanned");
  - the blob, then a thumbnail for PNG and JPEG (`SkiaThumbnailGenerator`), then the record and its audit entry in one save. If anything after the blob fails, the blob is deleted again.
- **Type sniffing** (`FileTypeSniffer`): PDF, PNG and JPEG by their first bytes ("magic bytes", a file's own label); HEIC and HEIF by their `ftyp` box; Word `.docx` by reading the ZIP's table of contents and its `[Content_Types].xml`, refusing macros; UTF-8 text with no control characters. SVG, HTML and anything starting with `<` are refused.
- **Serving** (`AttachmentResponseHeaders`): `Content-Disposition: attachment` with an ASCII and an RFC 5987 name, the sniffed type, `nosniff`, `no-store`, `Content-Security-Policy: sandbox; default-src 'none'` (replacing the `/api` policy), `Cross-Origin-Resource-Policy: same-origin` and `X-Frame-Options: DENY`. Only thumbnails appear in pages.
- **Deleting** is soft: the record is hidden from staff and stays for admins; the bytes stay until a purge job, which doesn't exist yet. Staff delete their own files where files can change; admins any file, except on a restricted or anonymised record. A patient or procedure with live files can't be deleted (409).
- **Audit and timeline:** adding, deleting and downloading are procedure audit entries (`AttachmentAudit`; a patient's own file has the all-zeros procedure ID `NoProcedure`). Added and deleted files are a new **Files** kind on the timeline; staff see that a file changed, admins also its name.
- **Privacy:** listing and downloading are in the activity log (deduplicated over 5 minutes); the subject access export lists every file's details; anonymising renames each file, scrubs `fileName` from the audit details and soft-deletes the live files.
- **Storage:** `IFileStore` with `BlobFileStore` (Azure Blob Storage SDK). Without a connection string, `UnconfiguredFileStore` answers 503 and everything else works.
  - **Development:** Azurite from the AppHost.
  - **Kubernetes:** an Azurite StatefulSet in the chart, with its own random key, a PVC, a network policy that lets only the API in, and the API's `/tmp` raised from 256 MiB to 1 GiB.
  - **Azure (not deployed):** a ZRS storage account with a private endpoint and DNS zone, a delete lock, 7 days' soft delete, and its connection string in Key Vault.
- **The migration** `AddedAttachments`: one table and four indexes, no foreign keys.
- **The web app:** a Files section on the patient page, the procedure page and each stage, with a drop area, progress and cancel per file, and Download and Delete.
- **Docs:** `AGENTS.md`, the handbook, the governance pack (DPIA risks 22 and 23, the data inventory, retention, rights) and the hazard log (H-23 to H-28).

## Design choices

- **The raw file as the body, not a multipart form:**
  - **Chosen because:** it streams straight into the temporary file, a 415 for any other type blocks cross-site sends, and there's no form parser to configure.
  - **Worse at:** the name must travel in a header, so it's percent-encoded and decoded again in app code (finding 4 notes the ASVS row this affects).
- **Type from the contents, with the extension made to match:**
  - **Instead:** an allowlist of extensions.
  - **Chosen because:** a file can't be a different kind of file from what its name says, so a renamed `.html` or `.exe` is refused, like a customs check that opens the parcel rather than reading the label.
  - **Worse at:** it checks the format, not what's inside it: a PDF with scripts or a Word document that fetches a remote template passes (finding 8). Some ordinary files are refused (a text file in Windows-1252, a PDF with bytes before `%PDF-`), with a clear message.
- **Server-made thumbnails with SkiaSharp:**
  - **Instead:** ImageSharp (commercial licence terms) or previews in the browser.
  - **Chosen because:** a re-encoded thumbnail carries none of the photo's metadata, and nothing uploaded is ever rendered as itself.
  - **Worse at:** a native image decoder now parses untrusted files inside the API process, so its memory is the API's memory (finding 1).
- **Downloads through the API, not links to storage:**
  - **Instead:** short-lived SAS links straight to the blob.
  - **Chosen because:** every download passes the API's checks, headers and audit.
  - **Worse at:** 20 MiB passes through an API copy per download.
- **Audit in the procedure audit log, with an all-zeros procedure ID for a patient's own files:**
  - **Instead:** a separate attachments audit table.
  - **Chosen because:** files show on the timeline and in the admins' log with no new query, and anonymisation already scrubs that table.
  - **Worse at:** every query over the audit log now meets file entries, including downloads, which are reads. The retention review is one such query, and it now counts downloads (finding 3).
- **No foreign keys, and a 409 for deleting a patient or procedure with live files:**
  - **Instead:** cascade deletes, or deleting the files with the patient or procedure.
  - **Chosen because:** the files' records must outlive what they were attached to until the bytes are purged, as the audit log does.
  - **Worse at:** a member of staff can't delete a patient who has a file someone else attached (only its uploader or an admin can delete it); an admin deletes files one at a time; and once a patient is deleted, their deleted files' names and bytes are out of reach of the export and anonymisation.
  - **As an existing feature:** this isn't a regression. No patient or procedure has files before this PR, so every delete that worked before still works; the rule matches the one for procedures. The governance pack says so (`docs/governance/data-subject-rights.md`).
- **Soft delete with no purge yet:**
  - **Chosen because:** a mistaken delete can be recovered by hand, and the purge window is the DPO's decision.
  - **Worse at:** deleted and anonymised patients' files are kept indefinitely. The DPIA (risk 5) and retention schedule say so plainly.
- **The account key in Key Vault on Azure, not workload identity:**
  - **Chosen because:** it matches the other secrets and needs no network path to Entra ID.
  - **Worse at:** the key can do anything to the account. Listed under "Not yet" in `deploy/azure/README.md`.
- **Staging uploads in `/tmp`:**
  - **Instead:** buffer in memory, or stream straight to blob storage.
  - **Chosen because:** the file must be read several times (sniffing, scan, thumbnail) before it's stored, without 20 MiB per upload in memory.
  - **Worse at:** the files land on the pod's disk, and outlive a crash (finding 7).

## How it was checked

- **Read:** the whole diff, the PR description, both `AGENTS.md` files (Attachments, the timeline, privacy, the security baseline, the review format), `docs/governance/` (the DPIA, data inventory, retention schedule, rights procedure), the hazard log (H-10, H-23 to H-28), `docs/security/`, and the code around the changes: the retention review, the anonymiser, the patient record repository, the security headers middleware, the exception middleware, the Argo CD Application and the chart's other templates.
- **Build:** `dotnet build MedApp.sln --configuration Release`: 0 errors; 1 warning, the deprecated Blazor client's existing CS0414.
- **.NET tests:** unit, 1,831 passed. Integration (the real API against SQL Server and Azurite in Testcontainers), 284 passed in 5 minutes 2 seconds; the containers were removed afterwards.
- **Web:** `npm run lint`, `npm run typecheck` and `npm run theme:check` clean; `npm run coverage`, 1,239 tests passed, with 99.45% statements, 93.5% branches, 99.8% functions and 99.83% lines; `npm run build` succeeded.
- **The migration:** `has-pending-model-changes` reports none; the SQL from `dotnet ef migrations script` is one `CREATE TABLE` and four `CREATE INDEX` in a transaction. Additive only, so the previous image still runs against the new schema.
- **Helm:** all seven `helm lint --strict` configurations from CI's `helm` job pass with Helm 3.22.1 (in Docker), and kind's values render. The rendered Azurite StatefulSet and network policies were read through.
- **Scratch programs** (outside the repository; details in [Detail](#detail)):
  - **Thumbnails:** progressive JPEGs made to measure, through the real `SkiaThumbnailGenerator`, in the `aspnet:10.0` Linux image with the chart's 1 GiB memory limit. A 500 KB file took the process to 550 MiB; a 1.1 MB one got it killed (exit 137).
  - **Staging:** the same file read from a real `TempFileUploadStaging` file, with a read-only root and a writable `/tmp`. The process was killed, and the staged file was still in `/tmp` afterwards.
  - **ZIP:** a 20.7 MB ZIP with 440,000 entries in its table of contents went through `FileTypeSniffer`: refused, but the process peaked at 203 MiB, against 34 MiB for a small one.
  - **DOCX:** a macro-free `.docx` with a remote template, an external OLE link and an embedded object was accepted as a Word document.
  - **Azurite** 3.37.0 under the chart's restrictions (read-only root, user 1000, every capability dropped): it starts, and the real `BlobFileStore` saves, reads and deletes with the chart's account. The well-known `devstoreaccount1` with its published key is refused.
  - **Chromium** 156 (Playwright): a file sent with exactly `AttachmentResponseHeaders`' headers downloads from an `<a download>`, a plain link and a direct navigation; a thumbnail sent the same way shows in an `<img>`.
- **The kind cluster on this machine, read only:** Argo CD's `medapp` Application is Synced and Healthy on `env/kind`; the `medapp-vault` namespace holds `medapp-db`, `medapp-first-admin` and `medapp-backplane`, and no `medapp-attachments`.
- **CI:** none. The PR conflicts with `main` (`mergeStateStatus: DIRTY`), so GitHub runs no `pull_request` workflow, and no run exists for `e8d55ce` (finding 5).
- **Not done:** the Bicep wasn't built (no Bicep CLI here, and CI hasn't run it either); nothing was deployed to kind; no UI parity screenshots; the app wasn't run in a browser; how long a hung storage account holds an upload wasn't measured (finding 9).

## Findings

Most severe first. Two are high, three medium and six low. None was fixed at the time of this review.

### 1. A crafted 1 MB JPEG gets a copy of the API killed for memory

- **Severity:** high. **Confidence:** high (reproduced).
- **What could go wrong:** a member of staff attaches a photo that came from outside, say by email. It's a progressive JPEG (one stored in several passes, common on the web) whose header says 24,000 by 24,000 pixels, though the file is only 1.1 MB. To make the thumbnail, the decoder scales it to an eighth (3,000 by 3,000, well under the 24-million-pixel cap), but a progressive JPEG must first hold the whole image's coefficients, its compressed building blocks, at full size: about 2 bytes a pixel, so over 1 GB. The API container's limit is 1 GiB, so the kernel kills it mid-request. Every other request on that copy fails with it. kind runs two copies, so two such uploads take the whole app down for a while; staff who see "The file couldn't be sent" and try again repeat it, and Kubernetes waits longer before each restart (up to five minutes). Each crash also leaves the staged file behind (finding 7).
  - It's like a parcel labelled "small box" that has to be fully unpacked on the floor before anyone can look at it: the pixel cap measures the box, not the floor space.
  - **The same gap, smaller:** a 24-megapixel PNG decodes at full size, about 96 MB, as the code's own comment says, and a ZIP whose table of contents lists 440,000 entries costs about 170 MB before it's refused. Nothing limits how many uploads decode at once, so a handful at the same time reach the limit too.
  - **The docs claim the opposite:** `AGENTS.md` says "Decoding is bounded", and the class comment says an image whose header claims any size is handled. H-10 (MedApp unavailable, Major) gains a cause the hazard log doesn't list.
- **Evidence:**
  - `MedApp.Infrastructure/Attachments/SkiaThumbnailGenerator.cs:12-14` (the claim), `:55-56` (the cap checks only the scaled size), `:61` (`GetPixels`, where the full-size buffer is allocated)
  - `MedApp.Application/Attachments/Commands/UploadAttachment/UploadAttachmentHandler.cs:68`, `:141`: thumbnails made in the request, with no limit on how many at once
  - `MedApp.Application/Attachments/Common/FileTypeSniffer.cs:148`: `new ZipArchive` reads the whole table of contents into objects
  - `deploy/helm/medapp/values.yaml:21`: the API's 1 GiB memory limit
  - `AGENTS.md:170`: "Decoding is bounded"
  - `MedApp.UnitTests/Infrastructure/Attachments/SkiaThumbnailGeneratorTests.cs:79-88`: the only huge-image test is a PNG, which isn't scaled, so the JPEG path is untested
  - The measurements are in [Detail](#detail).
- **Fix:**
  - **Cap the source, not just the result:** before `GetPixels`, refuse a thumbnail when the image's own width times height is over a budget for what its decoder must hold (for example 40 to 50 million pixels, which still covers a 48-megapixel phone photo, or a lower one for progressive JPEGs, found by reading their `SOF2` marker).
  - **Limit concurrency:** a `SemaphoreSlim` (say 2) around thumbnail making and ZIP inspection, so a burst can't add up.
  - **Bound the ZIP before opening it:** read the end-of-central-directory record's entry count first and refuse a `.docx` with more than a few thousand parts (real ones have tens).
  - **Test it:** a unit test with a progressive JPEG header claiming 24,000 by 24,000 (the generator in Detail is 20 lines) that must return null quickly. Correct `AGENTS.md` and the class comment, and add the cause to H-10.

### 2. The first deploy to kind after merging stalls: the secret store has no attachments secret

- **Severity:** high. **Confidence:** high that the secret is missing and the pods can't start (checked on this machine's cluster, read only); medium on exactly how Argo CD reports it.
- **What could go wrong:** the merge runs Publish, which moves `env/kind`, and Argo CD syncs it automatically. kind's values turn on Azurite and take secrets from External Secrets, so the chart now makes an ExternalSecret for `medapp-attachments`, reading the store's `medapp-attachments` entry. Only `deploy/kind/up.sh` creates that entry, and only when it's run. On a cluster set up before this PR, like the one on this machine, it doesn't exist:
  - the ExternalSecret can't sync, so the Secret is never made, and Argo CD sees the ExternalSecret (in wave -1) as Degraded and fails the sync before the migrations and the new API;
  - if a sync did get further, Azurite and every new API pod would stop at `CreateContainerConfigError`, since both read the Secret without `optional`.
  - **The result:** the merge never reaches kind, and nothing in the merge flow notices. Step 8 checks only that the Publish workflow succeeded, which it will. The kind README says the script fills the store "once", which reads as if nothing more is needed.
- **Evidence:**
  - `deploy/kind/up.sh:114-121`: the only place `medapp-attachments` is put in the store
  - `deploy/helm/medapp/templates/externalsecrets.yaml:19-20`: the new ExternalSecret; `deploy/kind/values.yaml:23-31`: Azurite on, secrets from the store
  - `deploy/helm/medapp/templates/api.yaml:79-83` and `deploy/helm/medapp/templates/azurite.yaml:63-67`: `secretKeyRef` to that Secret, not optional
  - `deploy/argocd/medapp-kind.yaml:37`: automated sync
  - `deploy/kind/README.md:42`: "Fills the store with random passwords (and Azurite's account key), once"
  - `AGENTS.md:474`: the merge flow's deploy check looks at Publish only
  - On this machine: `kubectl -n medapp-vault get secrets` lists `medapp-backplane`, `medapp-db` and `medapp-first-admin` only.
- **Fix:** before merging, run `deploy/kind/up.sh --argocd` once against the existing cluster (it adds the missing entry and changes nothing else that exists), and say so in the PR description and in the kind and Argo CD READMEs: a change that adds a store entry needs the script run before it merges. Make the merge flow's step 8 also check that Argo CD's sync of the merge commit succeeded. Optionally, mark the API's `ConnectionStrings__Attachments` reference `optional: true`, so a missing secret means "no file storage" (a 503) rather than an API that can't start.

### 3. Downloading a file pushes back the patient's retention review

- **Severity:** medium. **Confidence:** high (from the code; not run).
- **What could go wrong:** the retention review lists records whose period has passed since the "conclusion of treatment", which the retention schedule defines as the latest of the last closed procedure and "the date the patient's record was last changed". The code works it out from the procedure audit log too, with no filter on the action, and this PR writes an audit entry for every download. So:
  - an admin reviewing a record that's due opens one of its letters to decide, and the record drops off the review list, its due date moved a full period (8 years for an adult) into the future;
  - any routine download of an old document does the same, silently.
  - It's like a library that treats taking a book off the shelf to look at it as a new loan, so the book never reaches the weed-out list. This changes an existing feature, and the governance pack's retention promises with it.
- **Evidence:**
  - `MedApp.Application/Attachments/Common/AttachmentAudit.cs:29`: every attachment entry carries the patient's ID; `MedApp.Application/Attachments/Commands/DownloadAttachment/DownloadAttachmentHandler.cs:40-42`: one per download
  - `MedApp.Infrastructure/Data/Repositories/PatientRepository.cs:92` (any audit entry after the cut-off excludes the patient) and `:102` (the latest audit entry is the conclusion of treatment)
  - `MedApp.Application/Patients/Common/RetentionSchedule.cs:25-28`
  - `docs/governance/retention-schedule.md:26-28`: "the date the patient's record was last changed"
- **Fix:** leave `AttachmentDownloaded` out of both subqueries (`&& a.Action != PatientProcedureAuditAction.AttachmentDownloaded`), with a test that a download after the cut-off still lists the patient. Adding and deleting a file are changes to the record and can keep counting.

### 4. The security baseline's documents still say MedApp has no file uploads

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** `AGENTS.md` says a change to a data flow should update the threat model and the ASVS checklist. This PR adds the biggest new attack surface since the baseline, and changes neither. The ASVS file handling chapter still marks 8 of its 9 rows "Not applicable" because "MedApp has no file uploads or downloads", and row 1.1.1 still says no app code decodes input again (the upload's file name is percent-decoded in `AttachmentResponseHeaders.DecodeFileName`). The threat model has no threat about uploads: no denial of service by a hostile file (finding 1), no malware passed to workstations, no blob store and its key. Used as evidence, for a DSPT submission or a customer's security questionnaire, these documents give a wrong answer, and the rows that would now be "partly met" (5.2.1 sizes the app can process, 5.2.3 limits on archives, 5.4.3 malware scanning) would point straight at the gaps above.
- **Evidence:**
  - `docs/security/asvs.md:171-187` (V5, "MedApp has no file uploads or downloads"), `:76` (1.1.1), and the summary's count of 8 not-applicable file handling rows
  - `docs/security/threat-model.md`: no upload or attachment threat among T-01 to T-46
  - `AGENTS.md:281`: "A change to authentication, sessions, headers or a data flow should update both."
- **Fix:** fill in V5 from the code (5.1.1 documented types and size: met; 5.2.1: partly, finding 1; 5.2.2 content and extension checked: met; 5.2.3: partly, no entry-count limit; 5.3.1 never run: met; 5.3.2 storage keys from IDs only: met; 5.4.1 and 5.4.2 `Content-Disposition` with both names: met; 5.4.3 malware scanning: planned), correct 1.1.1, update the summary counts, and add threats for hostile files (availability), malware to workstations, the storage account and its key, and Azurite's plain http in the cluster.

### 5. The branch conflicts with `main` in 17 files, so CI has never run

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** `main` gained #83 and #84 after the branch's last merge. A trial merge (`git merge-tree`) conflicts in 17 files. Four are source and two are chart templates:
  - `PatientAnonymiser.cs` (#84 skips notifications with a fixed title; this PR adds file anonymisation)
  - `DeletePatientProcedureHandler.cs` (#84 calls the procedure monitor; this PR counts files first)
  - `PatientPrivacyTests.cs`, `deploy/helm/medapp/templates/api.yaml` (#83's `pages` container) and `networkpolicy.yaml`
  - plus `AGENTS.md`, the chart and kind READMEs, the handbook, and seven governance and clinical safety documents.
  - **Because it conflicts,** GitHub runs no workflow for the PR: none of the required checks (`web`, `helm`, `unit-tests`, `image`, `integration-tests`, `e2e`) has run on any of its commits, the Bicep included. The PR description's "how it was checked" is local only.
  - **Numbers that collide:** #84 also made the hazard log version 0.6 and the DPIA version 0.4. Risk and hazard numbers don't collide (#84 took risk 24 and H-29, leaving 22, 23 and H-23 to H-28 to this PR).
  - **The migration:** #84's `AddedMonitoringRules` (11:42:24) sorts before `AddedAttachments` (11:48:44), so the order holds, but this migration's designer model doesn't include #84's tables. The repository's practice is to re-create the branch's migration after merging `main` (as #69 did).
- **Evidence:** `git merge-tree --write-tree --name-only e8d55ce origin/main`; `gh pr view 85 --json mergeable,mergeStateStatus` (`CONFLICTING`, `DIRTY`); no workflow run for `e8d55ce`. The full list is in Detail.
- **Fix:** merge `main`, keeping both sides in the anonymiser (the fixed-title filter and the file loop) and the procedure delete handler (the file count before the change, the monitor call after it); renumber this PR's hazard log entry to 0.7 and DPIA entry to 0.5; re-create `AddedAttachments` on top of `AddedMonitoringRules` and check `has-pending-model-changes`; regenerate `schema.d.ts`; then let CI run and read every job.

### 6. An upload in progress isn't checked again, so a file can land on a record that changed meanwhile

- **Severity:** low. **Confidence:** high (from the code; not reproduced).
- **What could go wrong:** the handler checks the target (it exists, the record can change, it takes files) before reading the body, then spends as long as the upload takes (a minute for 20 MiB on a slow link) before saving, and doesn't check again. It's like checking a door is open, then spending a minute carrying a box in while someone locks it behind.
  - **Anonymised meanwhile:** the file is saved live, under its real name, with the real name in its audit entry, on a record the governance pack says holds no file names and no live files.
  - **Restricted meanwhile:** a file is added to a record that can't change.
  - **The patient or procedure deleted meanwhile** (both are refused only while they have live files, and this one isn't saved yet): the file's record points at nothing, and no export, anonymisation or purge of deleted files will ever find it.
  - **The stage finished meanwhile:** a file is added to a locked stage.
  - All need two people acting on the same record within one upload, so they're rare.
- **Evidence:** `MedApp.Application/Attachments/Commands/UploadAttachment/UploadAttachmentHandler.cs:44-48` (the checks), `:54` (the body read, after them), `:89` (the save, with no check); `MedApp.Infrastructure/Data/Repositories/AttachmentRepository.cs:12-17`.
- **Fix:** after staging, resolve the target again and repeat `EnsureCanChange` and `WhyFilesCantChange` before storing the blob. That narrows the window from the upload's length to milliseconds; closing it fully would need the save to re-read the patient's flags in the same transaction.

### 7. Staged uploads outlive a crash, in the pod's `/tmp`

- **Severity:** low. **Confidence:** high (reproduced).
- **What could go wrong:** the staging class says each temporary file is "deleted as soon as its stream is disposed, or by the system if the process dies first". On Windows that's true. On Linux, where the image runs, .NET's `DeleteOnClose` deletes the file when the stream is disposed, so a process that's killed (finding 1's crash, or any out-of-memory kill) leaves the file behind. In Kubernetes `/tmp` is an `emptyDir`, which survives container restarts and lasts as long as the pod. So whole copies of patients' files, up to 20 MiB each, sit on the node's disk outside retention, anonymisation and the export, and they count towards the 1 GiB limit, past which the kubelet evicts the pod. It's like a note meant to go in the shredder when the meeting ends, left on the table when the fire alarm empties the room.
- **Evidence:** `MedApp.Infrastructure/Attachments/TempFileUploadStaging.cs:8` (the claim), `:20` (`DeleteOnClose`); `deploy/helm/medapp/templates/api.yaml:137` (`sizeLimit: 1Gi`). Reproduced: after the kill, `ls /tmp` showed `medapp-upload-….tmp`, 1,125,118 bytes, mode 0600.
- **Fix:** on Linux, delete the path straight after opening it (`File.Delete(path)` once the `FileStream` exists): the open handle keeps the bytes readable until it's closed, and nothing is left if the process dies. Sweep stale `medapp-upload-*.tmp` files at start-up as well, and correct the comment.

### 8. The type check stops file formats, not active content inside the allowed ones

- **Severity:** low. **Confidence:** high on what passes (a DOCX was tried); medium on how much harm it allows on a workstation.
- **What could go wrong:** "Macros refused" is accurate, but a Word document doesn't need macros to be dangerous. A `.docx` with a remote template (template injection, a common way to fetch macros after the file is opened), an external OLE link to a `\\server\share` path (which can leak the user's Windows credentials hash when opened), or an embedded OLE object passes, and so does a PDF with JavaScript, a launch action or embedded files, since only its first five bytes are checked. Browsers never run them here (the headers do their job), but the hazard log's H-25 counts the type list as a control against malware reaching a workstation. Protected View, which Office applies to downloaded files, and the malware scanner required before first live use are the real controls.
- **Evidence:** `MedApp.Application/Attachments/Common/FileTypeSniffer.cs:150-167` (only `vbaProject.bin` and the declared content types are checked), `:76-77` (PDF by prefix); `docs/clinical-safety/hazard-log.md` H-25's existing controls. A scratch `.docx` with all three features was accepted as a Word document (Detail).
- **Fix:** refuse a `.docx` with any `TargetMode="External"` relationship in its `.rels` parts, or with parts under `word/embeddings/` or `word/activeX/`; refuse PDFs containing `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile` or `/RichMedia` (a byte search; false positives are acceptable here). Or say in H-25 that active content in allowed types is left to the scanner.

### 9. When storage hangs, an upload can take many minutes to fail, and fails as a 500

- **Severity:** low. **Confidence:** medium (from the SDK's defaults; not measured).
- **What could go wrong:** the blob client is made with the SDK's defaults: 3 retries with backoff and 100 seconds per try. If the storage account stops answering (a dropped private endpoint, a firewall change), an upload waits through all of that, then the clean-up deletes the blob (and the thumbnail) with `CancellationToken.None`, waiting through the same retries again, while the staged file sits in `/tmp`. The request ends as a generic 500. H-28 already records the 500 and proposes a 503; the length of the wait isn't recorded anywhere.
- **Evidence:** `MedApp.Infrastructure/Common/Extensions/ServiceExtensions.cs:62` (no `BlobClientOptions`); `MedApp.Infrastructure/Attachments/BlobFileStore.cs:38` (only 404 handled); `UploadAttachmentHandler.cs:96-98`, `:155`.
- **Fix:** set `BlobClientOptions.Retry` (for example 2 retries and a 20 to 30 second network timeout), map other storage failures to `FileStorageUnavailableException` (503) as H-28 proposes, and give the clean-up its own short timeout.

### 10. Anonymising deletes files without audit entries

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** `AGENTS.md` says adding, deleting and downloading a file are audit entries. The anonymiser soft-deletes every live file, recording the admin on the file, but writes no `AttachmentDeleted` entry, so the patient's timeline and the audit log show files attached and never removed. The activity log's `PatientAnonymised` entry is the only record of when they went.
- **Evidence:** `MedApp.Application/Patients/Common/PatientAnonymiser.cs:85-95`; `AGENTS.md` (Attachments, "Audit").
- **Fix:** add an `AttachmentDeleted` entry per file the anonymiser deletes (its `fileName` already scrubbed), or say in `AGENTS.md` and the DPIA that anonymisation's deletions are recorded only on the files and in the activity log.

### 11. The restriction guard's convention test now lets any handler through that calls `EnsureCanChange` anywhere

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the test stops procedure commands from skipping the guard that checks the patient's record. It used to make one named exception (creating a procedure); now any handler that calls `PatientRules.EnsureCanChange` anywhere passes, so a future procedure command that loads the procedure without the guard and checks the wrong patient, or checks on only one branch, passes too.
- **Evidence:** `MedApp.UnitTests/Application/Procedures/Common/ProcedureRestrictionGuardConventionTests.cs:69-71`.
- **Fix:** list `UploadAttachmentHandler` and `DeleteAttachmentHandler` by name beside `CreatePatientProcedureHandler`, as `DownloadAttachmentHandler` already is.

## Remaining risks

- **No malware scanning:** every file is "Not scanned". The DPIA and hazard log make a scanner a condition of first live use.
- **No purge:** deleted files, an anonymised patient's included, keep their bytes (and thumbnails) indefinitely, and a deleted patient's deleted files keep their names and bytes out of reach of the export and anonymisation. Documented (DPIA risk 5, the retention schedule, the rights procedure).
- **Thumbnails aren't in the activity log:** a thumbnail can be fetched by ID without a list, and staff get file IDs from the timeline. Documented as a choice.
- **The subject access export lists files but doesn't include them.** Documented (`docs/todo.md`).
- **Invisible characters stay in names:** the cleaning removes control and direction characters, but zero-width and other format characters (U+200B to U+200D, U+FEFF) and line separators (U+2028) remain, so two files can look identical in a list (H-27).
- **Restricted records' files can still be listed and downloaded,** by design.
- **No end-to-end test uploads a file;** the `e2e` job runs without storage (`docs/todo.md`).
- **Azure is untested:** the storage account, private endpoint and DNS zone have never been deployed, and the Bicep hasn't been built by CI for this branch (finding 5).
- **Azurite on kind** is one pod and one volume, unencrypted, with plain http inside the cluster; it's for kind only, and the chart says so.

## Say it like this

"PR #85 lets staff attach scans, photos, results and letters to a patient, a procedure or a stage. Files go to blob storage, their type is checked from their contents, they're only ever downloaded (never shown in the page), photos get a small server-made thumbnail, and every add, delete and download is audited and joined to the privacy controls. The serving side and the privacy joins are done well, and every local test suite passes. Two things must be fixed before it merges. First, a crafted 1 MB photo can crash a copy of the API, because making its thumbnail needs more than a gigabyte of memory, though the docs say decoding is bounded. Second, the kind cluster's first deploy after merging will stall, because the secret the new storage needs is only created by re-running the setup script. Also worth fixing: downloading a file wrongly resets the patient's retention clock; the security checklist still says there are no uploads; and the branch conflicts with main, so CI hasn't run at all. The rest is small: uploads aren't re-checked after a slow transfer, temporary files survive a crash, Word and PDF files can still carry active content, a hung storage account makes uploads wait for minutes, anonymising deletes files without audit entries, and one convention test got looser."

## Detail

### The scratch programs

A console project in a temporary folder, referencing the branch's `MedApp.Infrastructure`, published for `linux-x64` and run in `mcr.microsoft.com/dotnet/aspnet:10.0` with `--memory=1g --memory-swap=1g` (the chart's limit). The JPEG is generated, not downloaded: a grey, progressive (`SOF2`) image with one DC scan in which every block is "no change", so it compresses to about one bit per 8 by 8 block.

```csharp
W(0xFF, 0xD8);                                                  // SOI
W(0xFF, 0xDB, 0x00, 0x43, 0x00); /* 64 × 0x01 */                // DQT
W(0xFF, 0xC2, 0x00, 0x0B, 0x08, h >> 8, h, w >> 8, w, 0x01, 0x01, 0x11, 0x00); // SOF2, one component
W(0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01, /* 15 × 0x00 */ 0x00);    // DHT: one 1-bit code, category 0
W(0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00);  // SOS: DC first scan
/* blocks / 8 zero bytes, the last byte padded with ones */ W(0xFF, 0xD9);
```

Results (`FileTypeSniffer` reads each one as a JPEG image):

| Image | File size | Thumbnail | Peak memory |
|---|---|---|---|
| 4,000 × 4,000 | 31,368 bytes | 881 bytes, 102 ms | 70 MiB |
| 16,000 × 16,000 | 500,118 bytes | 881 bytes, 1,025 ms | 550 MiB |
| 24,000 × 24,000 | 1,125,118 bytes | none: killed, exit 137 | over 1 GiB |

The same 24,000 × 24,000 file through `TempFileUploadStaging`, with `--read-only --tmpfs /tmp --user 1654` (the image's `app` user):

```
staged in /tmp/medapp-upload-4a0b620fecbf432da733882f2c9ef9fb.tmp
Killed
dotnet exit: 137
-rw------- 1 app  app  1125118 Nov 16 12:23 medapp-upload-4a0b620fecbf432da733882f2c9ef9fb.tmp
```

The ZIP: one local header, then a central directory of single-character names (ZIP64 records past 65,535 entries).

| Entries | File size | Result | Peak memory |
|---|---|---|---|
| 1,000 | 47,053 bytes | refused, 32 ms | 34 MiB |
| 440,000 | 20,680,129 bytes | refused, 881 ms | 203 MiB |

The DOCX: `[Content_Types].xml` declaring only the ordinary Word main part, `word/document.xml`, `word/_rels/settings.xml.rels` with an `attachedTemplate` relationship to `https://attacker.invalid/t.dotm` (`TargetMode="External"`), `word/_rels/document.xml.rels` with an external `oleObject` relationship to a UNC path, and `word/embeddings/oleObject1.bin`. `FileTypeSniffer.Identify(…, "letter.docx")` returned `Word document`.

### The download headers in a browser

A small Node server sent a PDF and a PNG with exactly the headers `AttachmentResponseHeaders.Apply` sets, plus the middleware's `Referrer-Policy`. In Chromium 156 through Playwright: clicking `<a href download>`, clicking a plain link and navigating straight to the URL each produced a completed download named `letter.pdf`, and the PNG showed in an `<img>` (`naturalWidth` 1). So `sandbox` in the policy doesn't stop downloads, and the integration test `UploadAndDownload_PatientFile_RoundTripsWithTheSafeHeadersAndIsAudited` shows the API sends one policy, the sandboxing one, replacing `/api`'s.

### The trial merge with `main` at `173f7fe`

Conflicts: `AGENTS.md`, `MedApp.Application/Patients/Common/PatientAnonymiser.cs`, `MedApp.Application/Procedures/PatientProcedures/Commands/DeletePatientProcedure/DeletePatientProcedureHandler.cs`, `MedApp.IntegrationTests/Privacy/PatientPrivacyTests.cs`, `deploy/helm/medapp/README.md`, `deploy/helm/medapp/templates/api.yaml`, `deploy/helm/medapp/templates/networkpolicy.yaml`, `deploy/kind/README.md`, `docs/architecture.md`, `docs/clinical-safety/clinical-safety-case-report.md`, `docs/clinical-safety/hazard-log.md`, `docs/clinical-safety/intended-purpose-and-device-decision.md`, `docs/governance/README.md`, `docs/governance/data-inventory.md`, `docs/governance/data-subject-rights.md`, `docs/governance/dpia.md`, `docs/governance/privacy-notice-staff-template.md`. `MedAppDbContextModelSnapshot.cs` merges without a conflict, which is why the migration needs re-creating rather than trusting.

### Sketches of the fixes

Finding 3, in `PatientRepository.GetRetentionCandidatesAsync`:

```csharp
// A download is a read, not a change to the record: it mustn't move the conclusion of treatment.
var changes = context.PatientProcedureAuditEntries
    .Where(a => a.Action != PatientProcedureAuditAction.AttachmentDownloaded);
// ...then use `changes` in place of context.PatientProcedureAuditEntries in the Any(...) and the Max(...).
```

Finding 7, in `TempFileUploadStaging.Create`:

```csharp
var stream = new FileStream(path, options);
if (!OperatingSystem.IsWindows())
    File.Delete(path); // unlinked now; the open handle keeps the bytes until it's closed, even if the process dies
return stream;
```

Finding 1, in `SkiaThumbnailGenerator.Create`, before choosing the decode size:

```csharp
// A progressive JPEG holds its full-size coefficients whatever the scale, so cap the source too.
if ((long)codec.Info.Width * codec.Info.Height > MaxSourcePixels)
    return null;
```

with `MaxSourcePixels` chosen against the API's memory limit, and a shared `SemaphoreSlim` around `Create`.

### What was confirmed sound

- **Cross-site uploads are blocked twice:** the session cookie is `SameSite=Lax` (not sent on a cross-site POST) and `__Host-` (no subdomain can set it), and an `application/octet-stream` body with an `X-File-Name` header needs a CORS preflight nothing answers. Even a same-site page sending a CORS-safe type gets the 415, and without the header the validator's 400.
- **File names can't break out:** control and direction characters are removed before storing; the `filename` fallback is ASCII without quotes or backslashes, and `filename*` is percent-encoded UTF-8; the stored name always ends in the extension of the type found, so a name can't make a file a different kind of file.
- **IDs can't be mixed:** every request names exactly one of a patient, procedure or stage (the validators), and a stage's procedure and patient come from the database, not the request.
- **Staff never see a file's name on the timeline,** and `TimelineStaffCanaryTests` covers every attachment action with the name as its canary.
- **Anonymisation reaches every place a name is kept:** the `Attachment` row, `fileName` in every audit entry's details (the history is loaded by patient, so patient-level entries are included), and nowhere else: the activity log, notifications, blob keys and blob metadata hold no names.
- **Azurite's well-known account is off** once `AZURITE_ACCOUNTS` is set, and Azurite runs under the `restricted` Pod Security Standard with a read-only root.
