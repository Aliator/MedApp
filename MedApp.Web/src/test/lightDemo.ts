import type {CurrentUser} from '../auth/auth';
import {ADMIN, type DemoPerson} from '../demo/accounts';
import {answer} from '../demo/api/http';
import {registerHandlers} from '../demo/handlers';
import {seed} from '../demo/seed';
import {replaceState} from '../demo/store';
import {mockFetch} from './render';

/**
 * Starts the light demo's stand-in API for a test, from a fresh seed, signed in as `person` (null: signed out), and
 * answers the app's requests from it (the global fetch), as the demo build does.
 */
export function startLightDemo(person: DemoPerson | null = ADMIN) {
  registerHandlers();
  const state = replaceState(seed());
  state.user = person?.username ?? null;
  const fetch = mockFetch((request) => answer(request, async () => {}));
  return {state, fetch};
}

/** A demo account as whoami gives it, for `renderWithApp`'s `user`. */
export const asCurrentUser = (person: DemoPerson): CurrentUser => ({
  isAuthenticated: true,
  username: person.username,
  roles: person.roles,
  permissions: person.permissions,
  mustChangePassword: false,
  mustSetUpTwoFactor: false,
});

/** Calls the stand-in API directly, as the app's client would, and reads the JSON answer. */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- each test reads the answer it expects
export async function call(method: string, path: string, body?: unknown): Promise<{status: number; body: any}> {
  const response = await answer(
    new Request(`http://localhost${path}`, {
      method,
      ...(body === undefined ? {} : {body: JSON.stringify(body), headers: {'Content-Type': 'application/json'}}),
    }),
    async () => {},
  );
  const text = await response.text();
  return {status: response.status, body: text ? JSON.parse(text) : undefined};
}
