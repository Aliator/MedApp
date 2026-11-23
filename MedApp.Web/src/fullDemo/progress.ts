import type {StartupState} from './startup';

/** What the progress bar says, and how far along it is (0 to 100), or null while it can't tell (indeterminate). */
export interface ProgressWords {
  label: string;
  value: number | null;
}

const MB = 1024 * 1024;

/** Megabytes, to one decimal place under 10 MB: "3.4", "16". */
export function megabytes(bytes: number): string {
  const mb = bytes / MB;
  return mb < 10 ? (Math.round(mb * 10) / 10).toFixed(1) : String(Math.round(mb));
}

/**
 * The start in words: "Getting MedApp ready: 12 of 20 MB" while the backend and the database download (the runtime's
 * bytes read, scaled to its compressed size, about what was sent, plus the database's download), "Getting MedApp
 * ready from this browser" when its files come from the copies kept here, then "Starting…".
 */
export function progressWords(state: StartupState): ProgressWords {
  if (state.phase === 'ready') return {label: 'MedApp is ready', value: 100};
  if (state.phase === 'failed') return {label: 'MedApp couldn’t be got ready', value: null};
  if (state.phase === 'starting') return {label: 'Starting…', value: null};
  const {progress, size} = state;
  if (!progress || progress.backend.total === 0) return {label: 'Getting MedApp ready…', value: null};

  const {backend, database} = progress;
  const files = Math.min(1, backend.loaded / backend.total);
  const runtime = size && size.bytes > 0 && backend.bytes !== undefined ? Math.min(1, backend.bytes / size.bytes) : files;
  const databaseTotal = database.total || 0;
  // The runtime's files kept here, and the database's saved copy (nothing to download).
  const fromHere = (backend.cached ?? 0) > 0 && databaseTotal === 0;

  if (!size || size.compressedBytes === 0 || fromHere) {
    const value = Math.round(100 * runtime);
    return {label: fromHere ? 'Getting MedApp ready from this browser' : `Getting MedApp ready: ${value}%`, value};
  }
  const total = size.compressedBytes + databaseTotal;
  const loaded = runtime * size.compressedBytes + Math.min(database.loaded, databaseTotal);
  return {label: `Getting MedApp ready: ${megabytes(loaded)} of ${megabytes(total)} MB`, value: Math.round((100 * loaded) / total)};
}
