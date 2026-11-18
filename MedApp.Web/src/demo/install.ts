import {answer} from './api/http';
import {registerHandlers} from './handlers';
import {load, save} from './store';

/** Whether a request is for the API (`/api/...` on this site), which the light demo answers in the browser. */
export function isApiRequest(url: string): boolean {
  try {
    const parsed = new URL(url, window.location.href);
    return parsed.origin === window.location.origin && parsed.pathname.startsWith('/api/');
  } catch {
    return false;
  }
}

/**
 * Starts the light demo (`entry-client`, in the demo build only): loads or seeds its state, then answers every request
 * for `/api/...` from the stand-in's handlers instead of the network. Anything else (the app's own files) still goes
 * to the network.
 */
export async function installLightDemo(): Promise<void> {
  registerHandlers();
  await load();
  const networkFetch = window.fetch.bind(window);
  window.fetch = (input: RequestInfo | URL, init?: RequestInit) => {
    const url = input instanceof Request ? input.url : input.toString();
    if (!isApiRequest(url)) return networkFetch(input, init);
    return answer(new Request(input, init), save);
  };
}
