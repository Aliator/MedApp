# Review: PR #149, specialties

**PR:** [#149](https://github.com/Aliator/MedApp/pull/149), branch `feature/specialties`, reviewed at `2c4f2bc` on 18 November 2026. A quick review of the migration, the controllers' permissions and the membership handler; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44301167708. No findings.

Adds an admin-managed specialty list (`api/specialties`), a specialty on every pathway template (copied onto each new pathway), staff memberships (`api/auth/users/{username}/specialties`, `api/auth/me/specialties`), the Specialties page, the template editor field, the Users page dialog and the demo clinic's five specialties.

**Checked:** `AddedSpecialties` (`20261118120357`) gives both new non-null `SpecialtyId` columns a default of General's ID and inserts General (line 71) before adding the foreign keys (97-110), so existing rows comply; nothing is dropped; memberships cascade with the account and restrict a specialty's deletion. `SpecialtiesController` reads need `UseApp` and changes need `ManageSetup`; others' memberships need `ManageUsers`, your own `UseApp`. Membership changes and their `StaffSpecialtiesChanged` (42, free on `main`) entry are saved together, with names, IDs and the username only.

**Remaining risk:** the new screens still check `isAdmin` until the web app follows permissions; one synthetic parity meeting was created in the owner's local dev database while screenshots were taken through the wrong proxy (cleared by the next reseed).
