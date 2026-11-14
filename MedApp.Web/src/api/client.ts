import createClient from 'openapi-fetch';
import {ApiError, ApiValidationError, parseValidationErrors} from './errors';
import type {components, paths} from './schema';

/**
 * Typed client for MedApp.API. Paths and DTOs come from the API's Swagger document (`npm run gen:api`).
 * Requests are same-origin (`/api/...`): the Vite dev server proxies them to the API, and in production the API
 * serves this app. The HttpOnly session cookie goes with every request.
 */
export const api = createClient<paths>({
  // Absolute, because fetch's Request needs one outside the browser (unit tests); it is still this app's origin.
  baseUrl: window.location.origin,
  credentials: 'include',
  // Looked up per request, so tests can stub the global fetch.
  fetch: (request) => globalThis.fetch(request),
});

export type Schemas = components['schemas'];

/** Returns the response body, or throws {@link ApiValidationError} for a 400 and {@link ApiError} otherwise. */
export async function unwrap<T>(request: Promise<{data?: T; error?: unknown; response: Response}>): Promise<T> {
  const {data, error, response} = await request;
  if (response.ok) return data as T;
  if (response.status === 400) throw new ApiValidationError(parseValidationErrors(error));
  throw new ApiError(response.status);
}
