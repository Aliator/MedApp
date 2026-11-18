# Review of PR #150: permissions and the policy layer

- **PR:** https://github.com/Aliator/MedApp/pull/150, branch `feature/permissions`
- **Commit reviewed:** `89a19c6b`, against `main` at `975a9058`. **Date:** 18 November 2026.
- **High or critical findings:** none. One medium and one low, both fixed in the PR.

## Summary

Phase 2's roles and permissions (`docs/centre-phase2-plan.md`, decisions 51 to 68): permissions fixed in code, roles as data (`RolePermissions`), and every endpoint and handler moved from role names to permissions. Admin and "All staff (before roles)" (the old `User`) can do exactly what they could before.

## What changed, and why

- `Permission` catalogue, `StandardRoles`, the migration `AddedRolePermissions` (Admin built in, `User` renamed, five starting roles).
- Permissions read with the roles on every request (`RolePermissionQueries`), as claims; `whoami` lists them.
- `[RequirePermission]` on every action and the hub; a 403 `permission_missing` names the permission.
- Handlers ask the caller's permissions (`PathwayActor.Permissions`, `CalendarActor.Permissions`); `IPermissionHolders` decides who is told.
- Last-admin guard (`ILastAdminGuard`), `grant-admin`, `FirstAdminSetup` with Admin alone, the demo staff's starting roles.

## Design choices

- **Permissions in code, roles in data:** a permission is only real if code checks it, so admins can't invent one; the cost is a migration to rename one.
- **No cache:** one indexed join per request, so removing access applies at once; slower than a cached claim set, but by one query.
- **`[RequirePermission]` attribute** over `[Authorize(Policy = nameof(...))]`: typed and readable by the convention test.

## How it was checked

- Unit suite (with the endpoint-by-endpoint before/after test, the convention test and the catalogue pinned to the table); integration fixtures for permissions, admin safety, the migration and the areas the handlers touch; CI's full tier green on `89a19c6b`.

## Findings

1. **Medium, high confidence (fixed):** two admins revoking each other's Admin at once through the old `roles/revoke` could leave no admin.
   - **Evidence:** `RevokeRoleHandler.cs:27-35` called `lastAdmin.WouldLeaveNoAdminAsync` outside any transaction, and `LastAdminGuard.cs:24` only took `sp_getapplock` inside one, so the check and the change weren't serialised (also against `SetUserRoles` or `DeleteUser`).
   - **Fix:** the check, the revoke and its log entry now run in one `IIdentityTransactions.RunAsync`, as `SetUserRolesHandler` does; `LastAdminGuard` throws when called on SQL Server with no transaction, so a future caller can't skip the lock. Tests: `RevokeRoleHandlerTests.Handle_Admin_ChecksTheGuardAndRevokesInsideOneTransaction`, `AdminSafetyTests.RevokeRole_TwoAdminsRevokingEachOthersAdminAtOnce_LeaveOneAdmin`.
2. **Low, high confidence (fixed):** `grant-admin`'s security event was recorded after the commit (`AdminGrant.cs:74-82`), so a failure there would leave a grant with no record.
   - **Fix:** recorded inside the grant's transaction, so both are kept or lost together.

## Remaining risks

- The old role endpoints stay until the Roles and Permissions page replaces them.
- The web app still shows actions by `isAdmin` until it follows permissions (the next PR), so a starting role may see a button that answers 403.
- The `User` rename makes old pods refuse staff during the rollout (written in the chart README).
