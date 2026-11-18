# Review of #163: specimen tracking

[#163](https://github.com/Aliator/MedApp/pull/163), branch `feature/specimens`, reviewed 18 November 2026 (quick review, before the merges of `main`'s stage permissions, letters and retention classes).

**What it does:** PR 13 of `docs/centre-phases-3-5-plan.md`. Specimens are taken at a pathway's stage, numbered from a SQL sequence, printed on a label and request sheet, tracked to the lab and back, and their report attached as a file. It adds specimen types and labs, the permissions `RecordSpecimens` and `ProcessSpecimens`, and the last stage's gate. It also covers the timeline, the export, anonymisation and restricted records.

**What the review checked:**
- The report is never read or interpreted. It's stored and served as any attachment is.
- `AttachmentIntake`, taken out of `UploadAttachmentHandler`, keeps every check: sniffed type, size, malware scan, the target checked again, and deleting the bytes if the save fails.
- The label's QR code holds the specimen number only.
- Entered in error has its 403 guard: the recorder within 24 hours, or `CorrectRecords`.
- Audit details hold no free text.
- Stored numbers don't clash with `main`'s.

**Findings:** none critical, high or medium.
- **Low:** Lost and Entered in error check their permissions in the handler (`SpecimenWorkflow.MayMarkLost`, `MayMarkEnteredInError`), not in the endpoint's attribute, which needs only `ViewPatients`. This is deliberate, as either of two permissions, or the recorder's own 24 hours, allows them, and both return 403. It is listed in `AccessBeforeAndAfterPermissionsTests`. Left as is.
- **Low:** a report's thumbnail isn't made under `FileWorkLimiter`, the same as before for any upload. Only sniffing is limited. Left as is.

**Remaining risk:** until PR 14 (results), a wrong report can't be corrected: the file can't be deleted on its own, and there's no result to mark entered in error yet.
