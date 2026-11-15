import type {QueryClient} from '@tanstack/react-query';
import {createRouter} from '@tanstack/react-router';
import {setupRouterSsrQueryIntegration} from '@tanstack/react-router-ssr-query';
import {createQueryClient} from './app/queryClient';
import {routeTree} from './routeTree.gen';
import {DocumentWrap} from './ssr/document';

export interface RouterContext {
  queryClient: QueryClient;
}

export function createAppRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree,
    context: {queryClient},
    defaultPreload: 'intent',
    scrollRestoration: true,
  });
}

/**
 * The router for a page rendered on the server and taken over in the browser: it renders the whole page
 * (`DocumentWrap`), and carries the data its loaders fetched on the server across to the browser in the page, so
 * the browser starts with it rather than fetching it again. A new one (and query cache) for every server request.
 */
export function createSsrRouter() {
  const queryClient = createQueryClient({isSsr: true});
  const router = createRouter({
    routeTree,
    context: {queryClient},
    defaultPreload: 'intent',
    scrollRestoration: true,
    Wrap: DocumentWrap,
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
