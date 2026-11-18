/** The MDT page's place in the address: the specialty and the meeting chosen, so a list is a link. */
export interface MdtSearch {
  specialty?: string;
  meeting?: string;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const id = (value: unknown): string | undefined =>
  typeof value === 'string' && GUID.test(value.trim()) ? value.trim() : undefined;

/** What's in the address bar, dropping anything that isn't an ID, so a bad link shows the default. */
export function parseMdtSearch(raw: Record<string, unknown>): MdtSearch {
  const search: MdtSearch = {specialty: id(raw.specialty), meeting: id(raw.meeting)};
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as MdtSearch;
}
