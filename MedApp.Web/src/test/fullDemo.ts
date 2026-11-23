import {vi} from 'vitest';
import {setBackendModuleForTests, type BackendModule, type BackendResponse, type DatabaseVersion} from '../fullDemo/backend';
import {resetStartupForTests, setStartupStateForTests} from '../fullDemo/startup';

/** A published database's version file, as `tools/MedApp.BrowserDb` writes it (synthetic accounts, made-up IDs). */
export const FULL_DEMO_VERSION: DatabaseVersion = {
  builtOn: '2026-11-23',
  seedVersion: 1,
  sha256: 'abc123',
  bytes: 2_400_000,
  compressedBytes: 300_000,
  tour: {
    relay: '245a9076-edc8-41b3-b1b6-30991befb8b5',
    labCaseRelay: '4ecc8387-052e-4b01-a110-6ecda1643149',
    onHold: 'ee570a89-c782-4195-b8ee-d12cba6f470f',
    unassigned: '3bdc67d8-4394-42d4-97fd-8ea6f45e0a37',
  },
  accounts: [
    {username: 'admin', fullName: 'Centre administrator', jobTitle: 'Centre administrator', roles: ['Admin'], teams: []},
    {username: 'n.thornbury', fullName: 'Nadia Thornbury', jobTitle: 'Consultant dermatologist', roles: ['Clinician'], teams: ['Dermatology (clinic admin)']},
    {username: 'r.aldous', fullName: 'Ravi Aldous', jobTitle: 'Consultant histopathologist', roles: ['Lab', 'Pathologist'], teams: ['Lab']},
    {username: 'j.whitlock', fullName: 'Jo Whitlock', jobTitle: 'Service manager', roles: ['Manager'], teams: ['Dermatology', 'Eye clinic']},
    {username: 's.varma', fullName: 'Saira Varma', jobTitle: 'Consultant ophthalmologist', roles: ['Clinician'], teams: ['Eye clinic (clinic admin)']},
  ],
};

export const answer = (status: number, body?: unknown, headers: Record<string, string> = {'Content-Type': 'application/json'}): BackendResponse => ({
  status,
  headers,
  body: body === undefined ? '' : typeof body === 'string' ? body : JSON.stringify(body),
});

/**
 * A stand-in for `backend.js`: `fetchApi` answers with `handle` (by default a 200 with no body), the start resolves
 * with `FULL_DEMO_VERSION` (or what `start` returns). Installed for `backendModule()`.
 */
export function fakeBackend(handle: (method: string, url: string, headers: Record<string, string>, body?: Uint8Array | string) => BackendResponse | Promise<BackendResponse> = () => answer(200)) {
  const module = {
    startBackend: vi.fn(async ({onProgress}: {onProgress?: (p: never) => void}) => {
      void onProgress;
      return {version: FULL_DEMO_VERSION, source: 'downloaded' as const, timings: {}};
    }),
    fetchApi: vi.fn(async (method: string, url: string, headers: Record<string, string>, body?: Uint8Array | string) => handle(method, url, headers, body)),
    flush: vi.fn(async () => {}),
    forgetSavedDatabase: vi.fn(async () => {}),
  } satisfies BackendModule;
  setBackendModuleForTests(module as unknown as BackendModule);
  return module;
}

/** The full demo started (ready, on `FULL_DEMO_VERSION`), as the login page and the walkthrough see it. */
export function fullDemoReady(version: DatabaseVersion = FULL_DEMO_VERSION) {
  setStartupStateForTests({phase: 'ready', progress: null, version, size: null, start: {version, source: 'downloaded', timings: {}}, readyMs: 10});
}

/** Back to before the start, with no stand-in backend and nobody remembered. */
export function resetFullDemoForTests() {
  resetStartupForTests();
  setBackendModuleForTests(null);
  sessionStorage.clear();
}
