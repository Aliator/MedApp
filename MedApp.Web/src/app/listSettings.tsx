import {createContext, useCallback, useContext, useMemo, type ReactNode} from 'react';
import type {QueryClient} from '@tanstack/react-query';
import {settingsQuery} from '../api/settings';
import {appSettings, defineSetting, useAppSetting} from './settingsStore';

/** The rows a list shows a page (Account Settings, Lists): ten unless chosen otherwise, up to the API's 100. */
export const DEFAULT_PAGE_SIZE = 10;
export const MIN_PAGE_SIZE = 1;
export const MAX_PAGE_SIZE = 100;

/** A stored page size, if it's a whole number the lists can show; otherwise null. */
const parsePageSize = (stored: string | null) => {
  const size = Number(stored);
  return stored && Number.isInteger(size) && size >= MIN_PAGE_SIZE && size <= MAX_PAGE_SIZE ? size : null;
};

const PAGE_SIZE_KEY = defineSetting('medapp.pageSize');
const START_ON_MINE_KEY = defineSetting('medapp.proceduresStartOnMine');

interface ListSettingsContextValue {
  pageSize: number;
  setPageSize: (size: number) => void;
  /** Whether the procedure lists start on "Assigned to me" (their URL can still say otherwise). */
  startsOnMine: boolean;
  setStartsOnMine: (starts: boolean) => void;
}

const ListSettingsContext = createContext<ListSettingsContextValue | null>(null);

/** How the lists show (Account Settings, Lists), kept in this browser and with the account (`settingsStore`). */
export function ListSettingsProvider({children}: {children: ReactNode}) {
  const [storedSize, storeSize] = useAppSetting(PAGE_SIZE_KEY);
  const [storedMine, storeMine] = useAppSetting(START_ON_MINE_KEY);
  const pageSize = parsePageSize(storedSize) ?? DEFAULT_PAGE_SIZE;
  const startsOnMine = storedMine === 'true';

  const setPageSize = useCallback((size: number) => storeSize(String(parsePageSize(String(size)) ?? DEFAULT_PAGE_SIZE)), [storeSize]);
  const setStartsOnMine = useCallback((starts: boolean) => storeMine(String(starts)), [storeMine]);

  const value = useMemo(
    () => ({pageSize, setPageSize, startsOnMine, setStartsOnMine}),
    [pageSize, setPageSize, startsOnMine, setStartsOnMine],
  );
  return <ListSettingsContext.Provider value={value}>{children}</ListSettingsContext.Provider>;
}

const DEFAULTS: ListSettingsContextValue = {
  pageSize: DEFAULT_PAGE_SIZE,
  setPageSize: () => {},
  startsOnMine: false,
  setStartsOnMine: () => {},
};

/** The list settings; outside the provider (tests of one part), the defaults. */
export function useListSettings(): ListSettingsContextValue {
  return useContext(ListSettingsContext) ?? DEFAULTS;
}

/**
 * The list settings outside React, for route loaders: on the server, the signed-in account's (the page is rendered
 * with them, see `ServerSettingsContext`), in the browser, this browser's.
 */
export function listSettingsFor(queryClient: QueryClient): {pageSize: number; startsOnMine: boolean} {
  const read = (key: string) => {
    if (typeof window !== 'undefined') return appSettings.get(key);
    const value = queryClient.getQueryData(settingsQuery.queryKey)?.[key];
    return typeof value === 'string' ? value : null;
  };
  return {pageSize: parsePageSize(read(PAGE_SIZE_KEY)) ?? DEFAULT_PAGE_SIZE, startsOnMine: read(START_ON_MINE_KEY) === 'true'};
}
