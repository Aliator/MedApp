/** Stands in for this app's origin when checking where a path leads: only its own paths keep it. */
const BASE = 'https://medapp.invalid';

/** Control characters (tabs, newlines and the like), which browsers drop from addresses, so `/\t/x` could become `//x`. */
// eslint-disable-next-line no-control-regex -- matching them is the point.
const CONTROL =/[\u0000-\u001f\u007f]/;

/**
 * Where to go after signing in (`/login?redirect=…`), if it's safe: a path in this app, starting with one `/` (never
 * `//` or `/\`, which browsers read as another site), with no backslashes or control characters, and still on this
 * origin once parsed. Only its path and query are kept. Anything else is `undefined`, so sign-in goes to the start
 * page as usual: a link can't send someone off to another site after they sign in.
 */
export function safeRedirectPath(value: unknown): string | undefined {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return undefined;
  if (value.includes('\\') || CONTROL.test(value)) return undefined;
  // A path that starts with one "/" can't throw or leave the base's origin; the check is a second line, in case.
  const url = new URL(value, BASE);
  return url.origin === BASE ? url.pathname + url.search : undefined;
}
