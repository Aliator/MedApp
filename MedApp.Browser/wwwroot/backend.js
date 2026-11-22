// The MedApp backend in this page (docs/full-browser-demo-plan.md): the .NET backend, started on the demo database,
// answering /api requests as the API would. The visitor's copy of the database is kept in IndexedDB: saved after each
// request that changed it, and used on the next visit while it's a copy of the published one (db-version.json).
//
//   const { version } = await startBackend({ onProgress });
//   const response = await fetchApi('GET', '/api/auth/whoami', {});
import createDotnetRuntime from './_framework/dotnet.js';

const STORE_NAME = 'medapp-full-demo';
const STORE = 'database';
const SAVED = 'saved';

let backend = null;
let started = null;
let savedVersion = null;
let queue = Promise.resolve();
let saving = null;
let unsaved = false;

/**
 * Downloads the backend and the database (or takes the saved copy), then starts the backend on it.
 * onProgress({ backend: { loaded, total }, database: { loaded, total } }): the backend's files by count, the database's
 * download by bytes. database: false starts on a new, empty database instead, with only the first admin (not saved).
 * Returns { version, source: 'saved' | 'downloaded' | 'empty', timings }.
 */
export function startBackend({ onProgress = () => {}, database = true, base = new URL('./', import.meta.url) } = {}) {
  started ??= start(onProgress, database, base);
  return started;
}

async function start(onProgress, database, base) {
  const t0 = performance.now();
  const progress = { backend: { loaded: 0, total: 0 }, database: { loaded: 0, total: 0 } };
  const report = () => onProgress(structuredClone(progress));
  const timings = {};

  const runtime = (async () => {
    const api = await createDotnetRuntime({
      onDownloadResourceProgress: (loaded, total) => {
        progress.backend = { loaded, total };
        report();
      },
    });
    const exports = (await api.getAssemblyExports(api.getConfig().mainAssemblyName)).MedAppBackend;
    await api.runMain();
    timings.runtimeMs = Math.round(performance.now() - t0);
    return exports;
  })();

  const data = database
    ? loadDatabase(base, (loaded, total) => {
        progress.database = { loaded, total };
        report();
      }).then(result => ((timings.databaseMs = Math.round(performance.now() - t0)), result))
    : Promise.resolve({ bytes: new Uint8Array(), version: null, source: 'empty' });

  const [exports, { bytes, version, source }] = await Promise.all([runtime, data]);
  timings.backend = JSON.parse(await exports.StartAsync(bytes));
  timings.readyMs = Math.round(performance.now() - t0);
  backend = exports;
  savedVersion = database ? version : null;
  return { version, source, timings };
}

/**
 * One request, as fetch would send it to the API: its method, URL (from /api), headers (an object of strings) and body
 * (a string, bytes, or nothing). Returns { status, headers, body (text and JSON), bodyBase64 (anything else) }.
 * Requests are served one at a time, in order; one that changed the database has it saved afterwards.
 */
export function fetchApi(method, url, headers = {}, body = undefined) {
  if (!backend) return Promise.reject(new Error('The backend has not been started (startBackend).'));
  const bytes = typeof body === 'string' ? new TextEncoder().encode(body) : body === undefined || body === null ? new Uint8Array() : new Uint8Array(body);
  return inTurn(async () => {
    const response = JSON.parse(await backend.FetchAsync(method, url, JSON.stringify(headers), bytes));
    if (response.changed && savedVersion) save();
    delete response.changed;
    return response;
  });
}

/** Waits until every change so far is saved in IndexedDB. */
export async function flush() {
  while (saving) await saving;
}

/** Forgets the saved copy: the next start downloads the published database again. */
export async function forgetSavedDatabase() {
  await flush();
  await store('readwrite', s => s.delete(SAVED));
}

function inTurn(work) {
  const run = queue.then(work);
  queue = run.catch(() => {});
  return run;
}

function save() {
  unsaved = true;
  saving ??= (async () => {
    try {
      while (unsaved) {
        unsaved = false;
        // Between requests, never during one: the file may be half written while a request is served.
        const bytes = await inTurn(() => backend.ExportDatabase());
        await writeSaved(savedVersion, bytes);
      }
    } catch (e) {
      console.error('The demo database could not be saved in this browser:', e);
    } finally {
      saving = null;
    }
  })();
}

async function loadDatabase(base, onBytes) {
  let published = null;
  try {
    const response = await fetch(new URL('db-version.json', base), { cache: 'no-cache' });
    if (response.ok) published = await response.json();
  } catch {
    // Offline: the saved copy, if there is one.
  }

  const saved = await readSaved().catch(() => null);
  if (saved && (!published || sameDatabase(saved.version, published))) return { bytes: saved.bytes, version: saved.version, source: 'saved' };
  if (!published) throw new Error("The demo database's version (db-version.json) could not be read.");

  const bytes = await download(new URL('medapp.db.gz', base), published.compressedBytes, onBytes);
  const sha256 = await hash(bytes);
  if (sha256 !== published.sha256) throw new Error('The demo database downloaded is not the published one (its hash differs).');
  await writeSaved(published, bytes).catch(e => console.error('The demo database could not be saved in this browser:', e));
  return { bytes, version: published, source: 'downloaded' };
}

function sameDatabase(a, b) {
  return a.sha256 === b.sha256 && a.builtOn === b.builtOn && a.seedVersion === b.seedVersion;
}

async function download(url, expectedBytes, onBytes) {
  const response = await fetch(url, { cache: 'no-cache' });
  if (!response.ok || !response.body) throw new Error(`The demo database could not be downloaded (${response.status}).`);
  const total = Number(response.headers.get('Content-Length')) || expectedBytes || 0;
  const chunks = [];
  let loaded = 0;
  const reader = response.body.getReader();
  onBytes(0, total);
  for (let read = await reader.read(); !read.done; read = await reader.read()) {
    chunks.push(read.value);
    loaded += read.value.length;
    onBytes(loaded, Math.max(total, loaded));
  }
  const body = await new Blob(chunks).arrayBuffer();
  const isGzip = body.byteLength > 2 && new Uint8Array(body, 0, 2).every((b, i) => b === [0x1f, 0x8b][i]);
  // A server that sends it with Content-Encoding: gzip has had it decompressed already.
  if (!isGzip) return new Uint8Array(body);
  return new Uint8Array(await new Response(new Blob([body]).stream().pipeThrough(new DecompressionStream('gzip'))).arrayBuffer());
}

async function hash(bytes) {
  const digest = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes));
  return Array.from(digest, b => b.toString(16).padStart(2, '0')).join('');
}

function readSaved() {
  return store('readonly', s => s.get(SAVED));
}

function writeSaved(version, bytes) {
  return store('readwrite', s => s.put({ version, bytes, savedAt: new Date().toISOString() }, SAVED));
}

function store(mode, work) {
  return new Promise((resolve, reject) => {
    const open = indexedDB.open(STORE_NAME, 1);
    open.onupgradeneeded = () => open.result.createObjectStore(STORE);
    open.onerror = () => reject(open.error);
    open.onsuccess = () => {
      const db = open.result;
      const transaction = db.transaction(STORE, mode);
      const request = work(transaction.objectStore(STORE));
      transaction.oncomplete = () => {
        db.close();
        resolve(request.result);
      };
      transaction.onerror = transaction.onabort = () => {
        db.close();
        reject(transaction.error);
      };
    };
  });
}
