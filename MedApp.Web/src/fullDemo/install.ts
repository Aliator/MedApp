import {backendModule, type BackendResponse} from './backend';
import {installDownloads} from './downloads';
import {apiPath, encodeBody, headersObject, isApiUrl, problemAnswer, SIGNED_OUT_WHOAMI, toResponse} from './http';
import {noteAnswer, rememberedUser} from './session';
import {showStartScreen} from './startScreen';
import {startFullDemo, startupState, whenReady} from './startup';
import {installXhr} from './xhr';

const NOT_READY = 'MedApp could not start in this browser. Reload the page to try again.';

/**
 * One request for the API, answered by the backend in the page once it has started. Until then, whoami answers
 * "signed out" at once for a visitor nobody is signed in as (so the login page shows while the backend downloads), and
 * anything else waits for it. Sign-ins and sign-outs are noted (`noteAnswer`), so a reload signs in again.
 */
export async function answerApi(method: string, path: string, headers: Record<string, string>, bytes?: Uint8Array): Promise<BackendResponse> {
  const upper = method.toUpperCase();
  if (startupState().phase !== 'ready') {
    if (upper === 'GET' && path.split('?')[0].toLowerCase() === '/api/auth/whoami' && !rememberedUser()) return SIGNED_OUT_WHOAMI;
    try {
      await whenReady();
    } catch {
      return problemAnswer(503, NOT_READY);
    }
  }
  const backend = await backendModule();
  const answer = await backend.fetchApi(upper, path, headers, bytes);
  const requestText = upper === 'POST' && bytes ? new TextDecoder().decode(bytes) : null;
  noteAnswer(upper, path, answer.status, requestText, answer.body);
  return answer;
}

/** fetch, for a Request to the API. */
export async function answerRequest(request: Request): Promise<Response> {
  const hasBody = request.method !== 'GET' && request.method !== 'HEAD';
  const bytes = hasBody ? new Uint8Array(await request.arrayBuffer()) : undefined;
  return toResponse(await answerApi(request.method, apiPath(request.url), headersObject(request.headers), bytes));
}

/** XMLHttpRequest's body, encoded as fetch would, then answered. */
export async function answerBody(method: string, url: string, headers: Record<string, string>, body: unknown): Promise<BackendResponse> {
  const {bytes, contentType} = await encodeBody(body);
  const all: Record<string, string> = {Host: window.location.host, ...headers};
  if (contentType && !Object.keys(all).some((name) => name.toLowerCase() === 'content-type')) all['Content-Type'] = contentType;
  return answerApi(method, apiPath(url), all, bytes);
}

/**
 * Starts the full demo (`entry-client`, in its build only): every request for this site's `/api/...`, by fetch, by
 * XMLHttpRequest (uploads) and by following a download link, goes to the backend in the page instead of the network,
 * and the backend starts downloading at once. The app renders straight away (the login page shows its progress),
 * except after a reload while signed in, when a start screen shows until the backend has started and signed in again.
 */
export async function installFullDemo(): Promise<void> {
  const networkFetch = window.fetch.bind(window);
  window.fetch = (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input instanceof Request ? input.url : input.toString();
    if (!isApiUrl(url)) return networkFetch(input, init);
    return answerRequest(new Request(input instanceof Request ? input : new URL(url, window.location.href), init));
  };
  installXhr(answerBody);
  installDownloads((path) => answerApi('GET', path, {Host: window.location.host}));
  void startFullDemo();
  if (rememberedUser()) await showStartScreen();
}
