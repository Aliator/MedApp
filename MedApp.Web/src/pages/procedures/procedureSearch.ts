/**
 * The procedure page's search: a request to delete one of its notes, opened over the page for an admin (their "Note
 * deletion requested" notification links to `/procedures/<id>?deletionRequest=<request id>`). Anything that isn't an
 * ID is dropped, so a bad link just shows the procedure.
 */
export interface ProcedureSearch {
  deletionRequest?: string;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function parseProcedureSearch(raw: Record<string, unknown>): ProcedureSearch {
  const value = typeof raw.deletionRequest === 'string' ? raw.deletionRequest.trim() : '';
  // In lower case, as the API returns IDs, so it shares the cache entry the dialog's decision updates.
  return GUID.test(value) ? {deletionRequest: value.toLowerCase()} : {};
}
