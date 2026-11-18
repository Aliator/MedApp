import type {QueryClient} from '@tanstack/react-query';
import {createRouter} from '@tanstack/react-router';
import {setupRouterSsrQueryIntegration} from '@tanstack/react-router-ssr-query';
import {createQueryClient} from './app/queryClient';
import {routeTree} from './routeTree.gen';
import {DocumentWrap} from './ssr/document';
import {ssrRequestContext} from './ssr/requestContext';

export interface RouterContext {
  queryClient: QueryClient;
}

export function createAppRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree,
    context: {queryClient},
    defaultPreload: 'intent',
    scrollRestoration: true,
    // The light demo is served from a folder (/MedApp/ on GitHub Pages).
    ...(__LIGHT_DEMO__ ? {basepath: import.meta.env.BASE_URL} : {}),
  });
}

/**
 * The router for a page rendered on the server and taken over in the browser: it renders the whole page
 * (`DocumentWrap`), and carries the data its loaders fetched on the server across to the browser in the page, so
 * the browser starts with it rather than fetching it again. A new one (and query cache) for every server request.
 *
 * On the server, it puts the page's Content Security Policy nonce (from the request's context) on every script it
 * writes, and in a `csp-nonce` meta tag, where the browser's router finds it when it takes over.
 */
export function createSsrRouter() {
  const queryClient = createQueryClient({isSsr: true});
  const router = createRouter({
    routeTree,
    context: {queryClient},
    defaultPreload: 'intent',
    scrollRestoration: true,
    Wrap: DocumentWrap,
    ssr: {nonce: ssrRequestContext()?.cspNonce},
  });
  // The page's own providers (AppProviders, in the document) supply the cache to components.
  setupRouterSsrQueryIntegration({router, queryClient, wrapQueryClient: false});
  return router;
}

declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
