import {QueryClient} from '@tanstack/react-query';

/**
 * The app's query cache. A page rendered on the server arrives with its data, so then (`isSsr`) data is taken as
 * fresh for a short while, rather than fetched again the moment the browser takes the page over.
 */
export function createQueryClient({isSsr = false}: {isSsr?: boolean} = {}) {
  return new QueryClient({
    defaultOptions: {queries: {retry: 1, refetchOnWindowFocus: false, ...(isSsr ? {staleTime: 30_000} : {})}},
  });
}
