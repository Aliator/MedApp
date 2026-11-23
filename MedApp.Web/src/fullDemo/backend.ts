/**
 * The full demo's backend (docs/full-browser-demo-plan.md): MedApp.Browser's `backend.js`, published beside the app
 * (under `/MedApp/full/`, with its `_framework/` and the demo database), loaded by URL rather than bundled, as it
 * loads the .NET runtime from beside itself.
 */

/** One answer from the backend, as the API would send it. */
export interface BackendResponse {
  status: number;
  headers: Record<string, string>;
  /** Text and JSON bodies. */
  body?: string | null;
  /** Any other body (a file), base64. */
  bodyBase64?: string | null;
}

/** How far the start has got: the runtime's files by count and bytes read, the database by bytes downloaded. */
export interface BackendProgress {
  backend: {loaded: number; total: number; bytes?: number; cached?: number};
  database: {loaded: number; total: number};
}

/** One of the people in the demo database (`db-version.json`'s `accounts`). */
export interface DemoAccount {
  username: string;
  fullName: string;
  jobTitle: string;
  roles: string[];
  /** Their teams, as "Dermatology" or "Dermatology (clinic admin)". */
  teams: string[];
}

/** The published database's version file (`db-version.json`). */
export interface DatabaseVersion {
  builtOn: string;
  seedVersion: number;
  sha256: string;
  bytes: number;
  compressedBytes: number;
  /** The seed's records the walkthrough opens, by name (`relay`, `labCaseRelay`, …). */
  tour: Record<string, string>;
  accounts: DemoAccount[];
}

export interface BackendStart {
  version: DatabaseVersion | null;
  source: 'saved' | 'downloaded' | 'empty';
  timings: Record<string, unknown>;
}

/** What `backend.js` exports. */
export interface BackendModule {
  startBackend(options: {onProgress?: (progress: BackendProgress) => void}): Promise<BackendStart>;
  fetchApi(method: string, url: string, headers: Record<string, string>, body?: Uint8Array | string): Promise<BackendResponse>;
  flush(): Promise<void>;
  forgetSavedDatabase(): Promise<void>;
}

/** Where the backend's files are: the site's base (`/MedApp/full/`). */
export const backendBase = () => import.meta.env.BASE_URL;

let loading: Promise<BackendModule> | null = null;

/** Loads `backend.js` (once). */
export function backendModule(): Promise<BackendModule> {
  loading ??= import(/* @vite-ignore */ `${backendBase()}backend.js`) as Promise<BackendModule>;
  return loading;
}

/** Tests stand in for `backend.js`. */
export function setBackendModuleForTests(module: BackendModule | null): void {
  loading = module ? Promise.resolve(module) : null;
}

/** The published database's version (`db-version.json`), read afresh, or null when it can't be read (offline). */
export async function publishedVersion(): Promise<DatabaseVersion | null> {
  try {
    const response = await fetch(`${backendBase()}db-version.json`, {cache: 'no-cache'});
    return response.ok ? ((await response.json()) as DatabaseVersion) : null;
  } catch {
    return null;
  }
}

/** The runtime's size (`runtime-size.json`, written by the build): its files as read, and compressed. */
export interface RuntimeSize {
  bytes: number;
  compressedBytes: number;
}

export async function runtimeSize(): Promise<RuntimeSize | null> {
  try {
    const response = await fetch(`${backendBase()}runtime-size.json`);
    return response.ok ? ((await response.json()) as RuntimeSize) : null;
  } catch {
    return null;
  }
}
