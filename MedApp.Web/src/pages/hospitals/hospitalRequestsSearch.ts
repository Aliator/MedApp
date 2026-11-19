/** The columns Hospital Requests sorts by, as the API names them. */
export const HOSPITAL_REQUEST_SORTS = ['case', 'patient', 'hospital', 'needed', 'registered'] as const;
export type HospitalRequestSort = (typeof HOSPITAL_REQUEST_SORTS)[number];

/**
 * Hospital Requests' address: the hospital it's narrowed to, the page and the sort. The text searched and the priority
 * are never here: the search may be an NHS or hospital number, which stays out of URLs.
 */
export interface HospitalRequestsSearch {
  /** A hospital's ID. */
  hospital?: string;
  page?: number;
  sort?: HospitalRequestSort;
  asc?: boolean;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid. */
export function parseHospitalRequestsSearch(raw: Record<string, unknown>): HospitalRequestsSearch {
  const hospital = text(raw.hospital);
  const page = Number(text(raw.page));
  const sort = text(raw.sort)?.toLowerCase();
  const asc = text(raw.asc)?.toLowerCase();
  const search: HospitalRequestsSearch = {
    hospital: hospital && GUID.test(hospital) ? hospital.toLowerCase() : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
    sort: HOSPITAL_REQUEST_SORTS.find((s) => s === sort),
    asc: asc === 'true' ? true : asc === 'false' ? false : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as HospitalRequestsSearch;
}
