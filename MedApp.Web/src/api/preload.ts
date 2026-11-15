import type {FetchQueryOptions, QueryClient} from '@tanstack/react-query';

// Any query's options, whatever its data and key (each is fetched on its own terms).
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyQuery = FetchQueryOptions<any, any, any, any>;

/**
 * A route loader's data. Rendering on the server, the page waits for it, so it arrives with its data (a failure
 * leaves the query in error, for the page to show as it does any error). In the browser it only starts fetching:
 * the page opens at once and shows it loading, as it always has.
 */
export async function preload(queryClient: QueryClient, ...queries: AnyQuery[]) {
  if (typeof window === 'undefined') {
    await Promise.all(queries.map((query) => queryClient.fetchQuery(query).catch(() => undefined)));
  } else {
    for (const query of queries) void queryClient.prefetchQuery(query);
  }
}
