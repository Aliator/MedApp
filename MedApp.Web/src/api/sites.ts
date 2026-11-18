import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {closedDayKeys} from './closedDays';

/**
 * Sites (docs/centre-phases-3-5-plan.md, decisions 118 and 121): the places the centre works from, each with its rooms.
 * Admins manage them; staff read them. One that has been used is archived, not deleted (it can't be chosen for
 * anything new, and what names it keeps it), and can be restored. There's always at least one site that isn't
 * archived: every database starts with "Main site", which can be renamed.
 */
export type Site = Schemas['SiteResponse'];
export type Room = Schemas['RoomResponse'];
/** A site's details, for adding one or replacing an existing one's: an address, postcode or phone left out is removed. */
export type SiteInput = Schemas['SiteRequest'];
/** A site's opening hours today: its own (`hasOwnHours`), or the default (Monday to Friday, 08:00 to 18:00). */
export type SiteHours = Schemas['SiteHoursResponse'];
/** One day a site is open, to save: the day by name ("Tuesday") and "08:00" clock times. */
export type SiteHoursDayInput = Schemas['SiteHoursDayRequest'];

/** Every site and room query is under ['sites'], so one change refreshes them all. */
export const siteKeys = {
  all: ['sites'] as const,
  list: (includeArchived: boolean) => ['sites', 'list', includeArchived] as const,
  rooms: (siteId: string | undefined, includeArchived: boolean) => ['sites', 'rooms', siteId ?? null, includeArchived] as const,
  hours: (siteId: string) => ['sites', 'hours', siteId] as const,
};

/** Every site by name, each with its rooms by name; archived sites and rooms only with `includeArchived`. */
export function sitesQuery({includeArchived = false}: {includeArchived?: boolean} = {}) {
  return queryOptions({
    queryKey: siteKeys.list(includeArchived),
    queryFn: () => unwrap(api.GET('/api/sites', {params: {query: {includeArchived}}})),
  });
}

/** Rooms by site, then name: one site's (`siteId`) or every site's; archived ones only with `includeArchived`. */
export function roomsQuery({siteId, includeArchived = false}: {siteId?: string; includeArchived?: boolean} = {}) {
  return queryOptions({
    queryKey: siteKeys.rooms(siteId, includeArchived),
    queryFn: () => unwrap(api.GET('/api/rooms', {params: {query: {siteId, includeArchived}}})),
  });
}

/** A site's opening hours today. */
export function siteHoursQuery(siteId: string) {
  return queryOptions({
    queryKey: siteKeys.hours(siteId),
    queryFn: () => unwrap(api.GET('/api/sites/{id}/hours', {params: {path: {id: siteId}}})),
  });
}

/** A site's address on one line, its postcode last: "1 Northgate Road, Leeds, ZZ99 3AA"; empty for none. */
export const addressLine = (site: Pick<Site, 'addressLines' | 'postcode'>) =>
  [...(site.addressLines ?? []), site.postcode ?? ''].map((part) => part.trim()).filter(Boolean).join(', ');

/** Refreshes every site and room list; with `closedDays`, the closed days too, which carry their site's name. */
function useRefreshSites({closedDays = false}: {closedDays?: boolean} = {}) {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: siteKeys.all}),
      closedDays ? queryClient.invalidateQueries({queryKey: closedDayKeys.all}) : undefined,
    ]);
  };
}

/**
 * Adds a site (no `id`) or replaces one's details (admins). A name taken by another site is a 409; the API's 400 names
 * `Name`, `AddressLines`, `Postcode` or `Phone`. Saving refreshes the closed days too, as each shows its site's name.
 */
export function useSaveSite() {
  const onSuccess = useRefreshSites({closedDays: true});
  return useMutation({
    mutationFn: ({id, ...body}: SiteInput & {id?: string}) =>
      id ? unwrap(api.PUT('/api/sites/{id}', {params: {path: {id}}, body})) : unwrap(api.POST('/api/sites', {body})),
    onSuccess,
  });
}

/**
 * Archive, restore and delete for sites (admins). Archiving is a 409 while the site has rooms that aren't archived,
 * or when it's the last open site; deleting is a 409 once it has rooms or closed days (its `canBeDeleted` says so).
 */
export function useSiteActions() {
  const onSuccess = useRefreshSites();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/sites/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/sites/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/sites/{id}', path(id))), onSuccess}),
  };
}

/**
 * Adds a room to a site (no `id`: the site must not be archived) or renames one (admins); a room never moves to
 * another site. A name the site already has is a 409.
 */
export function useSaveRoom() {
  const onSuccess = useRefreshSites();
  return useMutation({
    mutationFn: ({id, siteId, name}: {id?: string; siteId: string; name: string}) =>
      id ? unwrap(api.PUT('/api/rooms/{id}', {params: {path: {id}}, body: {name}})) : unwrap(api.POST('/api/rooms', {body: {siteId, name}})),
    onSuccess,
  });
}

/**
 * Archive, restore and delete for rooms (admins). Restoring is a 409 while its site is archived; deleting is a 409
 * once anything has used it (its `canBeDeleted` says so).
 */
export function useRoomActions() {
  const onSuccess = useRefreshSites();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/rooms/{id}/archive', path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST('/api/rooms/{id}/restore', path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE('/api/rooms/{id}', path(id))), onSuccess}),
  };
}

/**
 * Sets a site's opening hours from today (admins; none: back on the default). A 409 when the site is archived, or while
 * anyone's working hours there would fall outside them (it names who). Calendars refresh too: their days follow it.
 */
export function useSaveSiteHours() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({siteId, days}: {siteId: string; days: SiteHoursDayInput[]}) =>
      unwrap(api.PUT('/api/sites/{id}/hours', {params: {path: {id: siteId}}, body: {days}})),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({queryKey: siteKeys.all}),
        queryClient.invalidateQueries({queryKey: ['calendar']}),
      ]);
    },
  });
}
