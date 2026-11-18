import {SEED_VERSION, seed} from './seed';
import type {DemoState} from './state';

/**
 * Where the light demo keeps its state: one record in this browser's IndexedDB, read once as the app starts and
 * written after every change. Nothing leaves the browser. Without IndexedDB (a private window that blocks it) the
 * demo still runs, in memory only, and starts afresh on every load.
 */

const DATABASE = 'medapp-light-demo';
const STORE = 'state';
const KEY = 'current';

let state: DemoState | null = null;

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DATABASE, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(STORE);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

async function run<T>(mode: IDBTransactionMode, work: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const database = await open();
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = database.transaction(STORE, mode);
      const request = work(transaction.objectStore(STORE));
      transaction.oncomplete = () => resolve(request.result);
      transaction.onerror = () => reject(transaction.error);
      transaction.onabort = () => reject(transaction.error);
    });
  } finally {
    database.close();
  }
}

async function read(): Promise<DemoState | undefined> {
  try {
    return (await run('readonly', (store) => store.get(KEY))) as DemoState | undefined;
  } catch {
    return undefined;
  }
}

/** Keeps the state in IndexedDB (best effort: without it, changes last until the page reloads). */
export async function save(): Promise<void> {
  if (!state) return;
  const copy = state;
  try {
    await run('readwrite', (store) => store.put(copy, KEY));
  } catch {
    // Kept in memory only.
  }
}

/** Reads the kept state, or seeds it on a first visit (or when the seed's shape has changed since). */
export async function load(): Promise<DemoState> {
  const kept = await read();
  state = kept && kept.version === SEED_VERSION ? kept : seed();
  if (state !== kept) await save();
  return state;
}

/** The state, once loaded. */
export function db(): DemoState {
  if (!state) state = seed();
  return state;
}

/** For tests: starts from a fresh seed, or the given state, without IndexedDB. */
export function replaceState(next: DemoState = seed()): DemoState {
  state = next;
  return state;
}

/** Clears everything and seeds again, keeping who's signed in, then saves it. The page reloads after. */
export async function reset(): Promise<void> {
  const user = state?.user ?? null;
  state = {...seed(), user};
  try {
    await run('readwrite', (store) => store.clear());
  } catch {
    // Nothing kept to clear.
  }
  await save();
}
