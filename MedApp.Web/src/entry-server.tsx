import {AsyncLocalStorage} from 'node:async_hooks';
import {createRequestHandler, defaultStreamHandler} from '@tanstack/react-router/ssr/server';
import type {AnyRouter} from '@tanstack/react-router';
import {createSsrRouter} from './router';
import {setSsrRequestContextProvider, type SsrRequestContext} from './ssr/requestContext';

/** The build's assets for the page's head and body: its stylesheets and scripts (see `server/server.mjs`). */
export type RouterManifest = Parameters<typeof createRequestHandler<AnyRouter>>[0]['getRouterManifest'] extends
  | (() => infer M)
  | undefined
  ? Awaited<M>
  : never;

const requests = new AsyncLocalStorage<SsrRequestContext>();
setSsrRequestContextProvider(() => requests.getStore());

/**
 * Renders a page on the server: runs the route's guards and loaders (fetching from the API as the signed-in user,
 * with the request's cookie), then streams the page's HTML with its data in it, for the browser to take over
 * (`entry-client`). A redirect (e.g. to sign in) comes back as one.
 */
export function render(request: Request, {manifest, context}: {manifest: RouterManifest; context: SsrRequestContext}) {
  return requests.run(context, () =>
    createRequestHandler({request, createRouter: createSsrRouter, getRouterManifest: () => manifest})(defaultStreamHandler),
  );
}
