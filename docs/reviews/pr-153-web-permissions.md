# Review: PR #153, the web app follows permissions

**PR:** [#153](https://github.com/Aliator/MedApp/pull/153), branch `feature/web-permissions`, reviewed at `d66522c` on 18 November 2026. A quick review of the backend diff; the web diff's file list was skimmed; nothing was run. CI (full tier): every job passed except `e2e`, still running at merge (https://github.com/Aliator/MedApp/actions/runs/44309047102). No findings.

The web app asks for permissions (`useCan`, `requirePermission` route guards, `/no-permission`, `permission_missing` 403s shown in dialogs and as a toast) instead of "is admin". On the API, `Can...` flags take the viewer's permissions, assignment needs the assignee to hold `WorkPathways`, and `GET api/calendar/staff` takes a `permission` filter.

**Checked:** each `Can...` flag in `PathwayMapper.cs` and `PathwayNoteMapper.cs` is the old record-and-workflow check and the matching permission (`WorkPathways`, `AssignPathways`, `OverrideStages`, `DeletePathways`, `CorrectRecords`), so flags only became stricter; `AttachmentMapper.cs:59-60` matches `AttachmentsController.cs:111`. `AssignPatientPathwayHandler.cs:42-45` adds a server-side check and removes none. The staff filter only narrows a list that already needs `UseApp` and returns the same fields (ID, username, week template). No endpoint's permission was loosened.

**Remaining risk:** a page already open when a permission is removed keeps its route until the next navigation (its buttons hide); the server refuses the action either way.
