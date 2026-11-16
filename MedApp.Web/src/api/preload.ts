import type {FetchInfiniteQueryOptions, FetchQueryOptions, QueryClient} from '@tanstack/react-query';

// Any query's options, whatever its data and key (each is fetched on its own terms).
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyQuery = FetchQueryOptions<any, any, any, any>;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type AnyInfiniteQuery = FetchInfiniteQueryOptions<any, any, any, any, any>;

/** A query fetched a page at a time (`infiniteQueryOptions`), of which the first page is preloaded. */
const isInfinite = (query: AnyQuery | AnyInfiniteQuery): query is AnyInfiniteQuery => 'initialPageParam' in query;

/**
 * A route loader's data. Rendering on the server, the page waits for it, so it arrives with its data (a failure
 * leaves the query in error, for the page to show as it does any error). In the browser it only starts fetching:
 * the page opens at once and shows it loading, as it always has. A paged feed (`infiniteQueryOptions`) gets its
 * first page.
 */
export async function preload(queryClient: QueryClient, ...queries: (AnyQuery | AnyInfiniteQuery)[]) {
  if (typeof window === 'undefined') {
    await Promise.all(
      queries.map((query) =>
        (isInfinite(query) ? queryClient.fetchInfiniteQuery(query) : queryClient.fetchQuery(query)).catch(() => undefined),
      ),
    );
  } else {
    for (const query of queries) {
      if (isInfinite(query)) void queryClient.prefetchInfiniteQuery(query);
      else void queryClient.prefetchQuery(query);
    }
  }
}
