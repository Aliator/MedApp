import createClient from 'openapi-fetch';
import {SSR_KEY_HEADER, ssrRequestContext} from '../ssr/requestContext';
import {ApiError, ApiValidationError, parseValidationErrors, problemTitle} from './errors';
import type {components, paths} from './schema';

/** Where requests start from outside a browser, until a page request's context says where the API is. */
const SERVER_PLACEHOLDER_ORIGIN = 'http://medapp.invalid';

/**
 * Typed client for MedApp.API. Paths and DTOs come from the API's Swagger document (`npm run gen:api`).
 * Requests are same-origin (`/api/...`): the Vite dev server proxies them to the API, and in production the API
 * serves this app. The HttpOnly session cookie goes with every request.
 *
 * Rendering a page on the server (`src/ssr`), requests go straight to the API instead, with the page request's
 * cookie, so the page is rendered as the signed-in user sees it.
 */
export const api = createClient<paths>({
  // Absolute, because fetch's Request needs one outside the browser (unit tests); it is still this app's origin.
  baseUrl: typeof window === 'undefined' ? SERVER_PLACEHOLDER_ORIGIN : window.location.origin,
  credentials: 'include',
  // Looked up per request, so tests can stub the global fetch.
  fetch: (request) => globalThis.fetch(request),
});

api.use({
  onRequest({request}) {
    const context = ssrRequestContext();
    if (!context) return request;
    const url = new URL(request.url);
    const headers = new Headers(request.headers);
    if (context.cookie) headers.set('Cookie', context.cookie);
    if (context.ssrKey) headers.set(SSR_KEY_HEADER, context.ssrKey);
    return new Request(new URL(url.pathname + url.search, context.apiUrl), {
      method: request.method,
      headers,
      body: request.body,
      // Node needs this to send a body it reads as it goes.
      ...(request.body ? {duplex: 'half'} : {}),
    } as RequestInit);
  },
});

export type Schemas = components['schemas'];

/** Returns the response body, or throws {@link ApiValidationError} for a 400 and {@link ApiError} otherwise. */
export async function unwrap<T>(request: Promise<{data?: T; error?: unknown; response: Response}>): Promise<T> {
  const {data, error, response} = await request;
  if (response.ok) return data as T;
  if (response.status === 400) throw new ApiValidationError(parseValidationErrors(error));
  throw new ApiError(response.status, undefined, problemTitle(error));
}
