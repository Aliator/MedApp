# MedApp: to do

Smaller improvements to pick up later, outside the phased plans (`docs/procedures-ui-plan.md`). Move an item into a plan or a branch when it's started, and delete it here when it's merged.

## Users

- **Combine Assign roles and Revoke roles into one "Edit roles" action.**
  - **Today:** each row on the Users page (`MedApp.Web/src/pages/users/UsersPage.tsx`) has two buttons, **Assign roles** and **Revoke roles**. Each opens its own `SelectionDialog`: one lists the roles the user doesn't have, the other the roles they do. Changing someone from User to Admin, or both ways, takes two dialogs.
  - **Instead:** one **Edit roles** button opening one dialog that lists every role, with the user's current roles ticked. Saving applies the difference: it assigns the newly ticked roles and revokes the unticked ones. Keep the rule that admins can't change their own roles (the button stays disabled on your own row).
  - **API:** either keep the two existing calls (`useAssignRoles` and `useRevokeRoles`, sent one after the other), or add one endpoint that sets a user's roles to an exact list. One endpoint is better: it changes the roles in one step, so a failure can't leave them half-changed.
  - **Tests and screenshots:** update `UsersPage.test.tsx`, and replace the `users-roles` screen in `tools/ui-parity/screens.ts`.
