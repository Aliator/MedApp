/**
 * What an API call made while rendering a page on the server needs from the page request it's for: where the API
 * is, and the signed-in user's session cookie (which the browser sends with the page request, not to the API).
 * The Node server (`server/server.mjs`) sets it for each request; in the browser there's none.
 */
export interface SsrRequestContext {
  /** The API's own address as the Node server reaches it, e.g. https://localhost:7038. */
  apiUrl: string;
  /** The page request's Cookie header, passed on to the API. */
  cookie?: string;
  /** The key the API gave the Node server it started, which lets its calls skip the https redirect locally. */
  ssrKey?: string;
}

let provider: (() => SsrRequestContext | undefined) | undefined;

/** Called by the server entry once, with how to find the current request's context (an AsyncLocalStorage). */
export function setSsrRequestContextProvider(next: () => SsrRequestContext | undefined) {
  provider = next;
}

/** The context of the page request being rendered on the server; undefined in the browser. */
export const ssrRequestContext = (): SsrRequestContext | undefined => provider?.();

/** The header that carries the Node server's key to the API. */
export const SSR_KEY_HEADER = 'X-MedApp-Ssr-Key';
