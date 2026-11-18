import {SPECIMEN_VIEWS, type SpecimenView} from '../../api/specimens';

/**
 * The Lab page's address: its view, the lab it's narrowed to and the page. With no view, it opens on Expected for
 * holders of ProcessSpecimens (the lab's own staff) and To send for everyone else (the clinic, who send them), so
 * the Lab item's own link has no search at all.
 */
export interface LabSearch {
  view?: SpecimenView;
  /** A lab's ID. */
  lab?: string;
  page?: number;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const text = (value: unknown) => (typeof value === 'string' || typeof value === 'number' ? String(value).trim() || undefined : undefined);

/** The search from the address bar, dropping anything that isn't valid, so a bad link shows the default view. */
export function parseLabSearch(raw: Record<string, unknown>): LabSearch {
  const view = text(raw.view)?.toLowerCase();
  const page = Number(text(raw.page));
  const lab = text(raw.lab);
  const search: LabSearch = {
    view: SPECIMEN_VIEWS.find((v) => v.toLowerCase() === view),
    lab: lab && GUID.test(lab) ? lab.toLowerCase() : undefined,
    page: Number.isInteger(page) && page > 1 ? page : undefined,
  };
  return Object.fromEntries(Object.entries(search).filter(([, v]) => v !== undefined)) as LabSearch;
}

/** The view a search shows: its own, or the default for the person (Expected for the lab's staff, To send otherwise). */
export const labViewOf = (search: LabSearch, canProcess: boolean): SpecimenView => search.view ?? (canProcess ? 'Expected' : 'ToSend');
