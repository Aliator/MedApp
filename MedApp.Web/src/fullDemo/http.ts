import type {BackendResponse} from './backend';

/** Whether a URL is for the API (`/api/...` on this site), which the full demo's backend answers in the page. */
export function isApiUrl(url: string | URL): boolean {
  try {
    const parsed = new URL(url, window.location.href);
    return parsed.origin === window.location.origin && parsed.pathname.startsWith('/api/');
  } catch {
    return false;
  }
}

/** A URL's path and query, as the backend takes it (`/api/patients/search?LastName=…`). */
export function apiPath(url: string | URL): string {
  const parsed = new URL(url, window.location.href);
  return `${parsed.pathname}${parsed.search}`;
}

/** Headers as the backend takes them: an object of strings, with the page's host. */
export function headersObject(headers: Headers): Record<string, string> {
  const object: Record<string, string> = {Host: window.location.host};
  headers.forEach((value, name) => (object[name] = value));
  return object;
}

/**
 * A body as bytes, with the content type it would be sent with (a form's multipart boundary, a file's type), as fetch
 * and XMLHttpRequest make them: whatever the app sends (text, a file, bytes, a form, search parameters).
 */
export async function encodeBody(body: unknown): Promise<{bytes: Uint8Array | undefined; contentType: string | null}> {
  if (body === undefined || body === null) return {bytes: undefined, contentType: null};
  const request = new Request(window.location.href, {method: 'POST', body: body as BodyInit});
  return {bytes: new Uint8Array(await request.arrayBuffer()), contentType: request.headers.get('Content-Type')};
}

function decodeBase64(base64: string): Uint8Array<ArrayBuffer> {
  const text = atob(base64);
  const bytes = new Uint8Array(text.length);
  for (let i = 0; i < text.length; i++) bytes[i] = text.charCodeAt(i);
  return bytes;
}

/** The answer's body as bytes: a file's (base64) or the text's. */
export function answerBytes(answer: BackendResponse): Uint8Array<ArrayBuffer> {
  if (answer.bodyBase64) return decodeBase64(answer.bodyBase64);
  return new TextEncoder().encode(answer.body ?? '');
}

/** The answer's body as text. */
export const answerText = (answer: BackendResponse): string =>
  answer.bodyBase64 ? new TextDecoder().decode(answerBytes(answer)) : (answer.body ?? '');

/** Statuses whose responses can't have a body (the Response constructor refuses one). */
const NO_BODY = new Set([101, 204, 205, 304]);

/** The answer as fetch's Response. */
export function toResponse(answer: BackendResponse): Response {
  const body = NO_BODY.has(answer.status) ? null : answer.bodyBase64 ? answerBytes(answer) : (answer.body ?? '');
  return new Response(body, {status: answer.status, headers: answer.headers});
}

/** A ProblemDetails answer, for the demo's own refusals (the backend not started). */
export function problemAnswer(status: number, title: string): BackendResponse {
  return {status, headers: {'Content-Type': 'application/problem+json'}, body: JSON.stringify({status, title})};
}

/** What the API answers to whoami while nobody is signed in. */
export const SIGNED_OUT_WHOAMI: BackendResponse = {
  status: 200,
  headers: {'Content-Type': 'application/json; charset=utf-8'},
  body: JSON.stringify({isAuthenticated: false, name: null, roles: [], mustChangePassword: false, mustSetUpTwoFactor: false, permissions: [], accountType: null, teams: []}),
};

/** The file name a download is saved as (its `Content-Disposition`), or null. */
export function downloadName(headers: Record<string, string>): string | null {
  const disposition = Object.entries(headers).find(([name]) => name.toLowerCase() === 'content-disposition')?.[1];
  if (!disposition) return null;
  const encoded = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(disposition);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim().replace(/^"|"$/g, ''));
    } catch {
      // Fall back to the plain name.
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : null;
}

/** A header's value, whatever its name's case. */
export const headerOf = (headers: Record<string, string>, name: string) =>
  Object.entries(headers).find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1] ?? null;
