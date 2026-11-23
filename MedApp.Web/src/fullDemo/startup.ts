import {backendModule, publishedVersion, runtimeSize, type BackendProgress, type BackendStart, type DatabaseVersion, type RuntimeSize} from './backend';
import {rememberedUser, restoreSession} from './session';

/**
 * Where the full demo's start has got, for the login page and the start screen: downloading (with its progress),
 * starting (everything downloaded, the backend starting), ready, or failed. `version` is the published database's
 * (`db-version.json`), read at once for the login page's accounts, then the one the backend started on.
 */
export type StartupState =
  | {phase: 'downloading' | 'starting'; progress: BackendProgress | null; version: DatabaseVersion | null; size: RuntimeSize | null}
  | {phase: 'ready'; progress: BackendProgress | null; version: DatabaseVersion | null; size: RuntimeSize | null; start: BackendStart; readyMs: number}
  | {phase: 'failed'; progress: BackendProgress | null; version: DatabaseVersion | null; size: RuntimeSize | null; error: string};

const INITIAL: StartupState = {phase: 'downloading', progress: null, version: null, size: null};

let state: StartupState = INITIAL;
let started: Promise<void> | null = null;
let ready: Promise<void> | null = null;
const listeners = new Set<() => void>();
/** Progress comes many times a second: it's passed on at most this often. */
const PROGRESS_EVERY_MS = 100;
let progressTimer: ReturnType<typeof setTimeout> | null = null;
let latestProgress: BackendProgress | null = null;

function set(next: StartupState) {
  state = next;
  listeners.forEach((listener) => listener());
}

export const startupState = () => state;

export function subscribeStartup(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Whether everything's downloaded (the runtime's files and the database), so what's left is the backend's start. */
export function isDownloaded(progress: BackendProgress | null): boolean {
  if (!progress) return false;
  const {backend, database} = progress;
  return backend.total > 0 && backend.loaded >= backend.total && (database.total === 0 || database.loaded >= database.total);
}

function onProgress(progress: BackendProgress) {
  latestProgress = progress;
  if (progressTimer) return;
  progressTimer = setTimeout(() => {
    progressTimer = null;
    if (state.phase !== 'downloading' && state.phase !== 'starting') return;
    set({...state, phase: isDownloaded(latestProgress) ? 'starting' : 'downloading', progress: latestProgress});
  }, PROGRESS_EVERY_MS);
}

const describe = (error: unknown) => (error instanceof Error ? error.message : String(error));

/**
 * Starts the full demo (once): reads the published version (for the accounts) and the runtime's size (for the
 * progress), downloads and starts the backend, then, after a reload, signs in again as whoever was signed in.
 */
export function startFullDemo(): Promise<void> {
  started ??= (async () => {
    const t0 = performance.now();
    void publishedVersion().then((version) => version && state.phase !== 'ready' && set({...state, version: state.version ?? version}));
    void runtimeSize().then((size) => set({...state, size}));
    try {
      const backend = await backendModule();
      const start = await backend.startBackend({onProgress});
      if (rememberedUser()) await restoreSession(backend);
      set({...state, phase: 'ready', progress: latestProgress, version: start.version ?? state.version, start, readyMs: Math.round(performance.now() - t0)});
    } catch (error) {
      console.error('The full demo could not start:', error);
      set({...state, phase: 'failed', progress: latestProgress, error: describe(error)});
    }
  })();
  return started;
}

/** Resolves once the backend is ready; rejects if it failed to start. */
export function whenReady(): Promise<void> {
  ready ??= new Promise<void>((resolve, reject) => {
    const check = () => {
      if (state.phase === 'ready') resolve();
      else if (state.phase === 'failed') reject(new Error(state.error));
      else return false;
      unsubscribe();
      return true;
    };
    const unsubscribe = subscribeStartup(() => void check());
    check();
  });
  return ready;
}

export function resetStartupForTests(): void {
  state = INITIAL;
  started = null;
  ready = null;
  latestProgress = null;
  if (progressTimer) clearTimeout(progressTimer);
  progressTimer = null;
  listeners.clear();
}

export function setStartupStateForTests(next: StartupState): void {
  set(next);
}
