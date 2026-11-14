# MedApp: to do

Smaller improvements to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Users

- **Combine Assign roles and Revoke roles into one "Edit roles" action.**
  - **Today:** each row on the Users page (`MedApp.Web/src/pages/users/UsersPage.tsx`) has two buttons, **Assign roles** and **Revoke roles**. Each opens its own `SelectionDialog`: one lists the roles the user doesn't have, the other the roles they do. Changing someone from User to Admin, or both ways, takes two dialogs.
  - **Instead:** one **Edit roles** button opening one dialog that lists every role, with the user's current roles ticked. Saving applies the difference: it assigns the newly ticked roles and revokes the unticked ones. Keep the rule that admins can't change their own roles (the button stays disabled on your own row).
  - **API:** either keep the two existing calls (`useAssignRoles` and `useRevokeRoles`, sent one after the other), or add one endpoint that sets a user's roles to an exact list. One endpoint is better: it changes the roles in one step, so a failure can't leave them half-changed.
  - **Tests and screenshots:** update `UsersPage.test.tsx`, and replace the `users-roles` screen in `tools/ui-parity/screens.ts`.

- **Ask for a password confirmation before deleting a user.**
  - **Today:** **Delete user** on the Users page opens a confirmation dialog, and confirming calls `DELETE api/auth/users/{username}` straight away. Deleting an account is permanent, so one click too many is costly.
  - **Instead:** the dialog asks the admin to type their own password, and Delete User stays disabled until they do.
  - **API:** the API must check it, not just the page. Send the password with the request (a body on the delete, or a `POST .../users/{username}/delete`), check it against the signed-in admin with Identity's `CheckPasswordAsync`, and refuse with a 401 or 403 if it's wrong. Failed attempts should count towards lockout, as sign-in failures do.
  - **Tests:** handler tests for a right and a wrong password, and an update to `UsersPage.test.tsx`.

## Appearance

- **Move the light/dark mode toggle into Account Settings.**
  - **Today:** it's an icon button at the bottom of the side nav (`AppSideNav.tsx`, using `useColorMode` from `src/app/colorMode.tsx`). The choice is kept in this browser.
  - **Instead:** a setting on the Account page (`AccountPage.tsx`), alongside the password form, with the side nav button removed.
- **Fix the light mode colour scheme.**
  - **The side nav** should be styled to match dark mode, rather than looking different from it.
  - **Cards** should still stand apart from the page background, as they do in dark mode: darker than the bright background.
  - **Where:** colours are tokens in `src/themes/medapp/medappTheme.ts` (each a [light, dark] pair). Rebuild with `npm run theme:build`. Check every page in light mode with the UI parity screenshots.

## Navigation

- **Add a half view, to see a patient and a procedure (or two of either) side by side.**
  - **Today:** one page fills the content area. Comparing a patient with one of their procedures means going back and forth, though pins in the side nav make that quicker.
  - **Instead:** a half-view button on the side nav's patient and procedure items (the opened and pinned ones) opens that page in a second pane beside the current one. Each pane is a page in its own right with its own Back, and either can be closed to return to one full page.
  - **Layout:** each pane gets half the content width, and uses the pages' narrow layouts (`NARROW_WINDOW_QUERY` in `Page.tsx` today goes by the window, so the panes need to go by their own width instead). Keep both panes in the URL, so a reload or a shared link opens the same pair.
  - **Tests and screenshots:** tests for opening, swapping and closing a pane, and a `tools/ui-parity/screens.ts` screen of a patient beside a procedure.

## Backend

- **Decide how Domain entities are mapped to API responses, then apply it everywhere.**
  - **Today:** handlers map by hand, in three styles:
    - inline `new XResponse(...)` in each handler (Patients, Auth);
    - shared static mapper classes (`ProcedureMapper`, `StageDefinitionMapper`, `StageTemplateMapper` in the Procedures area);
    - helper methods in a few handlers.
  - **Options:**
    - **A mapping library,** such as Mapster (free) or AutoMapper (commercial licence from v15).
    - **Custom mappers everywhere:** one static mapper class per area, as Procedures does, and no library. Explicit and easy to test, so this is my recommendation. A library adds little where the responses compute things (Procedures' `CanEdit`, `IsOverdue`, `AllowedStatusChanges`), and those mappings would stay hand-written anyway.
  - **Either way:**
    - write it down in `AGENTS.md` (the "Mapping" section, which says there's no mapping library on `main`);
    - move the Patients and Auth handlers onto it;
    - add tests for each mapper.
